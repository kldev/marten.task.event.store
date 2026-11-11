using Halcyon.App.Features.Tickets;
using Halcyon.Database.Documents;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;

namespace Halcyon.IntegrationTests.Infrastructure;

/// <summary>A started host with the seed's agents and people, a fixed clock and helpers to report tickets.</summary>
public abstract class TicketTestBase(PostgresContainer postgres) : IAsyncLifetime
{
    protected static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    protected TestApp App { get; private set; } = null!;

    /// <summary>The run's PostgreSQL, for a test that starts a second host.</summary>
    protected PostgresContainer Postgres => postgres;
    protected AgentRef Anna { get; private set; } = null!;
    protected AgentRef Piotr { get; private set; } = null!;
    protected AgentRef Kasia { get; private set; } = null!;
    protected Person Adam { get; private set; } = null!;

    public virtual async ValueTask InitializeAsync()
    {
        App = await TestApp.StartAsync(postgres, now: Now);
        await App.SeedPeopleAsync();
        Anna = await App.AgentAsync("anna.nowak@company.io");
        Piotr = await App.AgentAsync("piotr.kowalski@company.io");
        Kasia = await App.AgentAsync("katarzyna.wisniewska@company.io");
        Adam = await App.PersonAsync("adam.mazur@company.io");
    }

    public virtual async ValueTask DisposeAsync() => await App.DisposeAsync();

    protected ReportTicketCommand Command(
        Priority? priority = Priority.Medium,
        DateTimeOffset? reportedAt = null,
        Guid? personId = null,
        bool incognito = false,
        string subcategory = "it.hardware",
        string title = "Laptop nie ładuje"
    ) =>
        new(
            title,
            "Opis sprawy.",
            subcategory.Split('.')[0],
            subcategory,
            priority,
            reportedAt ?? Now,
            incognito ? null : personId ?? Adam.Id,
            incognito
        );

    protected async Task<Guid> ReportAsync(
        Priority priority = Priority.Medium,
        TimeSpan? ago = null,
        string subcategory = "it.hardware",
        string title = "Laptop nie ładuje"
    )
    {
        var reported = await App.Get<ReportTicket>()
            .ByAgentAsync(
                Anna,
                Command(priority, Now - (ago ?? TimeSpan.Zero), subcategory: subcategory, title: title)
            );
        return reported.TicketId;
    }

    protected async Task<TicketView> ViewAsync(Guid id)
    {
        await using var session = App.Store.QuerySession();
        return (await session.LoadAsync<TicketView>(id))!;
    }
}
