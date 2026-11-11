using Halcyon.Domain.Tickets;
using Halcyon.Domain.ValueObjects;
using static Halcyon.Domain.Tests.TestData;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 003: take, resolve, hand over, return.</summary>
public class WorkflowTests
{
    [Fact]
    public void Take_FromThePool_MakesItTheAgents()
    {
        var ticket = Reported().Then(Reported().Take(Anna, Now));

        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.Assignee.ShouldBe(Anna);
    }

    [Fact]
    public void Take_WhenAlreadyTaken_NamesWhoHasIt() =>
        Should.Throw<DomainException>(() => TakenBy(Anna).Take(Piotr, Now)).Message.ShouldContain("Anna Nowak");

    [Fact]
    public void Take_WhenResolved_IsRefused()
    {
        var ticket = TakenBy(Anna);
        ticket = ticket.Then(ticket.Resolve(Anna, Now));
        Should.Throw<DomainException>(() => ticket.Take(Piotr, Now));
    }

    [Fact]
    public void Resolve_ByAssignee_IsDone()
    {
        var ticket = TakenBy(Anna);
        var resolved = ticket.Resolve(Anna, Now.AddHours(1));
        ticket = ticket.Then(resolved);

        ticket.Status.ShouldBe(TicketStatus.Done);
        ticket.ResolvedAt.ShouldBe(Now.AddHours(1));
        resolved.Late.ShouldBeFalse();
        ticket.StateAt(Now.AddYears(1)).ShouldBe(DeadlineState.None);
    }

    [Fact]
    public void Resolve_AfterTheDeadline_IsMarkedLate()
    {
        var ticket = TakenBy(Anna, Priority.Critical);
        var resolved = ticket.Resolve(Anna, Now.AddHours(3));

        resolved.Late.ShouldBeTrue();
        ticket.Then(resolved).ResolvedLate.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_ByAnotherAgent_IsRefused() =>
        Should.Throw<DomainException>(() => TakenBy(Anna).Resolve(Piotr, Now)).Message.ShouldContain("Anna Nowak");

    [Fact]
    public void Resolve_FromThePool_IsRefused() => Should.Throw<DomainException>(() => Reported().Resolve(Anna, Now));

    [Fact]
    public void Resolve_Twice_IsRefused()
    {
        var ticket = TakenBy(Anna);
        ticket = ticket.Then(ticket.Resolve(Anna, Now));
        Should.Throw<DomainException>(() => ticket.Resolve(Anna, Now)).Message.ShouldContain("już zrealizowane");
    }

    [Fact]
    public void HandOver_MovesTheTicket_AndKeepsTheDeadline()
    {
        var ticket = TakenBy(Anna, Priority.High);
        var deadline = ticket.Deadline;

        var handed = ticket.HandOver(Anna, Piotr, "  urlop  ", Now);
        ticket = ticket.Then(handed);

        ticket.Assignee.ShouldBe(Piotr);
        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.Deadline.ShouldBe(deadline);
        handed.Reason.ShouldBe("urlop");
    }

    [Fact]
    public void HandOver_EmptyReason_IsStoredAsNone() =>
        TakenBy(Anna).HandOver(Anna, Piotr, "   ", Now).Reason.ShouldBeNull();

    [Fact]
    public void HandOver_ToYourself_IsRefused() =>
        Should
            .Throw<DomainException>(() => TakenBy(Anna).HandOver(Anna, Anna, null, Now))
            .Message.ShouldContain("samemu sobie");

    [Fact]
    public void HandOver_ByNotTheAssignee_IsRefused() =>
        Should.Throw<DomainException>(() => TakenBy(Anna).HandOver(Piotr, Anna, null, Now));

    [Fact]
    public void HandOver_FromThePool_IsRefused() =>
        Should
            .Throw<DomainException>(() => Reported().HandOver(Anna, Piotr, null, Now))
            .Message.ShouldContain("w puli");

    [Fact]
    public void HandOver_WhenResolved_IsRefused()
    {
        var ticket = TakenBy(Anna);
        ticket = ticket.Then(ticket.Resolve(Anna, Now));
        Should.Throw<DomainException>(() => ticket.HandOver(Anna, Piotr, null, Now));
    }

    [Fact]
    public void HandOver_TooLongReason_IsRefused() =>
        Should.Throw<DomainException>(() =>
            TakenBy(Anna).HandOver(Anna, Piotr, new string('x', Reason.MaxLength + 1), Now)
        );

    [Fact]
    public void ReturnToPool_ClearsTheAssignee_AndKeepsTheDeadline()
    {
        var ticket = TakenBy(Anna, Priority.Critical);
        var deadline = ticket.Deadline;

        ticket = ticket.Then(ticket.ReturnToPool(Anna, "szkolenie", Now));

        ticket.Status.ShouldBe(TicketStatus.InPool);
        ticket.Assignee.ShouldBeNull();
        ticket.Deadline.ShouldBe(deadline);
    }

    [Fact]
    public void ReturnToPool_ThenAnyoneCanTakeIt_EvenTheSameAgent()
    {
        var ticket = TakenBy(Anna);
        ticket = ticket.Then(ticket.ReturnToPool(Anna, null, Now));

        Should.NotThrow(() => ticket.Take(Anna, Now));
        ticket.Then(ticket.Take(Piotr, Now)).Assignee.ShouldBe(Piotr);
    }

    [Fact]
    public void ReturnToPool_ByAnotherAgent_IsRefused() =>
        Should.Throw<DomainException>(() => TakenBy(Anna).ReturnToPool(Piotr, null, Now));

    [Fact]
    public void ReturnToPool_FromThePool_IsRefused() =>
        Should.Throw<DomainException>(() => Reported().ReturnToPool(Anna, null, Now));

    [Fact]
    public void Apply_NeverRefusesAStoredEvent()
    {
        // A stored event is a fact (ADR-0003): even an out-of-order history folds without an exception.
        var ticket = Reported();
        Should.NotThrow(() =>
        {
            ticket = ticket.Then(new TicketResolved(ticket.Id, Anna, Now, false));
            ticket = ticket.Then(new TicketTaken(ticket.Id, Piotr, Now));
            ticket = ticket.Then(new TicketReturnedToPool(ticket.Id, Anna, null, Now));
        });
        ticket.Status.ShouldBe(TicketStatus.InPool);
    }
}
