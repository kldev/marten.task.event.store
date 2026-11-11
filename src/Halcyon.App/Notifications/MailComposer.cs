using System.Net;
using System.Text;
using Halcyon.Database.Documents;
using Halcyon.Domain.Categories;
using Halcyon.Domain.Tickets;
using Halcyon.Domain.Time;
using Marten;
using Microsoft.Extensions.Options;

namespace Halcyon.App.Notifications;

/// <summary>
/// What each mail says (spec 013), read at the moment it is sent: the ticket as it is now, the address as it is now.
/// Null means "nothing to send" - the ticket is gone or done, the deadline is no longer the ticket's, nobody to tell -
/// and the reason goes to the log. Polish, like the domain (ADR-0012): a requester has no language setting. Requesters
/// do not sign in (ADR-0005), so their mails carry no link; the agents' mails link to the ticket.
/// </summary>
public sealed class MailComposer(IDocumentStore store, IOptions<MailOptions> options, ILogger<MailComposer> logger)
{
    public async Task<OutgoingMail?> ConfirmationAsync(ReportConfirmationMail mail, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        if (await TicketAsync(session, mail, ct) is not { } t || await RequesterAsync(session, t, ct) is not { } person)
            return null;
        return Build(
            person.Email,
            person.FullName,
            $"Przyjęliśmy zgłoszenie {t.Number}: {t.Title}",
            $"Dzień dobry {person.FirstName},",
            [
                $"przyjęliśmy Twoje zgłoszenie {t.Number} „{t.Title}”.",
                $"Temat: {Topic(t)}",
                $"Priorytet: {PriorityNames.Name(t.Priority)}",
                $"Termin realizacji: {LocalTime.Format(t.Deadline)}",
                "Napiszemy, gdy zgłoszenie zostanie zrealizowane.",
            ],
            link: null
        );
    }

    public async Task<OutgoingMail?> ResolvedAsync(ResolvedMail mail, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        if (await TicketAsync(session, mail, ct) is not { } t || await RequesterAsync(session, t, ct) is not { } person)
            return null;
        return Build(
            person.Email,
            person.FullName,
            $"Zrealizowano zgłoszenie {t.Number}: {t.Title}",
            $"Dzień dobry {person.FirstName},",
            [
                $"Twoje zgłoszenie {t.Number} „{t.Title}” zostało zrealizowane"
                    + (t.AssigneeName is { } who ? $" przez: {who}." : "."),
                "Jeśli coś jest nie tak, odpowiedz na tę wiadomość albo zgłoś sprawę ponownie.",
            ],
            link: null
        );
    }

    public async Task<OutgoingMail?> HandedOverAsync(HandedOverMail mail, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        if (await TicketAsync(session, mail, ct) is not { } t)
            return null;
        if (await session.LoadAsync<Agent>(mail.ToAgentId, ct) is not { } agent)
            return Skip(mail, "the agent is gone");
        return Build(
            agent.Email,
            agent.FullName,
            $"Przekazano Ci zgłoszenie {t.Number}: {t.Title}",
            $"Cześć {agent.FirstName},",
            [
                $"{mail.FromName} przekazał(a) Ci zgłoszenie {t.Number} „{t.Title}”.",
                .. mail.Reason is { } reason ? [$"Powód: {reason}"] : Array.Empty<string>(),
                $"Temat: {Topic(t)}",
                $"Termin: {LocalTime.Format(t.Deadline)}",
            ],
            Link(t)
        );
    }

    public async Task<OutgoingMail?> DeadlineAsync(DeadlineMail mail, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        if (await TicketAsync(session, mail, ct) is not { } t)
            return null;
        if (t.Deadline != mail.Deadline)
            return Skip(mail, "the deadline changed since it was scheduled");

        var (to, name, greeting) = await RecipientAsync(session, t, ct);
        if (to is null)
            return Skip(mail, "the ticket is in the pool and Mail:PoolAddress is empty");

        var overdue = mail.Kind == DeadlineMailKind.Overdue;
        return Build(
            to,
            name,
            overdue ? $"Po terminie: {t.Number} {t.Title}" : $"Została godzina: {t.Number} {t.Title}",
            greeting,
            [
                overdue
                    ? $"minął termin zgłoszenia {t.Number} „{t.Title}” ({LocalTime.Format(t.Deadline)})."
                    : $"za godzinę mija termin zgłoszenia {t.Number} „{t.Title}” ({LocalTime.Format(t.Deadline)}).",
                $"Priorytet: {PriorityNames.Name(t.Priority)}",
                t.AssigneeName is { } who
                    ? $"W realizacji u: {who}"
                    : "Zgłoszenie czeka w puli - nikt go jeszcze nie przejął.",
            ],
            Link(t)
        );
    }

    /// <summary>The ticket, unless it is gone or already done - a done ticket needs no more mail.</summary>
    private async Task<TicketView?> TicketAsync(IQuerySession session, ITicketMail mail, CancellationToken ct)
    {
        var ticket = await session.LoadAsync<TicketView>(mail.TicketId, ct);
        if (ticket is null)
            return Skip<TicketView>(mail, "no such ticket");
        if (ticket.Status == TicketStatus.Done && mail is not ResolvedMail)
            return Skip<TicketView>(mail, "the ticket is done");
        return ticket;
    }

    private async Task<Person?> RequesterAsync(IQuerySession session, TicketView ticket, CancellationToken ct) =>
        ticket is { RequesterIncognito: false, RequesterId: { } id } ? await session.LoadAsync<Person>(id, ct) : null;

    private async Task<(string? To, string Name, string Greeting)> RecipientAsync(
        IQuerySession session,
        TicketView ticket,
        CancellationToken ct
    )
    {
        if (ticket.AssigneeId is { } id && await session.LoadAsync<Agent>(id, ct) is { } agent)
            return (agent.Email, agent.FullName, $"Cześć {agent.FirstName},");
        return string.IsNullOrWhiteSpace(options.Value.PoolAddress)
            ? (null, "", "")
            : (options.Value.PoolAddress, "Zespół", "Cześć,");
    }

    private string? Link(TicketView ticket) =>
        string.IsNullOrWhiteSpace(options.Value.BaseUrl)
            ? null
            : $"{options.Value.BaseUrl.TrimEnd('/')}/zgloszenia/{ticket.Id}";

    private static string Topic(TicketView ticket)
    {
        var catalog = CategoryCatalog.Default;
        var category = catalog.FindCategory(ticket.CategoryKey)?.Name ?? ticket.CategoryKey;
        return catalog.FindSubcategory(ticket.SubcategoryKey) is { } found
            ? $"{category} · {(found.Subcategory.Group is { } g ? $"{g} · " : "")}{found.Subcategory.Name}"
            : category;
    }

    private OutgoingMail? Skip(ITicketMail mail, string reason) => Skip<OutgoingMail>(mail, reason);

    private T? Skip<T>(ITicketMail mail, string reason)
        where T : class
    {
        logger.LogInformation(
            "Mail {Kind} for ticket {TicketId} not sent: {Reason}",
            mail.GetType().Name,
            mail.TicketId,
            reason
        );
        return null;
    }

    private static OutgoingMail Build(
        string to,
        string toName,
        string subject,
        string greeting,
        IReadOnlyList<string> lines,
        string? link
    )
    {
        var text = new StringBuilder().AppendLine(greeting).AppendLine();
        foreach (var line in lines)
            text.AppendLine(line);
        if (link is not null)
            text.AppendLine().AppendLine($"Zgłoszenie: {link}");
        text.AppendLine().AppendLine("Halcyon - zgłoszenia do działów");

        var html = new StringBuilder("<!doctype html><html lang=\"pl\"><body style=\"font-family:sans-serif\">");
        html.Append($"<p>{WebUtility.HtmlEncode(greeting)}</p>");
        foreach (var line in lines)
            html.Append($"<p>{WebUtility.HtmlEncode(line)}</p>");
        if (link is not null)
            html.Append($"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Otwórz zgłoszenie</a></p>");
        html.Append("<p style=\"color:#4f6e77\">Halcyon - zgłoszenia do działów</p></body></html>");

        return new OutgoingMail(to, toName, subject, text.ToString(), html.ToString());
    }
}
