using Halcyon.Domain.Tickets;
using static Halcyon.Domain.Tests.TestData;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 004: the requester sets the priority, an agent may change it; the deadline counts from the report date.</summary>
public class PriorityTests
{
    [Fact]
    public void Change_RecomputesTheDeadlineFromTheReportDate_NotFromNow()
    {
        var reportedAt = Now.AddDays(-2);
        var ticket = Reported(Priority.Medium, reportedAt);

        var changed = ticket.ChangePriority(Anna, Priority.High, "pilne", Now)!;
        ticket = ticket.Then(changed);

        ticket.Deadline.ShouldBe(reportedAt.AddHours(8));
        ticket.Deadline.ShouldNotBe(Now.AddHours(8));
        ticket.StateAt(Now).ShouldBe(DeadlineState.Overdue);
        changed.From.ShouldBe(Priority.Medium);
        changed.To.ShouldBe(Priority.High);
        ticket.OriginalPriority.ShouldBe(Priority.Medium);
    }

    [Fact]
    public void Raising_OnAFourHourOldTicket_MakesItOverdue()
    {
        var ticket = Reported(Priority.Low, Now.AddHours(-4));
        ticket.StateAt(Now).ShouldBe(DeadlineState.OnTime);

        ticket = ticket.Then(ticket.ChangePriority(Anna, Priority.Critical, null, Now));

        ticket.StateAt(Now).ShouldBe(DeadlineState.Overdue);
    }

    [Fact]
    public void Lowering_AnOverdueTicket_ClearsTheAlert()
    {
        var ticket = Reported(Priority.Critical, Now.AddHours(-4));
        ticket.StateAt(Now).ShouldBe(DeadlineState.Overdue);

        ticket = ticket.Then(ticket.ChangePriority(Anna, Priority.Medium, null, Now));

        ticket.StateAt(Now).ShouldBe(DeadlineState.OnTime);
    }

    [Fact]
    public void SamePriority_IsNoChange_AndNoEvent() =>
        Reported(Priority.High).ChangePriority(Anna, Priority.High, null, Now).ShouldBeNull();

    [Fact]
    public void Change_OnAResolvedTicket_IsRefused()
    {
        var ticket = TakenBy(Anna);
        ticket = ticket.Then(ticket.Resolve(Anna, Now));
        Should.Throw<DomainException>(() => ticket.ChangePriority(Anna, Priority.Critical, null, Now));
    }

    [Fact]
    public void Change_ByAnyAgent_InThePoolOrInProgress_IsAllowed()
    {
        Reported().ChangePriority(Piotr, Priority.Low, null, Now).ShouldNotBeNull();
        TakenBy(Anna).ChangePriority(Piotr, Priority.Low, null, Now).ShouldNotBeNull();
    }

    [Fact]
    public void Change_ToAnUnknownPriority_IsRefused() =>
        Should.Throw<DomainException>(() => Reported().ChangePriority(Anna, (Priority)42, null, Now));
}
