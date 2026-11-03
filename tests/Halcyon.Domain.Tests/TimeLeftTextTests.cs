using Halcyon.Domain.Tickets;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 004: time left in hours, minutes under an hour, a different text past the deadline.</summary>
public class TimeLeftTextTests
{
    [Theory]
    [InlineData(2 * 24 * 60 + 5 * 60, "2 dni 5 h")]
    [InlineData(24 * 60, "1 dzień 0 h")]
    [InlineData(24 * 60 + 59, "1 dzień 0 h")]
    [InlineData(7 * 24 * 60, "7 dni 0 h")]
    [InlineData(23 * 60 + 59, "23 h 59 min")]
    [InlineData(5 * 60 + 20, "5 h 20 min")]
    [InlineData(60, "1 h 0 min")]
    [InlineData(59, "59 min")]
    [InlineData(45, "45 min")]
    [InlineData(1, "1 min")]
    public void Remaining(int minutes, string expected) =>
        TimeLeftText.Format(TimeSpan.FromMinutes(minutes)).ShouldBe(expected);

    [Fact]
    public void UnderAMinute_IsZeroMinutes_RoundedDown() =>
        TimeLeftText.Format(TimeSpan.FromSeconds(59)).ShouldBe("0 min");

    [Fact]
    public void RoundsDown() =>
        TimeLeftText.Format(TimeSpan.FromMinutes(60) - TimeSpan.FromSeconds(1)).ShouldBe("59 min");

    [Theory]
    [InlineData(1, "po terminie o 1 min")]
    [InlineData(2 * 60 + 10, "po terminie o 2 h 10 min")]
    [InlineData(3 * 24 * 60 + 60, "po terminie o 3 dni 1 h")]
    public void Overdue(int minutesLate, string expected) =>
        TimeLeftText.Format(TimeSpan.FromMinutes(-minutesLate)).ShouldBe(expected);

    [Fact]
    public void AtTheDeadline_SaysOverdue_WithoutMinusZero()
    {
        TimeLeftText.Format(TimeSpan.Zero).ShouldBe("po terminie");
        TimeLeftText.Format(TimeSpan.FromSeconds(-30)).ShouldBe("po terminie");
    }
}
