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

/// <summary>
/// How long each priority has (ADR-0004, ADR-0013): calendar time, 24/7. <see cref="Default"/> is the owner's
/// 3 h / 8 h / 3 days / 1 week; a deployment may set other windows (<c>Sla:*Hours</c>). The deadline a ticket gets is
/// written into its events, so a changed policy moves only the tickets reported or re-prioritised after the change.
/// </summary>
public sealed record SlaPolicy(TimeSpan Critical, TimeSpan High, TimeSpan Medium, TimeSpan Low)
{
    /// <summary>The owner's windows - and the ones every ticket stored before ADR-0013 was given.</summary>
    public static SlaPolicy Default { get; } =
        new(TimeSpan.FromHours(3), TimeSpan.FromHours(8), TimeSpan.FromDays(3), TimeSpan.FromDays(7));

    public TimeSpan For(Priority priority) =>
        priority switch
        {
            Priority.Critical => Critical,
            Priority.High => High,
            Priority.Medium => Medium,
            Priority.Low => Low,
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null),
        };

    public DateTimeOffset DeadlineFor(DateTimeOffset reportedAt, Priority priority) => reportedAt + For(priority);

    /// <summary>"3 h", "3 dni", "1 tydzień" - or in English, "3 days", "1 week".</summary>
    public string Text(Priority priority, bool english = false) => SlaWindowText.Format(For(priority), english);

    /// <summary>
    /// Null when the windows make sense, otherwise why not: each must be positive, and a more urgent priority must not
    /// have more time than a less urgent one.
    /// </summary>
    public string? Problem()
    {
        TimeSpan[] windows = [Critical, High, Medium, Low];
        if (windows.Any(w => w <= TimeSpan.Zero))
            return "Every SLA window must be positive.";
        for (var i = 1; i < windows.Length; i++)
            if (windows[i] < windows[i - 1])
                return "A more urgent priority cannot have more time than a less urgent one (Critical <= High <= Medium <= Low).";
        return null;
    }
}

/// <summary>An SLA window in words: whole weeks, then whole days, then hours (and minutes).</summary>
public static class SlaWindowText
{
    public static string Format(TimeSpan window, bool english = false)
    {
        var minutes = (long)Math.Floor(window.TotalMinutes);
        const long day = 24 * 60;
        if (minutes > 0 && minutes % (7 * day) == 0)
        {
            var weeks = minutes / (7 * day);
            return english ? $"{weeks} {(weeks == 1 ? "week" : "weeks")}" : $"{weeks} {PolishWeeks(weeks)}";
        }
        if (minutes > 0 && minutes % day == 0)
        {
            var days = minutes / day;
            return english ? $"{days} {(days == 1 ? "day" : "days")}" : $"{days} {(days == 1 ? "dzień" : "dni")}";
        }
        return minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60} min";
    }

    private static string PolishWeeks(long n) =>
        n == 1 ? "tydzień"
        : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "tygodnie"
        : "tygodni";
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

    public static string Status(TicketStatus status) =>
        status switch
        {
            TicketStatus.InPool => "W puli",
            TicketStatus.InProgress => "W realizacji",
            TicketStatus.Done => "Zrealizowane",
            _ => status.ToString(),
        };
}
