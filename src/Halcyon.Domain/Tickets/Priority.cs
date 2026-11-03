namespace Halcyon.Domain.Tickets;

public enum Priority
{
    Critical,
    High,
    Medium,
    Low,
}

public enum TicketStatus
{
    InPool,
    InProgress,
    Done,
}

/// <summary>How the ticket reached the system: written down by an agent, or sent by the requester on the public page.</summary>
public enum ReportChannel
{
    Agent,
    SelfService,
}

/// <summary>Service level per priority (ADR-0004): calendar time, 24/7.</summary>
public static class Sla
{
    public static TimeSpan For(Priority priority) =>
        priority switch
        {
            Priority.Critical => TimeSpan.FromHours(3),
            Priority.High => TimeSpan.FromHours(8),
            Priority.Medium => TimeSpan.FromDays(3),
            Priority.Low => TimeSpan.FromDays(7),
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null),
        };

    public static DateTimeOffset DeadlineFor(DateTimeOffset reportedAt, Priority priority) =>
        reportedAt + For(priority);
}

public static class PriorityNames
{
    public static string Name(Priority priority) =>
        priority switch
        {
            Priority.Critical => "Krytyczny",
            Priority.High => "Wysoki",
            Priority.Medium => "Średni",
            Priority.Low => "Niski",
            _ => priority.ToString(),
        };

    public static string SlaText(Priority priority) =>
        priority switch
        {
            Priority.Critical => "3 h",
            Priority.High => "8 h",
            Priority.Medium => "3 dni",
            Priority.Low => "1 tydzień",
            _ => "",
        };

    public static string Status(TicketStatus status) =>
        status switch
        {
            TicketStatus.InPool => "W puli",
            TicketStatus.InProgress => "W realizacji",
            TicketStatus.Done => "Zrealizowane",
            _ => status.ToString(),
        };
}
