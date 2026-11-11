using Halcyon.Database.Documents;
using JasperFx;
using Marten;

namespace Halcyon.App.Notifications;

/// <summary>
/// At most once per message (Ambit's <c>ProcessedEventStore.SendOnceAsync</c>): the claim (<see cref="SentMail"/>, by
/// <see cref="ITicketMail.MailId"/>) is taken <b>before</b> the send and <b>released when the send throws</b>. A
/// redelivered or concurrent copy finds the claim and stays quiet; a failed attempt leaves nothing that would make the
/// retry a silent no-op.
/// </summary>
public sealed class MailDelivery(
    IDocumentStore store,
    ISendEmail sender,
    TimeProvider time,
    ILogger<MailDelivery> logger
)
{
    public async Task SendOnceAsync(ITicketMail message, OutgoingMail? mail, CancellationToken ct)
    {
        if (mail is null)
            return;

        await using (var claim = store.LightweightSession())
        {
            claim.Insert(
                new SentMail
                {
                    Id = message.MailId,
                    Kind = message.GetType().Name,
                    TicketId = message.TicketId,
                    To = mail.To,
                    SentAt = time.GetUtcNow(),
                }
            );
            try
            {
                await claim.SaveChangesAsync(ct);
            }
            catch (DocumentAlreadyExistsException)
            {
                logger.LogInformation("Mail {MailId} already sent", message.MailId);
                return;
            }
        }

        try
        {
            await sender.SendAsync(mail, ct);
        }
        catch
        {
            await using var release = store.LightweightSession();
            release.Delete<SentMail>(message.MailId);
            await release.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
}
