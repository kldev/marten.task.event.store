using Halcyon.Database;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using JasperFx;
using Marten;
using Npgsql;

namespace Halcyon.PersistenceTests.Infrastructure;

/// <summary>
/// A store described by <see cref="HalcyonStore.Configure"/> - the one the app and the migrator use - on the class's own
/// database, with the schema applied up front. Every write goes through its own session, as concurrent requests do.
/// </summary>
public abstract class StoreTestBase(PostgresContainer postgres) : IAsyncLifetime
{
    protected static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
    protected static readonly AgentRef Anna = new(Guid.Parse("00000000-0000-0000-0000-00000000000a"), "Anna Nowak");
    private static readonly PersonRef Adam = new(Guid.Parse("00000000-0000-0000-0000-0000000000c1"), "Adam", "Mazur");

    private NpgsqlDataSource dataSource = null!;

    protected IDocumentStore Store { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        dataSource = NpgsqlDataSource.Create(await postgres.CreateDatabaseAsync());
        Store = DocumentStore.For(options =>
        {
            HalcyonStore.Configure(options);
            options.Connection(dataSource);
            options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
        });
        await Store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Store.Dispose();
        await dataSource.DisposeAsync();
    }

    protected NpgsqlDataSource DataSource => dataSource;

    /// <summary>A report of an IT hardware ticket by Anna for Adam, as the domain makes it.</summary>
    protected static TicketReported Reported(Guid? id = null) =>
        Ticket.Report(
            id ?? Guid.CreateVersion7(),
            new TicketDraft(
                "Laptop nie ładuje",
                "Dioda nie świeci.",
                "it",
                "it.hardware",
                Priority.Medium,
                Now,
                Requester.For(Adam),
                ReportChannel.Agent,
                Anna
            ),
            Now
        );

    /// <summary>Starts the ticket's stream in its own session and saves it.</summary>
    protected async Task<TicketReported> ReportAsync(Guid? id = null)
    {
        var reported = Reported(id);
        await using var session = Store.LightweightSession();
        session.Events.StartStream<Ticket>(reported.TicketId, reported);
        await session.SaveChangesAsync();
        return reported;
    }

    /// <summary>Saves one document in its own session.</summary>
    protected async Task SaveAsync<T>(T document)
        where T : notnull
    {
        await using var session = Store.LightweightSession();
        session.Store(document);
        await session.SaveChangesAsync();
    }
}
