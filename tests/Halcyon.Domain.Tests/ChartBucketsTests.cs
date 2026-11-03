using Halcyon.Domain.Time;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 009: the charts count whole Warsaw calendar days and Monday-Sunday weeks, ending today.</summary>
public class ChartBucketsTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Theory]
    [InlineData(ChartRange.Days7, 7)]
    [InlineData(ChartRange.Days30, 30)]
    public void Days_EndToday_OldestFirst(ChartRange range, int count)
    {
        var buckets = ChartBuckets.For(range, D(2026, 11, 3));

        buckets.Count.ShouldBe(count);
        buckets[^1].Days.ShouldBe(new DayRange(D(2026, 11, 3), D(2026, 11, 3)));
        buckets[0].Days.Start.ShouldBe(D(2026, 11, 3).AddDays(1 - count));
        buckets.Zip(buckets.Skip(1)).ShouldAllBe(p => p.First.Until == p.Second.From);
        buckets[^1].Label.ShouldBe("03.11");
        buckets[^1].LongLabel.ShouldBe("wt. 03.11.2026");
    }

    [Fact]
    public void Weeks_AreTwelveCalendarWeeks_TheLastHoldingToday()
    {
        var buckets = ChartBuckets.For(ChartRange.Weeks12, D(2026, 11, 6)); // a Friday

        buckets.Count.ShouldBe(12);
        buckets[^1].Days.ShouldBe(new DayRange(D(2026, 11, 2), D(2026, 11, 8)));
        buckets[0].Days.ShouldBe(new DayRange(D(2026, 8, 17), D(2026, 8, 23)));
        buckets.ShouldAllBe(b => b.Days.Start.DayOfWeek == DayOfWeek.Monday);
        buckets[^1].LongLabel.ShouldBe("02.11–08.11.2026");
    }

    [Fact]
    public void ADay_StartsAtWarsawMidnight_AndTheClockChangeDay_Is25Hours()
    {
        var buckets = ChartBuckets.For(ChartRange.Days7, D(2026, 10, 27));
        var change = buckets.Single(b => b.Days.Start == D(2026, 10, 25)); // CEST -> CET

        change.From.ShouldBe(new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero));
        (change.Until - change.From).ShouldBe(TimeSpan.FromHours(25));
    }

    [Fact]
    public void WeekdayAndHour_AreInWarsawTime()
    {
        // Sunday 01.11.2026 23:30 UTC = Monday 02.11 00:30 in Warsaw.
        var instant = new DateTimeOffset(2026, 11, 1, 23, 30, 0, TimeSpan.Zero);
        ChartBuckets.Weekday(instant).ShouldBe(0);
        ChartBuckets.Hour(instant).ShouldBe(0);
    }
}
