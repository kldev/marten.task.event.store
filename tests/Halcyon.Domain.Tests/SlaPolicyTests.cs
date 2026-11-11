using Halcyon.Domain.Tickets;
using static Halcyon.Domain.Tests.TestData;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 012 / ADR-0013: configurable SLA windows, and the deadline as a fact in the ticket's events.</summary>
public class SlaPolicyTests
{
    private static readonly SlaPolicy Short = new(
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4),
        TimeSpan.FromDays(2),
        TimeSpan.FromDays(5)
    );

    [Theory]
    [InlineData(Priority.Critical, "3 h", "3 h")]
    [InlineData(Priority.High, "8 h", "8 h")]
    [InlineData(Priority.Medium, "3 dni", "3 days")]
    [InlineData(Priority.Low, "1 tydzień", "1 week")]
    public void DefaultWindows_InWords(Priority priority, string polish, string english)
    {
        SlaPolicy.Default.Text(priority).ShouldBe(polish);
        SlaPolicy.Default.Text(priority, english: true).ShouldBe(english);
    }

    [Theory]
    [InlineData(24, "1 dzień", "1 day")]
    [InlineData(120, "5 dni", "5 days")]
    [InlineData(336, "2 tygodnie", "2 weeks")]
    [InlineData(840, "5 tygodni", "5 weeks")]
    [InlineData(25, "25 h", "25 h")]
    public void Window_InWords(int hours, string polish, string english)
    {
        SlaWindowText.Format(TimeSpan.FromHours(hours)).ShouldBe(polish);
        SlaWindowText.Format(TimeSpan.FromHours(hours), english: true).ShouldBe(english);
    }

    [Fact]
    public void Window_WithMinutes_InWords() => SlaWindowText.Format(TimeSpan.FromMinutes(90)).ShouldBe("1 h 30 min");

    [Fact]
    public void DefaultAndOrderedWindows_HaveNoProblem()
    {
        SlaPolicy.Default.Problem().ShouldBeNull();
        Short.Problem().ShouldBeNull();
        (Short with { High = Short.Critical }).Problem().ShouldBeNull();
    }

    [Fact]
    public void ANonPositiveWindow_IsAProblem() =>
        (Short with { Medium = TimeSpan.Zero }).Problem().ShouldBe("Every SLA window must be positive.");

    [Fact]
    public void AMoreUrgentPriorityWithMoreTime_IsAProblem() =>
        (Short with { Critical = TimeSpan.FromHours(5) }).Problem()!.ShouldContain("Critical <= High <= Medium <= Low");

    [Fact]
    public void Report_StoresTheDeadlineOfThePolicy_InTheEvent()
    {
        var reported = Ticket.Report(Guid.NewGuid(), Draft(Priority.Critical, Now.AddMinutes(-10)), Now, sla: Short);

        reported.Deadline.ShouldBe(Now.AddMinutes(-10).AddHours(1));
        Ticket.FromEvents([reported]).Deadline.ShouldBe(reported.Deadline!.Value);
    }

    [Fact]
    public void ChangePriority_StoresTheNewDeadline_FromTheReportDate()
    {
        var reportedAt = Now.AddHours(-2);
        var ticket = Ticket.FromEvents([
            Ticket.Report(Guid.NewGuid(), Draft(Priority.Low, reportedAt), Now, sla: Short),
        ]);

        var changed = ticket.ChangePriority(Anna, Priority.High, null, Now, Short)!;

        changed.Deadline.ShouldBe(reportedAt.AddHours(4));
        ticket.Then(changed).Deadline.ShouldBe(reportedAt.AddHours(4));
    }

    [Fact]
    public void TheDeadlineInTheEvents_WinsOverAnotherPolicyLater()
    {
        // Reported under the short windows; a later change of policy (here the default) does not move it.
        var ticket = Ticket.FromEvents([Ticket.Report(Guid.NewGuid(), Draft(Priority.Critical), Now, sla: Short)]);

        ticket.Deadline.ShouldBe(Now.AddHours(1));
        ticket.Deadline.ShouldNotBe(SlaPolicy.Default.DeadlineFor(Now, Priority.Critical));
    }

    [Fact]
    public void EventsStoredBeforeTheDeadlineWasRecorded_GetTheDefaultWindows()
    {
        var reported = Ticket.Report(Guid.NewGuid(), Draft(), Now, sla: Short) with { Deadline = null };
        var ticket = Ticket.FromEvents([reported]);
        ticket.Deadline.ShouldBe(Now.AddDays(3));

        var taken = ticket.Then(ticket.Take(Anna, Now));
        var changed = taken.ChangePriority(Anna, Priority.Critical, null, Now, Short)! with { Deadline = null };

        taken.Then(changed).Deadline.ShouldBe(Now.AddHours(3));
    }

    [Fact]
    public void Resolve_IsLate_AgainstTheStoredDeadline()
    {
        // 2 hours after a report with a 1-hour window: late, although the default (3 h) would still be on time.
        var ticket = Ticket.FromEvents([
            Ticket.Report(Guid.NewGuid(), Draft(Priority.Critical, Now.AddHours(-2)), Now, sla: Short),
        ]);
        var taken = ticket.Then(ticket.Take(Anna, Now));

        taken.Resolve(Anna, Now).Late.ShouldBeTrue();
    }
}
