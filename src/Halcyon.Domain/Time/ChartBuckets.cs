using System.Globalization;

namespace Halcyon.Domain.Time;

/// <summary>The range of the charts (spec 009): the last 7 or 30 days, or the last 12 calendar weeks.</summary>
public enum ChartRange
{
    Days7,
    Days30,
    Weeks12,
}

/// <summary>One column of a chart: a Warsaw calendar day or a Monday-Sunday week.</summary>
/// <param name="Label">Short, under the axis: "03.11".</param>
/// <param name="LongLabel">In the tooltip and the table: "wt 03.11.2026", "02.11–08.11.2026".</param>
public sealed record ChartBucket(DayRange Days, string Label, string LongLabel)
{
    public DateTimeOffset From => Days.From;
    public DateTimeOffset Until => Days.Until;

    public bool Contains(DateTimeOffset instant) => Days.Contains(instant);
}

public static class ChartBuckets
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>
    /// The buckets of a range, oldest first, the last one holding today. Whole calendar units, never "the last 168
    /// hours": a day that changes the clock is 23 or 25 hours long.
    /// </summary>
    /// <param name="culture">The weekday's language in the long label ("wt. 03.11.2026"); Polish unless the caller
    /// shows the chart in another one.</param>
    public static IReadOnlyList<ChartBucket> For(ChartRange range, DateOnly today, CultureInfo? culture = null)
    {
        switch (range)
        {
            case ChartRange.Weeks12:
            {
                var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                return Enumerable
                    .Range(0, 12)
                    .Select(i => monday.AddDays(-7 * (11 - i)))
                    .Select(start =>
                    {
                        var days = new DayRange(start, start.AddDays(6));
                        return new ChartBucket(days, start.ToString("dd.MM", CultureInfo.InvariantCulture), days.Text);
                    })
                    .ToList();
            }
            default:
            {
                var count = range == ChartRange.Days7 ? 7 : 30;
                return Enumerable
                    .Range(0, count)
                    .Select(i => today.AddDays(i - count + 1))
                    .Select(day => new ChartBucket(
                        new DayRange(day, day),
                        day.ToString("dd.MM", CultureInfo.InvariantCulture),
                        day.ToString("ddd dd.MM.yyyy", culture ?? Polish)
                    ))
                    .ToList();
            }
        }
    }

    /// <summary>Monday = 0 … Sunday = 6, in Warsaw time.</summary>
    public static int Weekday(DateTimeOffset instant) => ((int)LocalTime.ToLocal(instant).DayOfWeek + 6) % 7;

    /// <summary>The hour 0-23 in Warsaw time.</summary>
    public static int Hour(DateTimeOffset instant) => LocalTime.ToLocal(instant).Hour;
}
