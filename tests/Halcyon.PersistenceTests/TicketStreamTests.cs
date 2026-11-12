using Halcyon.Database.Documents;
using Halcyon.Domain.Tickets;
using Halcyon.Domain.ValueObjects;
using Halcyon.PersistenceTests.Infrastructure;
using Npgsql;

namespace Halcyon.PersistenceTests;

/// <summary>
/// A ticket's stream in the store (ADR-0002, ADR-0003): the inline read model is written in the same save as the
/// events, the write model is folded by the domain's own Evolve, and an exclusive fetch makes a second writer wait.
/// </summary>
public class TicketStreamTests(PostgresContainer postgres) : StoreTestBase(postgres)
{
    [Fact]
    public async Task AReport_WritesTheListRow_InTheSameSave()
    {
        var reported = await ReportAsync();

        await using var session = Store.QuerySession();
        var view = await session.LoadAsync<TicketView>(reported.TicketId);

        view.ShouldNotBeNull();
        view.Number.ShouldBe(TicketNumber.For(reported.TicketId).Value);
        view.Status.ShouldBe(TicketStatus.InPool);
        view.Deadline.ShouldBe(Ticket.DeadlineOf(reported));
        view.RequesterName.ShouldBe("Adam Mazur");
    }

    [Fact]
    public async Task TheWriteModel_IsFoldedFromTheStream_AndNeverStored()
    {
        var reported = await ReportAsync();

        await using (var session = Store.LightweightSession())
        {
            var stream = await session.Events.FetchForWriting<Ticket>(reported.TicketId);
            stream.Aggregate.ShouldNotBeNull();
            stream.Aggregate.Title.Value.ShouldBe("Laptop nie ładuje");
            stream.AppendOne(stream.Aggregate.Take(Anna, Now));
            await session.SaveChangesAsync();
        }

        await using var query = Store.QuerySession();
        var ticket = await query.Events.AggregateStreamAsync<Ticket>(reported.TicketId);
        ticket!.Status.ShouldBe(TicketStatus.InProgress);
        ticket.Assignee.ShouldBe(Anna);
        (await query.LoadAsync<TicketView>(reported.TicketId))!.AssigneeId.ShouldBe(Anna.Id);

        // Live aggregation: there is no document table for the aggregate.
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("select to_regclass('halcyon.mt_doc_ticket') is null", connection);
        ((bool)(await command.ExecuteScalarAsync())!).ShouldBeTrue();
    }

    [Fact]
    public async Task AnExclusiveFetch_MakesTheSecondWriterWait_AndDecideOnTheFirstOnesResult()
    {
        var reported = await ReportAsync();

        await using var first = Store.LightweightSession();
        var firstStream = await first.Events.FetchForExclusiveWriting<Ticket>(reported.TicketId);
        firstStream.AppendOne(firstStream.Aggregate!.Take(Anna, Now));

        var second = Task.Run(async () =>
        {
            await using var session = Store.LightweightSession();
            var stream = await session.Events.FetchForExclusiveWriting<Ticket>(reported.TicketId);
            return stream.Aggregate!.Status;
        });

        // The first one still holds the stream: the second waits instead of reading the old state.
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        second.IsCompleted.ShouldBeFalse();

        await first.SaveChangesAsync();

        (await second.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(TicketStatus.InProgress);
    }

    [Fact]
    public async Task TheListColumns_AreDuplicatedOutOfTheJson()
    {
        // The lists filter by status and assignee and sort by the deadline (HalcyonStore): real columns, not jsonb paths.
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            select column_name from information_schema.columns
            where table_schema = 'halcyon' and table_name = 'mt_doc_ticketview'
            """,
            connection
        );
        var columns = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));

        columns.ShouldContain("status");
        columns.ShouldContain("assignee_id");
        columns.ShouldContain("deadline");
        columns.ShouldContain("number");
    }
}
