using Halcyon.Domain.Time;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 008: the agents' report counts whole calendar periods.</summary>
public class ReportPeriodsTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Theory]
    [InlineData(2026, 11, 2)] // Monday
    [InlineData(2026, 11, 6)] // Friday
    [InlineData(2026, 11, 8)] // Sunday
    public void TheWeek_IsMondayToSunday_OfThatWeek(int y, int m, int d) =>
        ReportPeriods.For(D(y, m, d)).Week.ShouldBe(new DayRange(D(2026, 11, 2), D(2026, 11, 8)));

    [Fact]
    public void TheMonth_IsTheWholeMonth_AndThreeMonths_AreItAndTheTwoBefore()
    {
        var periods = ReportPeriods.For(D(2026, 11, 6));
        periods.Month.ShouldBe(new DayRange(D(2026, 11, 1), D(2026, 11, 30)));
        periods.ThreeMonths.ShouldBe(new DayRange(D(2026, 9, 1), D(2026, 11, 30)));
    }

    [Fact]
    public void Previous_AreTheLastCompleteOnes()
    {
        var periods = ReportPeriods.For(D(2026, 11, 6), previous: true);
        periods.Week.ShouldBe(new DayRange(D(2026, 10, 26), D(2026, 11, 1)));
        periods.Month.ShouldBe(new DayRange(D(2026, 10, 1), D(2026, 10, 31)));
        periods.ThreeMonths.ShouldBe(new DayRange(D(2026, 8, 1), D(2026, 10, 31)));
    }

    [Fact]
    public void AcrossTheNewYear()
    {
        var periods = ReportPeriods.For(D(2027, 1, 1)); // a Friday
        periods.Week.ShouldBe(new DayRange(D(2026, 12, 28), D(2027, 1, 3)));
        periods.ThreeMonths.ShouldBe(new DayRange(D(2026, 11, 1), D(2027, 1, 31)));
        ReportPeriods.For(D(2027, 1, 15), previous: true).Month.ShouldBe(new DayRange(D(2026, 12, 1), D(2026, 12, 31)));
    }

    [Fact]
    public void ADay_EndsAtWarsawMidnight()
    {
        var week = ReportPeriods.For(D(2026, 11, 6)).Week;
        week.From.ShouldBe(new DateTimeOffset(2026, 11, 1, 23, 0, 0, TimeSpan.Zero));
        week.Contains(new DateTimeOffset(2026, 11, 8, 22, 59, 59, TimeSpan.Zero)).ShouldBeTrue();
        week.Contains(new DateTimeOffset(2026, 11, 8, 23, 0, 0, TimeSpan.Zero)).ShouldBeFalse();
    }

    [Fact]
    public void Today_IsInWarsaw() =>
        ReportPeriods.Today(new DateTimeOffset(2026, 11, 8, 23, 30, 0, TimeSpan.Zero)).ShouldBe(D(2026, 11, 9));
}
