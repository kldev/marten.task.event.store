using Halcyon.Database.Documents;
using Halcyon.Database.Monitoring;
using Halcyon.Database.Projections;
using Halcyon.Domain.Tickets;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Schema;
using Npgsql;
using Weasel.Core;

namespace Halcyon.Database;

/// <summary>
/// The one description of the Marten store (ADR-0002), used by the app and by the migrator. The connection is not set
/// here: each process registers one <c>NpgsqlDataSource</c> and hands it to Marten with <c>UseNpgsqlDataSource()</c>.
/// How much the store may change the schema by itself is decided by the caller (<see cref="Migrations.DatabaseStartup"/>).
/// </summary>
public static class HalcyonStore
{
    public const string DocumentSchema = "halcyon";
    public const string EventSchema = "events";

    /// <summary>The unique index that keeps a ticket's short number unique (ADR-0014).</summary>
    private const string TicketNumberIndex = "uidx_ticket_number";

    public static void Configure(StoreOptions options)
    {
        options.DatabaseSchemaName = DocumentSchema;
        options.UseSystemTextJsonForSerialization(enumStorage: EnumStorage.AsString);

        options.Events.DatabaseSchemaName = EventSchema;
        options.Events.StreamIdentity = StreamIdentity.AsGuid;
        options.Events.AppendMode = EventAppendMode.Quick;

        options.Events.AddEventType<TicketReported>();
        options.Events.AddEventType<TicketTaken>();
        options.Events.AddEventType<TicketHandedOver>();
        options.Events.AddEventType<TicketReturnedToPool>();
        options.Events.AddEventType<TicketPriorityChanged>();
        options.Events.AddEventType<TicketResolved>();
        options.Events.AddEventType<TicketCommented>();
        options.Events.AddEventType<TicketAttachmentAdded>();
        options.Events.AddEventType<TicketFieldsUpdated>();
        options.Events.AddEventType<TicketHandlerAssigned>();

        // Write model: folded on demand (FetchForWriting), never stored.
        options.Projections.Add(new TicketAggregation(), ProjectionLifecycle.Live);
        // Read model of the lists: same transaction as the events.
        options.Projections.Add(new TicketViewProjection(), ProjectionLifecycle.Inline);

        options.Schema.For<Agent>().UniqueIndex(UniqueIndexType.Computed, "uidx_agent_email", x => x.EmailNormalized);
        options.Schema.For<Person>().UniqueIndex(UniqueIndexType.Computed, "uidx_person_email", x => x.EmailNormalized);

        // The lists filter by status and assignee and sort/filter by the deadline.
        options
            .Schema.For<TicketView>()
            .Duplicate(x => x.Status)
            .Duplicate(x => x.AssigneeId!)
            .Duplicate(x => x.Deadline)
            .Index(x => x.CategoryKey)
            // A number is given once (ADR-0014): two reports that meet on one fail the slower save, which is made again.
            .UniqueIndex(UniqueIndexType.DuplicatedField, TicketNumberIndex, x => x.Number);

        // HTTP answers and the application's log, with retention (ADR-0011).
        MonitoringSchema.Register(options);
    }

    /// <summary>Whether a save failed because the ticket's number is already given (the unique index, ADR-0014).</summary>
    public static bool IsNumberTaken(Exception exception) =>
        exception switch
        {
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TicketNumberIndex } =>
                true,
            { InnerException: { } inner } => IsNumberTaken(inner),
            _ => false,
        };
}
