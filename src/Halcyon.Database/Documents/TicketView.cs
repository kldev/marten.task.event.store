using Halcyon.Domain.Tickets;

namespace Halcyon.Database.Documents;

/// <summary>
/// One row per ticket for the lists, written inline with the ticket's events (ADR-0002). The columns the lists filter
/// and sort by are indexed in <see cref="HalcyonStore"/>.
/// </summary>
public sealed class TicketView
{
    public Guid Id { get; set; }
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CategoryKey { get; set; } = "";
    public string SubcategoryKey { get; set; } = "";
    public Priority Priority { get; set; }
    public Priority OriginalPriority { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public TicketStatus Status { get; set; }
    public Guid? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }

    /// <summary>The person who does the work, named by the agent (spec 008).</summary>
    public Guid? HandlerId { get; set; }

    public string? HandlerName { get; set; }
    public bool RequesterIncognito { get; set; }
    public Guid? RequesterId { get; set; }
    public string RequesterName { get; set; } = "";
    public ReportChannel Channel { get; set; }
    public string? RegisteredByName { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public bool ResolvedLate { get; set; }
    public int CommentCount { get; set; }
    public List<AttachmentView> Attachments { get; set; } = [];

    /// <summary>The report data by field key (spec 008); only the values that were given.</summary>
    public Dictionary<string, string> Fields { get; set; } = [];
    public DateTimeOffset LastChangeAt { get; set; }

    public DeadlineState StateAt(DateTimeOffset now) => Deadlines.Evaluate(Status, Deadline, now);
}

/// <summary>A file on a ticket; the bytes are in the object storage under <see cref="StorageKey"/>.</summary>
public sealed class AttachmentView
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string StorageKey { get; set; } = "";
    public string AddedBy { get; set; } = "";
    public DateTimeOffset At { get; set; }
}
