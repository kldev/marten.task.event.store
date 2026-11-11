using Halcyon.Domain.Tickets;
using Halcyon.Domain.Time;
using Microsoft.Extensions.Time.Testing;
using static Halcyon.Domain.Tests.TestData;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 004 / ADR-0004.</summary>
public class DeadlineTests
{
    [Theory]
    [InlineData(Priority.Critical, 3 * 60)]
    [InlineData(Priority.High, 8 * 60)]
    [InlineData(Priority.Medium, 3 * 24 * 60)]
    [InlineData(Priority.Low, 7 * 24 * 60)]
    public void Sla_PerPriority(Priority priority, int minutes) =>
        SlaPolicy.Default.For(priority).ShouldBe(TimeSpan.FromMinutes(minutes));

    [Fact]
    public void ExactlySixtyMinutesLeft_IsStillOnTime() =>
        Deadlines.Evaluate(Now.AddMinutes(60), Now).ShouldBe(DeadlineState.OnTime);

    [Fact]
    public void FiftyNineMinutesFiftyNineSecondsLeft_IsDueSoon() =>
        Deadlines.Evaluate(Now.AddMinutes(59).AddSeconds(59), Now).ShouldBe(DeadlineState.DueSoon);

    [Fact]
    public void OneSecondLeft_IsDueSoon() => Deadlines.Evaluate(Now.AddSeconds(1), Now).ShouldBe(DeadlineState.DueSoon);

    [Fact]
    public void TheDeadlineInstant_IsOverdue() => Deadlines.Evaluate(Now, Now).ShouldBe(DeadlineState.Overdue);

    [Fact]
    public void PastTheDeadline_IsOverdue() =>
        Deadlines.Evaluate(Now.AddMinutes(-1), Now).ShouldBe(DeadlineState.Overdue);

    [Fact]
    public void Resolved_HasNoState() =>
        Deadlines.Evaluate(TicketStatus.Done, Now.AddDays(-5), Now).ShouldBe(DeadlineState.None);

    [Fact]
    public void ATicket_GoesFromOnTime_ToDueSoon_ToOverdue_AsTimePasses()
    {
        var clock = new FakeTimeProvider(Now);
        var ticket = Reported(Priority.Critical, Now);

        ticket.StateAt(clock.GetUtcNow()).ShouldBe(DeadlineState.OnTime);
        clock.Advance(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(1)));
        ticket.StateAt(clock.GetUtcNow()).ShouldBe(DeadlineState.DueSoon);
        clock.Advance(TimeSpan.FromMinutes(59));
        ticket.StateAt(clock.GetUtcNow()).ShouldBe(DeadlineState.Overdue);
    }

    [Fact]
    public void ADstChange_DoesNotMoveTheDeadline()
    {
        // 2026-10-25 03:00 CEST -> 02:00 CET in Warsaw. Reported at 01:30 local (CEST, UTC+2), Critical = 3 h.
        var reportedLocal = new DateTime(2026, 10, 25, 1, 30, 0);
        var reportedAt = LocalTime.FromLocal(reportedLocal);
        reportedAt.Offset.ShouldBe(TimeSpan.FromHours(2));

        var deadline = SlaPolicy.Default.DeadlineFor(reportedAt, Priority.Critical);

        (deadline - reportedAt).ShouldBe(TimeSpan.FromHours(3));
        // Three real hours later the wall clock shows 03:30 CET, not 04:30.
        LocalTime.ToLocal(deadline).ToString("HH:mm").ShouldBe("03:30");
    }

    [Fact]
    public void LocalTime_FormatsInWarsaw()
    {
        LocalTime.Format(new DateTimeOffset(2026, 7, 1, 10, 5, 0, TimeSpan.Zero)).ShouldBe("01.07.2026 12:05");
        LocalTime.Format(new DateTimeOffset(2026, 12, 1, 10, 5, 0, TimeSpan.Zero)).ShouldBe("01.12.2026 11:05");
    }

    [Fact]
    public void LocalTime_InTheSkippedHour_DoesNotThrow()
    {
        // 2026-03-29 02:30 does not exist in Warsaw (clocks jump 02:00 -> 03:00).
        Should.NotThrow(() => LocalTime.FromLocal(new DateTime(2026, 3, 29, 2, 30, 0)));
    }
}
