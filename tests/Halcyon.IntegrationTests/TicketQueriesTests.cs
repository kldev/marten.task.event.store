using Halcyon.App.Features.Tickets;
using Halcyon.Domain.Tickets;
using Halcyon.IntegrationTests.Infrastructure;

namespace Halcyon.IntegrationTests;

/// <summary>Specs 003 and 004: the lists, their filters and the alert counters.</summary>
public class TicketQueriesTests(PostgresContainer postgres) : TicketTestBase(postgres)
{
    private Guid overdue;
    private Guid dueSoon;
    private Guid onTime;
    private Guid mine;
    private Guid done;
    private Guid legal;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        overdue = await ReportAsync(Priority.Critical, TimeSpan.FromHours(4), title: "overdue");
        dueSoon = await ReportAsync(Priority.Critical, TimeSpan.FromHours(2.5), title: "duesoon");
        onTime = await ReportAsync(Priority.Low, TimeSpan.FromHours(1), title: "ontime");
        mine = await ReportAsync(Priority.High, TimeSpan.FromHours(1), title: "mine");
        done = await ReportAsync(Priority.Critical, TimeSpan.FromHours(10), title: "done");
        legal = await ReportAsync(Priority.Medium, TimeSpan.Zero, "legal.gdpr", "legal");

        await App.Get<TakeTicket>().ExecuteAsync(mine, Anna);
        await App.Get<TakeTicket>().ExecuteAsync(done, Piotr);
        await App.Get<ResolveTicket>().ExecuteAsync(done, Piotr);
    }

    private Task<IReadOnlyList<Database.Documents.TicketView>> ListAsync(TicketFilter filter) =>
        App.Get<TicketQueries>().ListAsync(filter);

    [Fact]
    public async Task Pool_HasOnlyUnassigned_MostUrgentFirst()
    {
        var pool = await ListAsync(new TicketFilter(TicketScope.Pool));
        pool.Select(t => t.Id).ShouldBe([overdue, dueSoon, legal, onTime]);
    }

    [Fact]
    public async Task Mine_HasOnlyTheAgentsInProgressTickets()
    {
        (await ListAsync(new TicketFilter(TicketScope.Mine, Anna.Id))).Select(t => t.Id).ShouldBe([mine]);
        (await ListAsync(new TicketFilter(TicketScope.Mine, Piotr.Id))).ShouldBeEmpty();
    }

    [Fact]
    public async Task All_HasOpenFirst_ThenResolved()
    {
        var all = await ListAsync(new TicketFilter(TicketScope.All));
        all.Count.ShouldBe(6);
        all[^1].Id.ShouldBe(done);
    }

    [Fact]
    public async Task OverdueFilter_IgnoresResolvedTickets()
    {
        var list = await ListAsync(new TicketFilter(TicketScope.All, Deadline: DeadlineFilter.Overdue));
        list.Select(t => t.Id).ShouldBe([overdue]);
    }

    [Fact]
    public async Task DueSoonFilter_HasOnlyTheLastHour()
    {
        var list = await ListAsync(new TicketFilter(TicketScope.All, Deadline: DeadlineFilter.DueSoon));
        list.Select(t => t.Id).ShouldBe([dueSoon]);
    }

    [Fact]
    public async Task CategoryPriorityAndSubcategoryFilters()
    {
        (await ListAsync(new TicketFilter(TicketScope.All, CategoryKey: "legal"))).Select(t => t.Id).ShouldBe([legal]);
        (await ListAsync(new TicketFilter(TicketScope.All, SubcategoryKey: "legal.gdpr")))
            .Select(t => t.Id)
            .ShouldBe([legal]);
        (await ListAsync(new TicketFilter(TicketScope.Pool, Priority: Priority.Critical)))
            .Select(t => t.Id)
            .ShouldBe([overdue, dueSoon]);
    }

    [Fact]
    public async Task Counters_SeparateOverdueFromDueSoon()
    {
        var counts = await App.Get<TicketQueries>().CountsAsync(Anna.Id);
        counts.ShouldBe(new AlertCounts(Overdue: 1, DueSoon: 1, Pool: 4, Mine: 1));
    }

    [Fact]
    public async Task AsTimePasses_DueSoonBecomesOverdue()
    {
        App.Clock.Advance(TimeSpan.FromMinutes(31));

        var counts = await App.Get<TicketQueries>().CountsAsync(Anna.Id);

        counts.Overdue.ShouldBe(2);
        counts.DueSoon.ShouldBe(0);
    }

    [Fact]
    public async Task Details_OfAnUnknownTicket_IsNull() =>
        (await App.Get<TicketQueries>().DetailsAsync(Guid.NewGuid())).ShouldBeNull();
}
