namespace Halcyon.Domain.Tickets;

public static class TicketLimits
{
    /// <summary>How far ahead a report date may be: clocks of two machines are never exactly the same.</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    /// <summary>How far back an agent may date a report (a phone call this morning, a mail from last week).</summary>
    public static readonly TimeSpan MaxBackdating = TimeSpan.FromDays(30);

    /// <summary>Under this much time left a ticket is "due soon" (warning).</summary>
    public static readonly TimeSpan DueSoonThreshold = TimeSpan.FromHours(1);
}
