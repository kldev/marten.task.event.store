using Halcyon.Database.Documents;
using Halcyon.Database.Seed;
using Halcyon.Domain.Tickets;
using Halcyon.IntegrationTests.Infrastructure;
using Marten;

namespace Halcyon.IntegrationTests;

/// <summary>Spec 007 / ADR-0006: insert what is missing, never overwrite.</summary>
public class SeedTests(PostgresContainer postgres) : IAsyncLifetime
{
    private TestApp app = null!;

    public async ValueTask InitializeAsync() => app = await TestApp.StartAsync(postgres);

    public async ValueTask DisposeAsync() => await app.DisposeAsync();

    private async Task<(int Agents, int People, int Tickets, long Events)> CountsAsync()
    {
        await using var session = app.Store.QuerySession();
        return (
            await session.Query<Agent>().CountAsync(),
            await session.Query<Person>().CountAsync(),
            await session.Query<TicketView>().CountAsync(),
            await session.Events.QueryAllRawEvents().LongCountAsync()
        );
    }

    [Fact]
    public async Task FirstRun_AddsTenAgents_FortyPeople_AndTicketsInEveryState()
    {
        var result = await app.Get<HalcyonSeeder>().RunAsync();

        result.ShouldBe(new SeedResult(10, 40, SeedData.Tickets.Count));
        await using var session = app.Store.QuerySession();
        var tickets = await session.Query<TicketView>().ToListAsync();
        var now = app.Clock.GetUtcNow();
        tickets.ShouldContain(t => t.Status == TicketStatus.InPool);
        tickets.ShouldContain(t => t.Status == TicketStatus.InProgress);
        tickets.ShouldContain(t => t.Status == TicketStatus.Done && !t.ResolvedLate);
        tickets.ShouldContain(t => t.Status == TicketStatus.Done && t.ResolvedLate);
        tickets.ShouldContain(t => t.StateAt(now) == DeadlineState.Overdue);
        tickets.ShouldContain(t => t.StateAt(now) == DeadlineState.DueSoon);
        tickets.ShouldContain(t => t.RequesterIncognito);
        tickets.ShouldContain(t => t.Channel == ReportChannel.SelfService);
        tickets.ShouldContain(t => t.CommentCount > 0);
        tickets.ShouldContain(t => t.Priority != t.OriginalPriority);
    }

    [Fact]
    public async Task SecondRun_AddsNothing()
    {
        await app.Get<HalcyonSeeder>().RunAsync();
        var before = await CountsAsync();

        app.Clock.Advance(TimeSpan.FromDays(1));
        var second = await app.Get<HalcyonSeeder>().RunAsync();

        second.NothingAdded.ShouldBeTrue();
        (await CountsAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task ARecordChangedByHand_IsNotOverwritten()
    {
        await app.Get<HalcyonSeeder>().RunAsync();
        var id = StableId.Agent("anna.nowak@company.io");
        await using (var session = app.Store.LightweightSession())
        {
            var anna = (await session.LoadAsync<Agent>(id))!;
            anna.Phone = "+48 999 999 999";
            session.Store(anna);
            await session.SaveChangesAsync();
        }

        await app.Get<HalcyonSeeder>().RunAsync();

        await using var check = app.Store.QuerySession();
        (await check.LoadAsync<Agent>(id))!.Phone.ShouldBe("+48 999 999 999");
    }

    [Fact]
    public async Task AChangedDefinition_ChangesNothingThatExists_AndAddsOnlyTheNewKey()
    {
        await app.Get<HalcyonSeeder>().RunAsync();
        var before = await CountsAsync();

        var changed = SeedDefinition.Default with
        {
            Agents =
            [
                .. SeedData.Agents.Select(a =>
                    a.Email == "anna.nowak@company.io" ? a with { LastName = "Zmieniona" } : a
                ),
                new SeedAgent("Nowy", "Agent", "+48 600 100 199", "nowy.agent@company.io"),
            ],
        };
        var result = await app.Get<HalcyonSeeder>().RunAsync(changed);

        result.ShouldBe(new SeedResult(1, 0, 0));
        var after = await CountsAsync();
        after.Agents.ShouldBe(before.Agents + 1);
        after.Events.ShouldBe(before.Events);
        await using var session = app.Store.QuerySession();
        (await session.LoadAsync<Agent>(StableId.Agent("anna.nowak@company.io")))!.LastName.ShouldBe("Nowak");
    }

    [Fact]
    public void StableIds_DoNotDependOnLetterCase() =>
        StableId.Agent(" Anna.Nowak@Company.io").ShouldBe(StableId.Agent("anna.nowak@company.io"));

    [Fact]
    public void SeedData_UsesOnlyTheCompanyDomain_AndUniqueAddresses()
    {
        var emails = SeedData.Agents.Select(a => a.Email).Concat(SeedData.People.Select(p => p.Email)).ToList();
        emails.ShouldAllBe(e => e.EndsWith("@company.io"));
        emails.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(emails.Count);
        SeedData.Agents.Count.ShouldBe(10);
        SeedData.People.Count.ShouldBe(40);
    }
}
