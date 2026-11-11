namespace Halcyon.App.Notifications;

/// <summary>
/// A mail about a ticket, as a message (spec 013). <see cref="MailId"/> is its idempotency key: the durable queue
/// delivers at least once, the key makes the mail leave at most once (Ambit's <c>EventId</c>).
/// </summary>
public interface ITicketMail
{
    Guid MailId { get; }
    Guid TicketId { get; }
}
