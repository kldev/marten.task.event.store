using Halcyon.App.Features.Tickets;
using Halcyon.Database;
using Halcyon.Database.Documents;
using Halcyon.Domain.Tickets;
using Halcyon.IntegrationTests.Infrastructure;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Halcyon.IntegrationTests;

/// <summary>
/// Spec 012 on PostgreSQL 18 (Testcontainers): SLA windows from the configuration with the deadline stored in the
/// events (ADR-0013), and a unique ticket number (ADR-0014).
/// </summary>
public class SlaAndNumberTests(PostgresContainer postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, string?> ShortSla = new()
    {
        ["Sla:CriticalHours"] = "1",
        ["Sla:HighHours"] = "4",
        ["Sla:MediumHours"] = "48",
        ["Sla:LowHours"] = "120",
    };

    private readonly List<TestApp> apps = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var app in apps)
            await app.DisposeAsync();
    }

    private async Task<TestApp> Track(Task<TestApp> starting)
    {
        var app = await starting;
        apps.Add(app);
        await app.SeedPeopleAsync();
        return app;
    }

    private static async Task<TicketReported> ReportAsync(TestApp app, Priority priority, TimeSpan ago)
    {
        var anna = await app.AgentAsync("anna.nowak@company.io");
        var adam = await app.PersonAsync("adam.mazur@company.io");
        return await app.Get<ReportTicket>()
            .ByAgentAsync(
                anna,
                new ReportTicketCommand(
                    "Laptop nie ładuje",
                    "Opis sprawy.",
                    "it",
                    "it.hardware",
                    priority,
                    Now - ago,
                    adam.Id,
                    false
                )
            );
    }

    private static async Task<TicketView> ViewAsync(TestApp app, Guid id)
    {
        await using var session = app.Store.QuerySession();
        return (await session.LoadAsync<TicketView>(id))!;
    }

    private static async Task<Ticket> AggregateAsync(TestApp app, Guid id)
    {
        await using var session = app.Store.LightweightSession();
        return (await session.Events.FetchForWriting<Ticket>(id)).Aggregate!;
    }

    // ------------------------------------------------------------------------------------------------ SLA (ADR-0013)

    [Fact]
    public async Task ConfiguredWindows_GiveTheDeadline_OfAReportAndOfAPriorityChange()
    {
        var app = await Track(TestApp.StartAsync(postgres, settings: ShortSla, now: Now));

        var reported = await ReportAsync(app, Priority.Critical, TimeSpan.FromMinutes(10));
        reported.Deadline.ShouldBe(Now.AddMinutes(50));
        (await ViewAsync(app, reported.TicketId)).Deadline.ShouldBe(Now.AddMinutes(50));

        var anna = await app.AgentAsync("anna.nowak@company.io");
        var changed = await app.Get<ChangeTicketPriority>().ExecuteAsync(reported.TicketId, anna, Priority.Low, null);

        changed!.Deadline.ShouldBe(Now.AddMinutes(-10).AddHours(120));
        (await ViewAsync(app, reported.TicketId)).Deadline.ShouldBe(Now.AddMinutes(-10).AddHours(120));
    }

    [Fact]
    public async Task ARestartWithOtherWindows_DoesNotMoveTheTicketsAlreadyThere()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        (await Migrator.RunAsync(connectionString)).ShouldBe(0);

        var before = await Track(TestApp.StartOnAsync(connectionString, now: Now));
        var reported = await ReportAsync(before, Priority.Medium, TimeSpan.Zero);
        await before.DisposeAsync();
        apps.Remove(before);

        var after = await Track(TestApp.StartOnAsync(connectionString, ShortSla, Now));

        (await ViewAsync(after, reported.TicketId)).Deadline.ShouldBe(Now.AddDays(3));
        (await AggregateAsync(after, reported.TicketId)).Deadline.ShouldBe(Now.AddDays(3));
        // A new report takes the new windows.
        var fresh = await ReportAsync(after, Priority.Medium, TimeSpan.Zero);
        (await ViewAsync(after, fresh.TicketId)).Deadline.ShouldBe(Now.AddHours(48));
    }

    [Fact]
    public async Task EventsStoredWithoutADeadline_AreReadWithTheDefaultWindows()
    {
        var app = await Track(TestApp.StartAsync(postgres, settings: ShortSla, now: Now));
        var reported = await ReportAsync(app, Priority.High, TimeSpan.Zero);

        // A stream as it was written before ADR-0013: the same report, without the deadline.
        var legacyId = Guid.CreateVersion7();
        await using (var session = app.Store.LightweightSession())
        {
            session.Events.StartStream<Ticket>(legacyId, reported with { TicketId = legacyId, Deadline = null });
            await session.SaveChangesAsync();
        }

        (await ViewAsync(app, legacyId)).Deadline.ShouldBe(Now.AddHours(8));
        (await AggregateAsync(app, legacyId)).Deadline.ShouldBe(Now.AddHours(8));
    }

    [Fact]
    public async Task WindowsOutOfOrder_StopTheHost_NamingTheReason()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var host = TestApp.Build(
            connectionString,
            new() { ["Sla:CriticalHours"] = "10", ["Sla:HighHours"] = "5" },
            null,
            out _
        );

        var refusal = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
        refusal.Message.ShouldContain("Critical <= High <= Medium <= Low");
    }

    [Fact]
    public async Task AZeroWindow_StopsTheHost()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var host = TestApp.Build(connectionString, new() { ["Sla:LowHours"] = "0" }, null, out _);

        var refusal = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
        refusal.Message.ShouldContain("LowHours");
    }

    // ------------------------------------------------------------------------------------- unique number (ADR-0014)

    /// <summary>Hands out the ids it was given, in order; then fresh ones.</summary>
    private sealed class ScriptedIds(params Guid[] script) : TicketIds
    {
        private readonly Queue<Guid> queue = new(script);
        public int Handed { get; private set; }

        public override Guid Next()
        {
            Handed++;
            return queue.TryDequeue(out var id) ? id : Guid.CreateVersion7();
        }
    }

    /// <summary>Another version 7 id with the same last 8 hex digits - so the same "HAL-" number.</summary>
    private static Guid SameNumberAs(Guid id)
    {
        var hex = Guid.CreateVersion7().ToString("N");
        var other = Guid.ParseExact(hex[..24] + id.ToString("N")[24..], "N");
        Ticket.NumberOf(other).ShouldBe(Ticket.NumberOf(id));
        other.ShouldNotBe(id);
        return other;
    }

    [Fact]
    public async Task AReportThatMeetsAGivenNumber_IsMadeAgainUnderANewId()
    {
        var first = Guid.CreateVersion7();
        var clash = SameNumberAs(first);
        var ids = new ScriptedIds(first, clash);
        var app = await Track(TestApp.StartAsync(postgres, now: Now, services: s => s.AddSingleton<TicketIds>(ids)));

        var one = await ReportAsync(app, Priority.Medium, TimeSpan.Zero);
        var two = await ReportAsync(app, Priority.Medium, TimeSpan.Zero);

        one.TicketId.ShouldBe(first);
        two.TicketId.ShouldNotBe(clash);
        ids.Handed.ShouldBe(3);
        (await ViewAsync(app, two.TicketId)).Number.ShouldNotBe(Ticket.NumberOf(first));
        await using var session = app.Store.QuerySession();
        (await session.Events.FetchStreamStateAsync(clash)).ShouldBeNull("the failed attempt left nothing behind");
        (await session.Query<TicketView>().CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task ANumberThatKeepsMeetingGivenOnes_IsRefused_AfterThreeAttempts()
    {
        var first = Guid.CreateVersion7();
        var ids = new ScriptedIds(first, SameNumberAs(first), SameNumberAs(first), SameNumberAs(first));
        var app = await Track(TestApp.StartAsync(postgres, now: Now, services: s => s.AddSingleton<TicketIds>(ids)));
        await ReportAsync(app, Priority.Medium, TimeSpan.Zero);

        var refusal = await Should.ThrowAsync<Exception>(() => ReportAsync(app, Priority.Medium, TimeSpan.Zero));

        HalcyonStore.IsNumberTaken(refusal).ShouldBeTrue();
        ids.Handed.ShouldBe(1 + ReportTicketAttempts);
        await using var session = app.Store.QuerySession();
        (await session.Query<TicketView>().CountAsync()).ShouldBe(1);
    }

    private const int ReportTicketAttempts = 3;

    [Fact]
    public async Task TheSeed_GivesEveryTicketItsOwnNumber()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        (await Migrator.RunAsync(connectionString, "seed")).ShouldBe(0);
        var app = await Track(TestApp.StartOnAsync(connectionString, now: Now));

        await using var session = app.Store.QuerySession();
        var numbers = await session.Query<TicketView>().Select(t => t.Number).ToListAsync();
        numbers.Count.ShouldBeGreaterThan(200);
        numbers.Distinct().Count().ShouldBe(numbers.Count);
    }
}
