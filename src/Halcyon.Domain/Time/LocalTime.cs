namespace Halcyon.Domain.Time;

/// <summary>Times are stored in UTC and shown in Polish local time (ADR-0004).</summary>
public static class LocalTime
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public static DateTimeOffset ToLocal(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Zone);

    /// <summary>A wall-clock time in Warsaw (e.g. picked in the calendar) as an instant.</summary>
    public static DateTimeOffset FromLocal(DateTime wallClock)
    {
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        // In the hour that does not exist (spring forward) take the instant as if it were standard time.
        var offset = Zone.IsInvalidTime(unspecified) ? Zone.BaseUtcOffset : Zone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset);
    }

    public static string Format(DateTimeOffset utc) => ToLocal(utc).ToString("dd.MM.yyyy HH:mm");
}
