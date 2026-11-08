using Halcyon.Database.Documents;
using Halcyon.Domain.Categories;
using Halcyon.Domain.Tickets;
using Marten;
using static Halcyon.App.Localization.Tr;

namespace Halcyon.App.Features.Tickets;

public enum TicketScope
{
    Pool,
    Mine,
    All,
}

public enum DeadlineFilter
{
    Any,
    Overdue,
    DueSoon,
}

/// <summary>The "only open / only resolved" toggle of the lists and reports.</summary>
public enum CompletionFilter
{
    Any,
    Open,
    Done,
}

public sealed record TicketFilter(
    TicketScope Scope,
    Guid? AgentId = null,
    string? CategoryKey = null,
    string? SubcategoryKey = null,
    Priority? Priority = null,
    DeadlineFilter Deadline = DeadlineFilter.Any,
    CompletionFilter Completion = CompletionFilter.Any,
    string? Search = null
);

/// <summary>What the filter bar of a list reports when any of its filters changes.</summary>
public sealed record TicketFilterChange(
    string? Category,
    string? Subcategory,
    Priority? Priority,
    DeadlineFilter Deadline,
    CompletionFilter Completion,
    string? Search
);

/// <param name="Page">From 0.</param>
public sealed record TicketPage(IReadOnlyList<TicketView> Items, int Page, int PageSize, bool HasMore);

public sealed record AlertCounts(int Overdue, int DueSoon, int Pool, int Mine);

public enum TimelineKind
{
    Reported,
    Taken,
    HandedOver,
    Returned,
    PriorityChanged,
    Resolved,
    Commented,
    AttachmentAdded,
    FieldsUpdated,
    HandlerAssigned,
}

public sealed record TimelineEntry(DateTimeOffset At, TimelineKind Kind, string Actor, string Text, string? Note);

public sealed record TicketDetails(
    TicketView Ticket,
    string CategoryName,
    string SubcategoryName,
    IReadOnlyList<TimelineEntry> Timeline
);

/// <summary>The lists (pool, mine, all), the alert counters and the ticket page (specs 003, 004, 005).</summary>
public sealed class TicketQueries(IDocumentStore store, TimeProvider time)
{
    public const int PageSize = 25;

    /// <summary>Every ticket of the filter (tests, small lists); the pages use <see cref="PageAsync"/>.</summary>
    public async Task<IReadOnlyList<TicketView>> ListAsync(TicketFilter filter, CancellationToken ct = default) =>
        (await PageAsync(filter, 0, 100_000, ct)).Items;

    /// <summary>
    /// One page, open tickets first by deadline, then resolved ones latest first (the order the lists always had) - in
    /// the database, with no total: <see cref="TicketPage.HasMore"/> comes from one row more than asked for, and the
    /// total is counted only on request (<see cref="CountAsync"/>).
    /// </summary>
    public async Task<TicketPage> PageAsync(
        TicketFilter filter,
        int page,
        int pageSize = PageSize,
        CancellationToken ct = default
    )
    {
        page = Math.Max(page, 0);
        var skip = page * pageSize;
        await using var session = store.QuerySession();

        var open = await Filtered(session, filter)
            .Where(t => t.Status != TicketStatus.Done)
            .OrderBy(t => t.Deadline)
            .ThenBy(t => t.Id)
            .Skip(skip)
            .Take(pageSize + 1)
            .ToListAsync(ct);
        if (open.Count > pageSize)
            return new TicketPage(open.Take(pageSize).ToList(), page, pageSize, HasMore: true);

        // The page reaches past the open ones: how many open there are tells where the resolved ones start.
        var openTotal =
            open.Count > 0
                ? skip + open.Count
                : await Filtered(session, filter).Where(t => t.Status != TicketStatus.Done).CountAsync(ct);
        var done = await Filtered(session, filter)
            .Where(t => t.Status == TicketStatus.Done)
            .OrderByDescending(t => t.ResolvedAt)
            .ThenBy(t => t.Id)
            .Skip(Math.Max(0, skip - openTotal))
            .Take(pageSize - open.Count + 1)
            .ToListAsync(ct);

        var items = open.Concat(done).ToList();
        return new TicketPage(items.Take(pageSize).ToList(), page, pageSize, HasMore: items.Count > pageSize);
    }

    /// <summary>The total of the filter - the "Policz wszystkie" button.</summary>
    public async Task<int> CountAsync(TicketFilter filter, CancellationToken ct = default)
    {
        await using var session = store.QuerySession();
        return await Filtered(session, filter).CountAsync(ct);
    }

    private IQueryable<TicketView> Filtered(IQuerySession session, TicketFilter filter)
    {
        var now = time.GetUtcNow();
        var soon = now + TicketLimits.DueSoonThreshold;
        IQueryable<TicketView> query = session.Query<TicketView>();

        query = filter.Scope switch
        {
            TicketScope.Pool => query.Where(t => t.Status == TicketStatus.InPool),
            TicketScope.Mine => query.Where(t => t.Status == TicketStatus.InProgress && t.AssigneeId == filter.AgentId),
            _ => query,
        };

        if (!string.IsNullOrEmpty(filter.CategoryKey))
            query = query.Where(t => t.CategoryKey == filter.CategoryKey);
        if (!string.IsNullOrEmpty(filter.SubcategoryKey))
            query = query.Where(t => t.SubcategoryKey == filter.SubcategoryKey);
        if (filter.Priority is { } priority)
            query = query.Where(t => t.Priority == priority);
        if (filter.Search?.Trim() is { Length: > 0 } term)
        {
            // A part of the title (case-insensitive) or the exact number; % and _ are letters here, not wildcards.
            var number = term.ToUpperInvariant();
            query = query.Where(t => t.Title.Contains(term, StringComparison.OrdinalIgnoreCase) || t.Number == number);
        }

        query = filter.Deadline switch
        {
            DeadlineFilter.Overdue => query.Where(t => t.Status != TicketStatus.Done && t.Deadline <= now),
            DeadlineFilter.DueSoon => query.Where(t =>
                t.Status != TicketStatus.Done && t.Deadline > now && t.Deadline < soon
            ),
            _ => query,
        };

        return filter.Completion switch
        {
            CompletionFilter.Open => query.Where(t => t.Status != TicketStatus.Done),
            CompletionFilter.Done => query.Where(t => t.Status == TicketStatus.Done),
            _ => query,
        };
    }

    public async Task<AlertCounts> CountsAsync(Guid? agentId, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var soon = now + TicketLimits.DueSoonThreshold;

        await using var session = store.QuerySession();
        var open = await session
            .Query<TicketView>()
            .Where(t => t.Status != TicketStatus.Done)
            .Select(t => new
            {
                t.Status,
                t.Deadline,
                t.AssigneeId,
            })
            .ToListAsync(ct);

        return new AlertCounts(
            Overdue: open.Count(t => t.Deadline <= now),
            DueSoon: open.Count(t => t.Deadline > now && t.Deadline < soon),
            Pool: open.Count(t => t.Status == TicketStatus.InPool),
            Mine: open.Count(t => t.Status == TicketStatus.InProgress && t.AssigneeId == agentId)
        );
    }

    /// <summary>A ticket by its short number (a linked ticket in the report data).</summary>
    public async Task<Guid?> FindByNumberAsync(string number, CancellationToken ct = default)
    {
        await using var session = store.QuerySession();
        var upper = number.Trim().ToUpperInvariant();
        var found = await session.Query<TicketView>().Where(t => t.Number == upper).Select(t => t.Id).ToListAsync(ct);
        return found.Count == 0 ? null : found[0];
    }

    /// <summary>The marketing campaigns that point at this ticket (spec 008: recruitment → marketing).</summary>
    public async Task<IReadOnlyList<TicketView>> LinkedCampaignsAsync(string number, CancellationToken ct = default)
    {
        await using var session = store.QuerySession();
        var campaigns = await session
            .Query<TicketView>()
            .Where(t => t.SubcategoryKey == ReportPrefill.CampaignSubcategory)
            .ToListAsync(ct);
        return campaigns
            .Where(t => t.Fields.GetValueOrDefault("related-ticket") == number)
            .OrderBy(t => t.ReportedAt)
            .ToList();
    }

    public async Task<TicketDetails?> DetailsAsync(Guid ticketId, CancellationToken ct = default)
    {
        await using var session = store.QuerySession();
        var view = await session.LoadAsync<TicketView>(ticketId, ct);
        if (view is null)
            return null;

        var events = await session.Events.FetchStreamAsync(ticketId, token: ct);
        var timeline = events.Select(e => ToEntry(e.Data)).OfType<TimelineEntry>().ToList();
        var (category, subcategory) = CategoryCatalog.Default.NamesFor(view.CategoryKey, view.SubcategoryKey);
        return new TicketDetails(view, category, subcategory, timeline);
    }

    private static TimelineEntry? ToEntry(object data) =>
        data switch
        {
            TicketReported r => new TimelineEntry(
                r.RecordedAt,
                TimelineKind.Reported,
                r.RegisteredBy?.FullName ?? r.Requester.DisplayName,
                r.Channel == ReportChannel.SelfService
                    ? T(
                        $"Zgłoszenie wysłane samodzielnie, priorytet {PriorityNames.Name(r.Priority)}",
                        $"Reported by the requester, priority {N(PriorityNames.Name(r.Priority))}"
                    )
                    : T(
                        $"Zgłoszenie zarejestrowane przez agenta w imieniu: {r.Requester.DisplayName}, priorytet {PriorityNames.Name(r.Priority)}",
                        $"Registered by an agent on behalf of: {r.Requester.DisplayName}, priority {N(PriorityNames.Name(r.Priority))}"
                    ),
                null
            ),
            TicketTaken t => new TimelineEntry(
                t.At,
                TimelineKind.Taken,
                t.Agent.FullName,
                T("Przejęcie z puli", "Taken from the pool"),
                null
            ),
            TicketHandedOver h => new TimelineEntry(
                h.At,
                TimelineKind.HandedOver,
                h.From.FullName,
                T($"Przekazanie do: {h.To.FullName}", $"Handed over to: {h.To.FullName}"),
                h.Reason
            ),
            TicketReturnedToPool p => new TimelineEntry(
                p.At,
                TimelineKind.Returned,
                p.Agent.FullName,
                T("Oddanie do puli", "Returned to the pool"),
                p.Reason
            ),
            TicketPriorityChanged c => new TimelineEntry(
                c.At,
                TimelineKind.PriorityChanged,
                c.Agent.FullName,
                T(
                    $"Zmiana priorytetu: {PriorityNames.Name(c.From)} → {PriorityNames.Name(c.To)}",
                    $"Priority changed: {N(PriorityNames.Name(c.From))} → {N(PriorityNames.Name(c.To))}"
                ),
                c.Reason
            ),
            TicketResolved d => new TimelineEntry(
                d.At,
                TimelineKind.Resolved,
                d.Agent.FullName,
                d.Late
                    ? T("Zrealizowane po terminie", "Resolved late")
                    : T("Zrealizowane w terminie", "Resolved on time"),
                null
            ),
            TicketCommented m => new TimelineEntry(
                m.At,
                TimelineKind.Commented,
                m.Agent.FullName,
                T("Komentarz", "Comment"),
                m.Text
            ),
            TicketAttachmentAdded a => new TimelineEntry(
                a.At,
                TimelineKind.AttachmentAdded,
                a.AddedBy,
                T($"Załącznik: {a.FileName}", $"Attachment: {a.FileName}"),
                null
            ),
            TicketHandlerAssigned h => new TimelineEntry(
                h.At,
                TimelineKind.HandlerAssigned,
                h.Agent.FullName,
                h.Person is { } person
                    ? T($"Do realizacji: {person.FullName}", $"Person doing the work: {person.FullName}")
                    : T("Bez osoby realizującej", "No person doing the work"),
                h.Reason
            ),
            TicketFieldsUpdated f => new TimelineEntry(
                f.At,
                TimelineKind.FieldsUpdated,
                f.Agent.FullName,
                T("Dane do raportu", "Report data"),
                string.Join("; ", f.Changes.Select(c => $"{N(c.Label)}: {Shown(c.From)} → {Shown(c.To)}"))
            ),
            _ => null,
        };

    /// <summary>A report field's value as the reader sees it: a choice in their language, nothing as "-".</summary>
    private static string Shown(string? value) => string.IsNullOrEmpty(value) ? "-" : N(value);
}
