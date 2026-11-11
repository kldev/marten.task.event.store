using Halcyon.Domain.Tickets;
using Wolverine;

namespace Halcyon.App.Notifications;

/// <summary>The requester hears that the ticket arrived: number, title, deadline (spec 013).</summary>
public sealed record ReportConfirmationMail(Guid MailId, Guid TicketId) : ITicketMail;

/// <summary>The agent a ticket was handed over to hears it, with who handed it and why.</summary>
public sealed record HandedOverMail(Guid MailId, Guid TicketId, Guid ToAgentId, string FromName, string? Reason)
    : ITicketMail;

/// <summary>The requester hears that the ticket is resolved.</summary>
public sealed record ResolvedMail(Guid MailId, Guid TicketId) : ITicketMail;

public enum DeadlineMailKind
{
    /// <summary>An hour left (ADR-0004's "due soon").</summary>
    DueSoon,

    /// <summary>The deadline has passed.</summary>
    Overdue,
}

/// <summary>
/// A warning before the deadline or an alert after it, scheduled when the deadline is given (spec 013).
/// <see cref="Deadline"/> is the one it was scheduled for: a priority change gives a new deadline and new mails, and
/// the old ones see that theirs is no longer the ticket's and stay quiet - nothing has to be cancelled.
/// </summary>
public sealed record DeadlineMail(Guid MailId, Guid TicketId, DateTimeOffset Deadline, DeadlineMailKind Kind)
    : ITicketMail;

/// <summary>Which mails a ticket's event causes (spec 013). The handlers return them; Wolverine's outbox sends them.</summary>
public static class TicketMailPlan
{
    public static OutgoingMessages Reported(TicketReported reported, DateTimeOffset now)
    {
        var messages = new OutgoingMessages();
        if (reported.Requester is { Incognito: false, Person: not null })
            messages.Add(new ReportConfirmationMail(Guid.CreateVersion7(), reported.TicketId));
        ScheduleDeadline(messages, reported.TicketId, Ticket.DeadlineOf(reported), now);
        return messages;
    }

    public static OutgoingMessages PriorityChanged(
        TicketPriorityChanged changed,
        DateTimeOffset reportedAt,
        DateTimeOffset now
    )
    {
        var messages = new OutgoingMessages();
        ScheduleDeadline(messages, changed.TicketId, Ticket.DeadlineOf(changed, reportedAt), now);
        return messages;
    }

    public static OutgoingMessages HandedOver(TicketHandedOver handed) =>
        [new HandedOverMail(Guid.CreateVersion7(), handed.TicketId, handed.To.Id, handed.From.FullName, handed.Reason)];

    public static OutgoingMessages Resolved(TicketResolved resolved, Ticket ticket) =>
        ticket.Requester is { Incognito: false, Person: not null }
            ? [new ResolvedMail(Guid.CreateVersion7(), resolved.TicketId)]
            : [];

    /// <summary>
    /// The warning an hour before the deadline and the alert at it, delayed by this host's clock. A time already past
    /// is sent at once - a ticket back-dated past its deadline gets its alert now - and only the latest state matters:
    /// past the deadline there is no warning any more, only the alert.
    /// </summary>
    private static void ScheduleDeadline(
        OutgoingMessages messages,
        Guid ticketId,
        DateTimeOffset deadline,
        DateTimeOffset now
    )
    {
        var warningAt = deadline - TicketLimits.DueSoonThreshold;
        if (deadline > now)
            Add(
                messages,
                new DeadlineMail(Guid.CreateVersion7(), ticketId, deadline, DeadlineMailKind.DueSoon),
                warningAt - now
            );
        Add(
            messages,
            new DeadlineMail(Guid.CreateVersion7(), ticketId, deadline, DeadlineMailKind.Overdue),
            deadline - now
        );
    }

    private static void Add(OutgoingMessages messages, DeadlineMail mail, TimeSpan delay)
    {
        if (delay > TimeSpan.Zero)
            messages.Delay(mail, delay);
        else
            messages.Add(mail);
    }
}
