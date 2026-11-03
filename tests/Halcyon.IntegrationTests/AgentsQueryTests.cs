using Halcyon.App.Features.Agents;
using Halcyon.App.Features.Tickets;
using Halcyon.IntegrationTests.Infrastructure;

namespace Halcyon.IntegrationTests;

/// <summary>Spec 001: agents see each other and the people.</summary>
public class AgentsQueryTests(PostgresContainer postgres) : TicketTestBase(postgres)
{
    [Fact]
    public async Task Agents_HaveNamePhoneEmailAndLoad()
    {
        var id = await ReportAsync();
        await App.Get<TakeTicket>().ExecuteAsync(id, Piotr);

        var agents = await App.Get<AgentDirectory>().AgentsAsync();

        agents.Count.ShouldBe(10);
        agents.ShouldAllBe(a => a.Email.EndsWith("@company.io") && a.Phone.Length > 0);
        agents.Single(a => a.Id == Piotr.Id).InProgress.ShouldBe(1);
        agents.Where(a => a.Id != Piotr.Id).ShouldAllBe(a => a.InProgress == 0);
    }

    [Fact]
    public async Task People_AreForty_AndNoneIsAnAgent()
    {
        var people = await App.Get<AgentDirectory>().PeopleAsync();
        var agents = await App.Get<AgentDirectory>().AgentsAsync();

        people.Count.ShouldBe(40);
        people.Select(p => p.Email).Intersect(agents.Select(a => a.Email)).ShouldBeEmpty();
    }
}
