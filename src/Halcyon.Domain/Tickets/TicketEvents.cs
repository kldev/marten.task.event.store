using Halcyon.Domain.People;

namespace Halcyon.Domain.Tickets;

// The facts of a ticket's life (ADR-0003). Stored as they are: never change a field's meaning; add a new event
// instead. Every event carries the actor as they were at that moment.

public sealed record TicketReported(
    Guid TicketId,
    string Title,
    string Description,
    string CategoryKey,
    string SubcategoryKey,
    Priority Priority,
    DateTimeOffset ReportedAt,
    Requester Requester,
    ReportChannel Channel,
    AgentRef? RegisteredBy,
    DateTimeOffset RecordedAt,
    IReadOnlyDictionary<string, string>? Fields = null,
    // The deadline the ticket was given (ADR-0013). Null in events stored before it: those had SlaPolicy.Default.
    DateTimeOffset? Deadline = null
);

public sealed record TicketTaken(Guid TicketId, AgentRef Agent, DateTimeOffset At);

public sealed record TicketHandedOver(Guid TicketId, AgentRef From, AgentRef To, string? Reason, DateTimeOffset At);

public sealed record TicketReturnedToPool(Guid TicketId, AgentRef Agent, string? Reason, DateTimeOffset At);

public sealed record TicketPriorityChanged(
    Guid TicketId,
    Priority From,
    Priority To,
    AgentRef Agent,
    string? Reason,
    DateTimeOffset At,
    // The new deadline, from the report date (ADR-0013). Null in events stored before it: SlaPolicy.Default.
    DateTimeOffset? Deadline = null
);

public sealed record TicketResolved(Guid TicketId, AgentRef Agent, DateTimeOffset At, bool Late);

public sealed record TicketCommented(Guid TicketId, Guid CommentId, AgentRef Agent, string Text, DateTimeOffset At);

/// <summary>
/// A file attached by an agent (<see cref="Agent"/>) or by the requester on the public form (Agent null). The file
/// itself is in the object storage under <see cref="StorageKey"/>; the event is the record that it belongs here.
/// </summary>
public sealed record TicketAttachmentAdded(
    Guid TicketId,
    Guid AttachmentId,
    string FileName,
    string ContentType,
    long Size,
    string StorageKey,
    string AddedBy,
    AgentRef? Agent,
    DateTimeOffset At
);

/// <summary>
/// The report data of a ticket filled in or corrected (spec 008). Only what changed; <see cref="FieldChange.To"/> null
/// clears a value. The label is the one shown at that moment, so the history reads the same after a rename.
/// </summary>
public sealed record TicketFieldsUpdated(
    Guid TicketId,
    IReadOnlyList<FieldChange> Changes,
    AgentRef Agent,
    DateTimeOffset At
);

public sealed record FieldChange(string Key, string Label, string? From, string? To);

/// <summary>
/// The agent who took the ticket names the person who does the work (spec 008) - for HR somebody from Kadry, e.g. when
/// the agent has no access to the system that needs it. <see cref="Person"/> null takes the person off again.
/// </summary>
public sealed record TicketHandlerAssigned(
    Guid TicketId,
    PersonRef? Person,
    AgentRef Agent,
    string? Reason,
    DateTimeOffset At
);
