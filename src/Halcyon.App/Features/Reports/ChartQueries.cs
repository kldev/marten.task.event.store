using System.Globalization;
using Halcyon.Database.Documents;
using Halcyon.Domain.Categories;
using Halcyon.Domain.Tickets;
using Halcyon.Domain.Time;
using Marten;
using static Halcyon.App.Localization.Tr;

namespace Halcyon.App.Features.Reports;

/// <summary>What the charts page shows (spec 009): a range and, optionally, one category.</summary>
public sealed record ChartFilter(ChartRange Range = ChartRange.Days30, string? CategoryKey = null);

/// <param name="OnTimeShare">Resolved on time / resolved, null when nothing was resolved.</param>
public sealed record KeyFigures(
    int Reported,
    int Resolved,
    int ResolvedLate,
    double? OnTimeShare,
    TimeSpan? MedianResolution,
    int OpenNow,
    int DueSoonNow,
    int OverdueNow
);

/// <param name="OpenAtEnd">Open at the end of the bucket; for the bucket holding now, open now.</param>
public sealed record FlowPoint(ChartBucket Bucket, int Reported, int Resolved, int ResolvedLate, int OpenAtEnd)
{
    public int ResolvedOnTime => Resolved - ResolvedLate;
}

/// <param name="Median">Time to resolve as a share of the ticket's SLA (1 = the whole SLA).</param>
/// <param name="P90">Null with fewer than <see cref="ChartQueries.MinForP90"/> tickets.</param>
public sealed record SlaUse(Priority Priority, int Count, double? Median, double? P90, TimeSpan? MedianTime);

/// <summary>A row of a stacked bar: one value per segment, in the order of the chart's segments.</summary>
public sealed record Breakdown(string Key, string Label, IReadOnlyList<int> Values)
{
    public int Total => Values.Sum();
}

public sealed record ChartData(
    ChartFilter Filter,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ChartBucket> Buckets,
    KeyFigures Figures,
    IReadOnlyList<FlowPoint> Flow,
    IReadOnlyList<SlaUse> Sla,
    bool BySubcategory,
    IReadOnlyList<Breakdown> ByCategory,
    IReadOnlyList<Breakdown> QueueByPriority,
    IReadOnlyList<Breakdown> Agents,
    int[][] Heat
)
{
    public string RangeText => $"{Buckets[0].Days.Start:dd.MM.yyyy} – {Buckets[^1].Days.End:dd.MM.yyyy}";
    public int HeatMax => Heat.Max(row => row.Max());
}

/// <summary>
/// The numbers behind the charts (spec 009, ADR-0010). One query loads every ticket that was open at some moment of the
/// range or was reported in it; everything else is counted in memory - fine for a team desk, see the ADR for when not.
/// </summary>
public sealed class ChartQueries(IDocumentStore store, TimeProvider time)
{
    public const int MinForP90 = 5;
    public const int TopAgents = 8;

    public async Task<ChartData> RunAsync(ChartFilter filter, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var buckets = ChartBuckets.For(filter.Range, ReportPeriods.Today(now), Localization.Language.Culture);
        var from = buckets[0].From;
        var until = buckets[^1].Until;

        await using var session = store.QuerySession();
        IQueryable<TicketView> query = session
            .Query<TicketView>()
            .Where(t => t.ReportedAt < until && (t.ResolvedAt == null || t.ResolvedAt >= from));
        if (filter.CategoryKey is { } key)
            query = query.Where(t => t.CategoryKey == key);
        var tickets = await query.ToListAsync(ct);

        return Build(filter, now, buckets, tickets);
    }

    public static ChartData Build(
        ChartFilter filter,
        DateTimeOffset now,
        IReadOnlyList<ChartBucket> buckets,
        IReadOnlyList<TicketView> tickets
    )
    {
        var from = buckets[0].From;
        var until = buckets[^1].Until;
        bool InRange(DateTimeOffset at) => at >= from && at < until;

        var reported = tickets.Where(t => InRange(t.ReportedAt)).ToList();
        var resolved = tickets.Where(t => t.ResolvedAt is { } at && InRange(at)).ToList();
        var open = tickets.Where(t => t.Status != TicketStatus.Done).ToList();

        var flow = buckets
            .Select(b =>
            {
                // A finished bucket: open at its end. The bucket holding now: open now, so the last point is the
                // same number as the key figure (a ticket resolved this instant is no longer open).
                var openAtEnd =
                    b.Until <= now
                        ? tickets.Count(t =>
                            t.ReportedAt < b.Until && (t.ResolvedAt is null || t.ResolvedAt >= b.Until)
                        )
                        : tickets.Count(t => t.ReportedAt <= now && t.Status != TicketStatus.Done);
                return new FlowPoint(
                    b,
                    reported.Count(t => b.Contains(t.ReportedAt)),
                    resolved.Count(t => b.Contains(t.ResolvedAt!.Value)),
                    resolved.Count(t => t.ResolvedLate && b.Contains(t.ResolvedAt!.Value)),
                    openAtEnd
                );
            })
            .ToList();

        var states = open.Select(t => (t.Priority, State: t.StateAt(now))).ToList();
        var figures = new KeyFigures(
            reported.Count,
            resolved.Count,
            resolved.Count(t => t.ResolvedLate),
            resolved.Count == 0 ? null : resolved.Count(t => !t.ResolvedLate) / (double)resolved.Count,
            resolved.Count == 0
                ? null
                : TimeSpan.FromTicks(
                    (long)Median(resolved.Select(t => (double)(t.ResolvedAt!.Value - t.ReportedAt).Ticks))!.Value
                ),
            open.Count,
            states.Count(s => s.State == DeadlineState.DueSoon),
            states.Count(s => s.State == DeadlineState.Overdue)
        );

        var sla = Enum.GetValues<Priority>()
            .Select(p =>
            {
                var of = resolved.Where(t => t.Priority == p).ToList();
                var shares = of.Select(t => (t.ResolvedAt!.Value - t.ReportedAt) / (t.Deadline - t.ReportedAt))
                    .ToList();
                var times = of.Select(t => (double)(t.ResolvedAt!.Value - t.ReportedAt).Ticks).ToList();
                return new SlaUse(
                    p,
                    of.Count,
                    Median(shares),
                    of.Count >= MinForP90 ? Percentile(shares, 0.9) : null,
                    Median(times) is { } ticks ? TimeSpan.FromTicks((long)ticks) : null
                );
            })
            .ToList();

        var bySubcategory = filter.CategoryKey is not null;
        var catalog = CategoryCatalog.Default;
        var byCategory = reported
            .GroupBy(t => bySubcategory ? t.SubcategoryKey : t.CategoryKey)
            .Select(g => new Breakdown(
                g.Key,
                bySubcategory ? catalog.FindSubcategory(g.Key)?.Subcategory.DisplayName ?? g.Key : ShortName(g.Key),
                [
                    g.Count(t => t.Status == TicketStatus.Done && !t.ResolvedLate),
                    g.Count(t => t.Status == TicketStatus.Done && t.ResolvedLate),
                    g.Count(t => t.Status != TicketStatus.Done),
                ]
            ))
            .OrderByDescending(b => b.Total)
            .ThenBy(b => b.Label, StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), false))
            .ToList();

        var queue = Enum.GetValues<Priority>()
            .Select(p => new Breakdown(
                p.ToString(),
                PriorityNames.Name(p),
                [
                    states.Count(s => s.Priority == p && s.State == DeadlineState.OnTime),
                    states.Count(s => s.Priority == p && s.State == DeadlineState.DueSoon),
                    states.Count(s => s.Priority == p && s.State == DeadlineState.Overdue),
                ]
            ))
            .ToList();

        var agents = resolved
            .GroupBy(t => (t.AssigneeId, Name: t.AssigneeName ?? "Nieznany"))
            .Select(g => new Breakdown(
                g.Key.AssigneeId?.ToString() ?? "",
                g.Key.Name,
                [g.Count(t => !t.ResolvedLate), g.Count(t => t.ResolvedLate)]
            ))
            .OrderByDescending(b => b.Total)
            .ThenBy(b => b.Label, StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), false))
            .ToList();
        if (agents.Count > TopAgents + 1)
        {
            var rest = agents.Skip(TopAgents).ToList();
            agents =
            [
                .. agents.Take(TopAgents),
                new Breakdown(
                    "rest",
                    $"Pozostali ({rest.Count})",
                    [rest.Sum(b => b.Values[0]), rest.Sum(b => b.Values[1])]
                ),
            ];
        }

        var heat = Enumerable.Range(0, 7).Select(_ => new int[24]).ToArray();
        foreach (var t in reported)
            heat[ChartBuckets.Weekday(t.ReportedAt)][ChartBuckets.Hour(t.ReportedAt)]++;

        return new ChartData(filter, now, buckets, figures, flow, sla, bySubcategory, byCategory, queue, agents, heat);
    }

    /// <summary>The category on a chart's axis: "IT" rather than "Zgłoszenie do IT".</summary>
    public static string ShortName(string categoryKey) =>
        categoryKey switch
        {
            "it" => "IT",
            "legal" => T("Dział Prawny", "Legal"),
            "marketing" => "Marketing",
            "recruitment" => N("Rekrutacja"),
            "finance" => N("Finanse"),
            _ => CategoryCatalog.Default.FindCategory(categoryKey) is { } category ? N(category.Name) : categoryKey,
        };

    public static double? Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0)
            return null;
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <summary>The nearest rank: the smallest value with at least <paramref name="p"/> of the values at or below it.</summary>
    public static double? Percentile(IEnumerable<double> values, double p)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0)
            return null;
        var rank = (int)Math.Ceiling(p * sorted.Count);
        return sorted[Math.Clamp(rank, 1, sorted.Count) - 1];
    }
}
