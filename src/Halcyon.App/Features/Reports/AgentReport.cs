using System.Globalization;
using Halcyon.Database.Documents;
using Halcyon.Domain.Tickets;
using Halcyon.Domain.Time;
using Marten;

namespace Halcyon.App.Features.Reports;

public enum AgentPeriods
{
    Current,
    Previous,
}

/// <summary>
/// How the agents work (spec 008): per agent and calendar period, how many tickets they took from the pool, how many
/// they resolved and how many of those after the deadline - counted from the events, so a ticket handed over counts for
/// whoever really took or resolved it. Plus what each one has in progress right now.
/// </summary>
public sealed class AgentReportQueries(IDocumentStore store, TimeProvider time)
{
    public const string Slug = "agenci";

    public static string QueryFor(AgentPeriods periods) => periods == AgentPeriods.Previous ? "?okresy=poprzednie" : "";

    public static AgentPeriods ParsePeriods(string? value) =>
        value == "poprzednie" ? AgentPeriods.Previous : AgentPeriods.Current;

    public async Task<ReportTable> RunAsync(AgentPeriods which, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var periods = ReportPeriods.For(ReportPeriods.Today(now), which == AgentPeriods.Previous);
        var ranges = new[]
        {
            ("Tydzień", periods.Week),
            ("Miesiąc", periods.Month),
            ("3 miesiące", periods.ThreeMonths),
        };
        // The widest range holds the other two.
        var from = ranges.Min(r => r.Item2.From);
        var until = ranges.Max(r => r.Item2.Until);

        await using var session = store.QuerySession();
        var agents = (await session.Query<Agent>().ToListAsync(ct))
            .OrderBy(a => a.LastName, StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), false))
            .ThenBy(a => a.FirstName)
            .ToList();
        var taken = await session
            .Events.QueryRawEventDataOnly<TicketTaken>()
            .Where(e => e.At >= from && e.At < until)
            .ToListAsync(ct);
        var resolved = await session
            .Events.QueryRawEventDataOnly<TicketResolved>()
            .Where(e => e.At >= from && e.At < until)
            .ToListAsync(ct);
        var inProgress = (
            await session.Query<TicketView>().Where(t => t.Status == TicketStatus.InProgress).ToListAsync(ct)
        )
            .Where(t => t.AssigneeId is not null)
            .GroupBy(t => t.AssigneeId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var columns = new List<ReportColumn> { new("agent", "Agent") };
        foreach (var (name, range) in ranges)
        {
            var label = $"{name} {range.Text}";
            columns.Add(new($"taken:{name}", $"{label}: przejęte", ReportColumnKind.Number, label, "Przejęte"));
            columns.Add(
                new($"resolved:{name}", $"{label}: zrealizowane", ReportColumnKind.Number, label, "Zrealizowane")
            );
            columns.Add(new($"late:{name}", $"{label}: po terminie", ReportColumnKind.Number, label, "Po terminie"));
        }
        columns.Add(new("in-progress", "Teraz w realizacji", ReportColumnKind.Number));

        string N(int value) => value.ToString(CultureInfo.InvariantCulture);

        var rows = new List<ReportRow>();
        foreach (var agent in agents)
        {
            var cells = new List<string> { $"{agent.FirstName} {agent.LastName}" };
            foreach (var (_, range) in ranges)
            {
                cells.Add(N(taken.Count(e => e.Agent.Id == agent.Id && range.Contains(e.At))));
                cells.Add(N(resolved.Count(e => e.Agent.Id == agent.Id && range.Contains(e.At))));
                cells.Add(N(resolved.Count(e => e.Agent.Id == agent.Id && e.Late && range.Contains(e.At))));
            }
            cells.Add(N(inProgress.GetValueOrDefault(agent.Id)));
            rows.Add(new ReportRow(null, cells));
        }

        var totals = new List<string> { "Razem" };
        for (var c = 1; c < columns.Count; c++)
            totals.Add(N(rows.Sum(r => int.Parse(r.Cells[c], CultureInfo.InvariantCulture))));
        rows.Add(new ReportRow(null, totals, IsTotal: true));

        var text =
            which == AgentPeriods.Previous
                ? "Poprzednie pełne okresy kalendarzowe"
                : "Bieżące okresy kalendarzowe (tydzień pon-niedz, miesiąc, 3 miesiące)";
        return new ReportTable(Slug, "Praca agentów", text, now, columns, rows, "agentów");
    }
}
