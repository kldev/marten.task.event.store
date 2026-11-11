namespace Halcyon.Database.Documents;

/// <summary>
/// A mail that left (spec 013), by its message's <c>MailId</c>: the claim taken before the send and removed when the
/// send fails, so a redelivered message never sends twice and a failed one is tried again (Ambit's processed events).
/// </summary>
public sealed class SentMail
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public Guid TicketId { get; set; }
    public string To { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}
