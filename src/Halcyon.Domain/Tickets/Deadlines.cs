namespace Halcyon.Domain.Tickets;

/// <summary>Where an unresolved ticket stands against its deadline (ADR-0004). Resolved tickets are <see cref="None"/>.</summary>
public enum DeadlineState
{
    None,
    OnTime,
    DueSoon,
    Overdue,
}

public static class Deadlines
{
    /// <summary>
    /// The deadline instant itself is already overdue; exactly 60 minutes left is still on time.
    /// </summary>
    public static DeadlineState Evaluate(DateTimeOffset deadline, DateTimeOffset now)
    {
        var left = deadline - now;
        if (left <= TimeSpan.Zero)
            return DeadlineState.Overdue;
        return left < TicketLimits.DueSoonThreshold ? DeadlineState.DueSoon : DeadlineState.OnTime;
    }

    public static DeadlineState Evaluate(TicketStatus status, DateTimeOffset deadline, DateTimeOffset now) =>
        status == TicketStatus.Done ? DeadlineState.None : Evaluate(deadline, now);
}

/// <summary>
/// "Time left" in words (ADR-0004), rounded down to whole minutes: over a day "2 dni 5 h", from an hour "5 h 20 min",
/// under an hour "45 min", past the deadline "po terminie o 2 h 10 min".
/// </summary>
public static class TimeLeftText
{
    public static string Format(DateTimeOffset deadline, DateTimeOffset now) => Format(deadline - now);

    public static string Format(TimeSpan left)
    {
        if (left > TimeSpan.Zero)
            return Duration(left);

        var late = left.Negate();
        return late < TimeSpan.FromMinutes(1) ? "po terminie" : $"po terminie o {Duration(late)}";
    }

    /// <summary>A positive duration in days/hours/minutes, rounded down to the minute.</summary>
    public static string Duration(TimeSpan duration)
    {
        var totalMinutes = (long)Math.Floor(duration.TotalMinutes);
        if (totalMinutes < 0)
            totalMinutes = 0;

        const long minutesPerDay = 24 * 60;
        if (totalMinutes >= minutesPerDay)
        {
            var days = totalMinutes / minutesPerDay;
            var hours = totalMinutes % minutesPerDay / 60;
            return $"{days} {(days == 1 ? "dzień" : "dni")} {hours} h";
        }

        if (totalMinutes >= 60)
            return $"{totalMinutes / 60} h {totalMinutes % 60} min";

        return $"{totalMinutes} min";
    }
}
