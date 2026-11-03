using Halcyon.Domain.Calendar;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 006: the date picker's grid.</summary>
public class CalendarMonthTests
{
    [Fact]
    public void Grid_HasSixWeeks_StartingOnMonday()
    {
        var month = CalendarMonth.Build(2026, 11);

        month.Days.Count.ShouldBe(42);
        month.Days[0].Date.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        month.Weeks.Count().ShouldBe(6);
    }

    [Fact]
    public void November2026_StartsOnSunday_SoTheFirstRowEndsWithIt()
    {
        // 1 Nov 2026 is a Sunday: the first row is 26 Oct - 1 Nov.
        var month = CalendarMonth.Build(2026, 11);
        month.Days[0].Date.ShouldBe(new DateOnly(2026, 10, 26));
        month.Days[6].Date.ShouldBe(new DateOnly(2026, 11, 1));
        month.Days[6].InMonth.ShouldBeTrue();
        month.Days[0].InMonth.ShouldBeFalse();
    }

    [Fact]
    public void MonthStartingOnMonday_HasNoLeadingDays()
    {
        // 1 June 2026 is a Monday.
        CalendarMonth.Build(2026, 6).Days[0].Date.ShouldBe(new DateOnly(2026, 6, 1));
    }

    [Fact]
    public void DaysAfterMax_AreDisabled()
    {
        var month = CalendarMonth.Build(2026, 11, max: new DateOnly(2026, 11, 3));

        month.Days.Single(d => d.Date == new DateOnly(2026, 11, 3)).Disabled.ShouldBeFalse();
        month.Days.Single(d => d.Date == new DateOnly(2026, 11, 4)).Disabled.ShouldBeTrue();
    }

    [Fact]
    public void DaysBeforeMin_AreDisabled()
    {
        var month = CalendarMonth.Build(2026, 11, min: new DateOnly(2026, 11, 2));

        month.Days.Single(d => d.Date == new DateOnly(2026, 11, 1)).Disabled.ShouldBeTrue();
        month.Days.Single(d => d.Date == new DateOnly(2026, 11, 2)).Disabled.ShouldBeFalse();
    }

    [Fact]
    public void Navigation_WrapsTheYear()
    {
        CalendarMonth.Build(2026, 1).Previous.ShouldBe((2025, 12));
        CalendarMonth.Build(2026, 12).Next.ShouldBe((2027, 1));
    }

    [Fact]
    public void Title_IsPolish() => CalendarMonth.Build(2026, 11).Title.ShouldBe("Listopad 2026");

    [Fact]
    public void LeapYear_February_HasTwentyNineDays() =>
        CalendarMonth.Build(2028, 2).Days.Count(d => d.InMonth).ShouldBe(29);
}
