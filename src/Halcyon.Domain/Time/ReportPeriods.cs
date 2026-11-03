using System.Globalization;

namespace Halcyon.Domain.Time;

/// <summary>Calendar days [<see cref="Start"/>, <see cref="End"/>], both included.</summary>
public sealed record DayRange(DateOnly Start, DateOnly End)
{
    /// <summary>The first instant of <see cref="Start"/> in Warsaw time.</summary>
    public DateTimeOffset From => LocalTime.FromLocal(Start.ToDateTime(TimeOnly.MinValue));

    /// <summary>The first instant after <see cref="End"/> in Warsaw time (exclusive).</summary>
    public DateTimeOffset Until => LocalTime.FromLocal(End.AddDays(1).ToDateTime(TimeOnly.MinValue));

    public bool Contains(DateTimeOffset instant) => instant >= From && instant < Until;

    public string Text =>
        $"{Start.ToString("dd.MM", CultureInfo.InvariantCulture)}–{End.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
}

/// <summary>
/// The calendar periods of the agents' report (spec 008): always whole calendar units, never "the last 7 days". On a
/// Friday the week is Monday to Sunday of that week; the month is the 1st to its last day; three months are this one
/// and the two before. <c>previous</c> gives the last complete ones (last week, last month, the three before this).
/// </summary>
public sealed record ReportPeriods(DayRange Week, DayRange Month, DayRange ThreeMonths)
{
    public static ReportPeriods For(DateOnly today, bool previous = false)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        if (previous)
        {
            monday = monday.AddDays(-7);
            firstOfMonth = firstOfMonth.AddMonths(-1);
        }
        return new ReportPeriods(
            new DayRange(monday, monday.AddDays(6)),
            new DayRange(firstOfMonth, firstOfMonth.AddMonths(1).AddDays(-1)),
            new DayRange(firstOfMonth.AddMonths(-2), firstOfMonth.AddMonths(1).AddDays(-1))
        );
    }

    /// <summary>Today in Warsaw.</summary>
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(LocalTime.ToLocal(now).DateTime);
}
