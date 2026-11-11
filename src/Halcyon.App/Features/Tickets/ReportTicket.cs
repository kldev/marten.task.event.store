using System.Threading.RateLimiting;
using Halcyon.Database;
using Halcyon.Database.Documents;
using Halcyon.Domain;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;

namespace Halcyon.App.Features.Tickets;

/// <summary>
/// What a report form sends. <see cref="ReportedAt"/> is used only when an agent registers the ticket; the public
/// form reports "now". The requester is a person from the list xor incognito (spec 002). Files go in with the report,
/// in the same stream and the same save.
/// </summary>
public sealed record ReportTicketCommand(
    string? Title,
    string? Description,
    string? CategoryKey,
    string? SubcategoryKey,
    Priority? Priority,
    DateTimeOffset? ReportedAt,
    Guid? RequesterPersonId,
    bool Incognito,
    IReadOnlyList<PendingFile>? Files = null,
    IReadOnlyDictionary<string, string?>? Fields = null
);

/// <summary>What a new-ticket form starts with when another ticket prepared it (spec 008).</summary>
public sealed record ReportPrefill(
    string? SubcategoryKey,
    string? Title,
    string? Description,
    Guid? RequesterPersonId,
    IReadOnlyDictionary<string, string?> Fields
)
{
    public const string CampaignSubcategory = "marketing.budget";

    /// <summary>
    /// Recruitment asks marketing for a campaign: the budget topic, the goal "Rekrutacja", the position, how many people
    /// and the recruitment ticket's number, in the requester's name.
    /// </summary>
    public static string CampaignUrlFor(TicketView recruitment)
    {
        var position = recruitment.Fields.GetValueOrDefault("position");
        var query = new List<(string, string?)>
        {
            ("podkategoria", CampaignSubcategory),
            ("tytul", position is null ? "Kampania rekrutacyjna" : $"Kampania rekrutacyjna: {position}"),
            ("opis", $"Zapotrzebowanie z rekrutacji {recruitment.Number}: {recruitment.Title}"),
            ("zglaszajacy", recruitment.RequesterId?.ToString()),
            ("pole-campaign-goal", "Rekrutacja"),
            ("pole-related-ticket", recruitment.Number),
            ("pole-position", position),
            ("pole-headcount", recruitment.Fields.GetValueOrDefault("headcount")),
        };
        return "nowe?"
            + string.Join(
                "&",
                query
                    .Where(q => !string.IsNullOrEmpty(q.Item2))
                    .Select(q => $"{q.Item1}={Uri.EscapeDataString(q.Item2!)}")
            );
    }

    /// <summary>The query of <see cref="CampaignUrlFor"/> back; null without a subcategory.</summary>
    public static ReportPrefill? FromQuery(IReadOnlyDictionary<string, string> query) =>
        query.TryGetValue("podkategoria", out var sub)
            ? new ReportPrefill(
                sub,
                query.GetValueOrDefault("tytul"),
                query.GetValueOrDefault("opis"),
                Guid.TryParse(query.GetValueOrDefault("zglaszajacy"), out var person) ? person : null,
                query
                    .Where(q => q.Key.StartsWith("pole-", StringComparison.Ordinal))
                    .ToDictionary(q => q.Key["pole-".Length..], q => (string?)q.Value)
            )
            : null;
}

/// <summary>Where a new ticket's id - and so its number - comes from; a test replaces it to make two numbers meet.</summary>
public class TicketIds
{
    public virtual Guid Next() => Guid.CreateVersion7();
}

/// <summary>
/// Reports a new ticket into the pool, by an agent or by the requester themselves (spec 002). The deadline comes from
/// the configured SLA and is stored in the event (ADR-0013); the number is unique by a unique index, and a report that
/// meets a number already given is made again under a new id (ADR-0014).
/// </summary>
public sealed class ReportTicket(
    IDocumentStore store,
    TimeProvider time,
    PublicReportLimiter limiter,
    AttachmentFiles files,
    SlaPolicy sla,
    TicketIds ids
)
{
    /// <summary>Two 8-digit numbers meeting is rare; three times in a row means something else is wrong.</summary>
    private const int NumberAttempts = 3;

    public async Task<TicketReported> ByAgentAsync(
        AgentRef agent,
        ReportTicketCommand command,
        CancellationToken ct = default
    )
    {
        if (command.ReportedAt is null)
            throw new DomainException("Podaj datę i godzinę zgłoszenia.");
        return await ReportAsync(command, command.ReportedAt.Value, ReportChannel.Agent, agent, ct);
    }

    /// <summary>The public page: no sign-in, the moment of sending is the report date (ADR-0004, ADR-0005).</summary>
    public async Task<TicketReported> SelfServiceAsync(ReportTicketCommand command, CancellationToken ct = default)
    {
        using var lease = limiter.TryAcquire();
        if (!lease.IsAcquired)
            throw new DomainException("Zbyt wiele zgłoszeń w krótkim czasie. Spróbuj ponownie za minutę.");
        return await ReportAsync(command, time.GetUtcNow(), ReportChannel.SelfService, null, ct);
    }

    private async Task<TicketReported> ReportAsync(
        ReportTicketCommand command,
        DateTimeOffset reportedAt,
        ReportChannel channel,
        AgentRef? registeredBy,
        CancellationToken ct
    )
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ReportOnceAsync(ids.Next(), command, reportedAt, channel, registeredBy, ct);
            }
            catch (Exception exception) when (attempt < NumberAttempts && HalcyonStore.IsNumberTaken(exception)) { }
        }
    }

    private async Task<TicketReported> ReportOnceAsync(
        Guid id,
        ReportTicketCommand command,
        DateTimeOffset reportedAt,
        ReportChannel channel,
        AgentRef? registeredBy,
        CancellationToken ct
    )
    {
        await using var session = store.LightweightSession();
        var requester = await RequesterAsync(session, command, ct);

        var now = time.GetUtcNow();
        var reported = Ticket.Report(
            id,
            new TicketDraft(
                command.Title,
                command.Description,
                command.CategoryKey,
                command.SubcategoryKey,
                command.Priority,
                reportedAt,
                requester,
                channel,
                registeredBy,
                command.Fields
            ),
            now,
            sla: sla
        );

        // The domain checks every file (type, size, how many) before anything is stored.
        var ticket = Ticket.FromEvents([reported]);
        var attached = new List<(TicketAttachmentAdded, PendingFile)>();
        foreach (var file in command.Files ?? [])
        {
            var e = ticket.AddAttachment(
                Guid.CreateVersion7(),
                file.Name,
                file.Content.Length,
                registeredBy,
                requester.DisplayName,
                now
            );
            ticket = Ticket.Evolve(ticket, e);
            attached.Add((e, file));
        }

        session.Events.StartStream<Ticket>(id, [reported, .. attached.Select(a => (object)a.Item1)]);
        await files.SaveWithAsync(attached, () => session.SaveChangesAsync(ct), ct);
        return reported;
    }

    private static async Task<Requester> RequesterAsync(
        IQuerySession session,
        ReportTicketCommand command,
        CancellationToken ct
    )
    {
        if (command.RequesterPersonId is not { } personId)
            return new Requester(command.Incognito, null);

        var person = await session.LoadAsync<Person>(personId, ct);
        if (person is not null)
            return new Requester(command.Incognito, person.ToRef());

        if (await session.LoadAsync<Agent>(personId, ct) is not null)
            throw new DomainException("Agent nie może być zgłaszającym - wybierz osobę z listy.");
        throw new DomainException("Nie ma takiej osoby na liście zgłaszających.");
    }
}

/// <summary>
/// A brake on the public form: it needs no sign-in, so a script could flood the pool. Interactive Blazor events do not
/// pass the HTTP rate limiter, hence a limiter of its own, for the whole app (ADR-0005).
/// </summary>
public sealed class PublicReportLimiter : IDisposable
{
    private readonly FixedWindowRateLimiter limiter = new(
        new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }
    );

    public RateLimitLease TryAcquire() => limiter.AttemptAcquire();

    public void Dispose() => limiter.Dispose();
}
