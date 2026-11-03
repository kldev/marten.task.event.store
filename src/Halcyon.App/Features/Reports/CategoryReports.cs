using System.Globalization;
using Halcyon.App.Features.Tickets;
using Halcyon.Database.Documents;
using Halcyon.Domain.Categories;
using Halcyon.Domain.Tickets;
using Halcyon.Domain.Time;
using Marten;

namespace Halcyon.App.Features.Reports;

/// <summary>A report of one category (spec 008): HR, finance, recruitment, marketing; the slug is its address under <c>/raporty</c>.</summary>
public sealed record CategoryReport(string Slug, string CategoryKey, string Title)
{
    public static IReadOnlyList<CategoryReport> All { get; } =
    [
        new("kadry-polska", "hr-poland", "Kadry Polska"),
        new("kadry-zagranica", "hr-abroad", "Kadry Zagranica"),
        new("finanse", "finance", "Finanse"),
        new("rekrutacja", "recruitment", "Rekrutacja"),
        new("marketing", "marketing", "Marketing"),
    ];

    public static CategoryReport? Find(string? slug) => All.FirstOrDefault(r => r.Slug == slug);

    public Category Category =>
        CategoryCatalog.Default.FindCategory(CategoryKey)
        ?? throw new InvalidOperationException($"Report {Slug}: no category {CategoryKey}.");
}

/// <summary>
/// The filters of a report, the same on the page and in the export links. Days are calendar days in Warsaw time,
/// both ends included.
/// </summary>
public sealed record ReportFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    string? Group = null,
    string? SubcategoryKey = null,
    CompletionFilter Completion = CompletionFilter.Any
)
{
    private const string Day = "yyyy-MM-dd";

    public string ToQueryString()
    {
        var parts = new List<string>();
        if (From is { } from)
            parts.Add("od=" + from.ToString(Day, CultureInfo.InvariantCulture));
        if (To is { } to)
            parts.Add("do=" + to.ToString(Day, CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(Group))
            parts.Add("rynek=" + Uri.EscapeDataString(Group));
        if (!string.IsNullOrEmpty(SubcategoryKey))
            parts.Add("podkategoria=" + Uri.EscapeDataString(SubcategoryKey));
        if (Completion != CompletionFilter.Any)
            parts.Add("stan=" + (Completion == CompletionFilter.Open ? "niezrealizowane" : "zrealizowane"));
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    /// <summary>Unreadable values are ignored rather than refused: a link edited by hand still gives a report.</summary>
    public static ReportFilter Parse(string? od, string? @do, string? rynek, string? podkategoria, string? stan) =>
        new(
            ParseDay(od),
            ParseDay(@do),
            string.IsNullOrWhiteSpace(rynek) ? null : rynek,
            string.IsNullOrWhiteSpace(podkategoria) ? null : podkategoria,
            stan switch
            {
                "niezrealizowane" => CompletionFilter.Open,
                "zrealizowane" => CompletionFilter.Done,
                _ => CompletionFilter.Any,
            }
        );

    private static DateOnly? ParseDay(string? value) =>
        DateOnly.TryParseExact(value, Day, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

public enum ReportColumnKind
{
    Text,
    Date,
    DateTime,
    Number,
}

/// <param name="Header">The full header, as the files have it.</param>
/// <param name="Group">On the page: a heading over neighbouring columns of the same group (a period).</param>
/// <param name="Short">On the page under <see cref="Group"/>, instead of the full header.</param>
public sealed record ReportColumn(
    string Key,
    string Header,
    ReportColumnKind Kind = ReportColumnKind.Text,
    string? Group = null,
    string? Short = null
);

/// <param name="Cells">One per column, "" when empty; dates as <c>yyyy-MM-dd</c>, times as <c>yyyy-MM-dd HH:mm</c>
/// in Warsaw time.</param>
/// <param name="TicketId">The ticket the row is about, when it is about one (the page links it).</param>
/// <param name="IsTotal">The sum row at the bottom: shown in bold and not counted.</param>
public sealed record ReportRow(Guid? TicketId, IReadOnlyList<string> Cells, bool IsTotal = false);

/// <summary>What every export and the page show: the same columns, the same rows.</summary>
/// <param name="Slug">Part of the export's file name.</param>
public sealed record ReportTable(
    string Slug,
    string Title,
    string FilterText,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<ReportRow> Rows,
    string RowNoun = "zgłoszeń"
)
{
    public int Count => Rows.Count(r => !r.IsTotal);
}

/// <summary>How a cell reads for a person (the page and the PDF); the spreadsheets get the raw values.</summary>
public static class ReportFormat
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static string Display(ReportColumn column, string value) =>
        column.Kind switch
        {
            ReportColumnKind.Date
                when DateOnly.TryParseExact(
                    value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var d
                ) => d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            ReportColumnKind.DateTime
                when DateTime.TryParseExact(
                    value,
                    CategoryReportQueries.DateTimeFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var t
                ) => t.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
            ReportColumnKind.Number
                when value.Contains('.', StringComparison.Ordinal)
                    && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) =>
                amount.ToString("N2", Polish),
            _ => value,
        };
}

/// <summary>
/// The category reports (spec 008): the tickets of one category with their report data as columns. Only the field columns
/// that have a value in at least one row are shown - a column nobody filled in is noise in a spreadsheet.
/// </summary>
public sealed class CategoryReportQueries(IDocumentStore store, TimeProvider time)
{
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    public async Task<ReportTable> RunAsync(CategoryReport report, ReportFilter filter, CancellationToken ct = default)
    {
        var category = report.Category;
        var scope = category
            .Subcategories.Where(s => filter.Group is null || s.Group == filter.Group)
            .Where(s => filter.SubcategoryKey is null || s.Key == filter.SubcategoryKey)
            .ToList();
        var keys = scope.Select(s => s.Key).ToArray();

        await using var session = store.QuerySession();
        IQueryable<TicketView> query = session.Query<TicketView>().Where(t => t.CategoryKey == category.Key);
        if (filter.Group is not null || filter.SubcategoryKey is not null)
            query = query.Where(t => keys.Contains(t.SubcategoryKey));
        if (filter.From is { } from)
        {
            var start = LocalTime.FromLocal(from.ToDateTime(TimeOnly.MinValue));
            query = query.Where(t => t.ReportedAt >= start);
        }
        if (filter.To is { } to)
        {
            var end = LocalTime.FromLocal(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(t => t.ReportedAt < end);
        }
        query = filter.Completion switch
        {
            CompletionFilter.Open => query.Where(t => t.Status != TicketStatus.Done),
            CompletionFilter.Done => query.Where(t => t.Status == TicketStatus.Done),
            _ => query,
        };

        var tickets = (await query.ToListAsync(ct)).OrderBy(t => t.ReportedAt).ThenBy(t => t.Number).ToList();

        var fields = scope
            .SelectMany(s => s.ReportFields)
            .DistinctBy(f => f.Key)
            .Where(f => tickets.Any(t => t.Fields.ContainsKey(f.Key)))
            .ToList();
        var showGroup = category.Groups.Count > 1 && filter.Group is null;

        var columns = new List<ReportColumn>
        {
            new("number", "Numer"),
            new("reported-at", "Data zgłoszenia", ReportColumnKind.DateTime),
        };
        if (showGroup)
            columns.Add(new("group", "Rynek"));
        columns.AddRange([
            new("subcategory", "Podkategoria"),
            new("title", "Tytuł"),
            new("requester", "Zgłaszający"),
            new("status", "Status"),
            new("priority", "Priorytet"),
            new("assignee", "Agent"),
            new("handler", "Osoba realizująca"),
            new("deadline", "Termin", ReportColumnKind.DateTime),
            new("resolved-at", "Zrealizowano", ReportColumnKind.DateTime),
        ]);
        columns.AddRange(
            fields.Select(f => new ReportColumn(
                "field:" + f.Key,
                f.Label,
                f.Kind switch
                {
                    FieldKind.Date => ReportColumnKind.Date,
                    FieldKind.Amount or FieldKind.Count => ReportColumnKind.Number,
                    _ => ReportColumnKind.Text,
                }
            ))
        );

        var rows = tickets
            .Select(t =>
            {
                var sub = category.Subcategories.FirstOrDefault(s => s.Key == t.SubcategoryKey);
                var cells = new List<string> { t.Number, Format(t.ReportedAt) };
                if (showGroup)
                    cells.Add(sub?.Group ?? "");
                cells.AddRange([
                    sub?.Name ?? t.SubcategoryKey,
                    t.Title,
                    t.RequesterName,
                    PriorityNames.Status(t.Status),
                    PriorityNames.Name(t.Priority),
                    t.AssigneeName ?? "",
                    t.HandlerName ?? "",
                    Format(t.Deadline),
                    t.ResolvedAt is { } resolved ? Format(resolved) : "",
                ]);
                cells.AddRange(fields.Select(f => t.Fields.GetValueOrDefault(f.Key) ?? ""));
                return new ReportRow(t.Id, cells);
            })
            .ToList();

        return new ReportTable(report.Slug, report.Title, Describe(category, filter), time.GetUtcNow(), columns, rows);
    }

    public static string Format(DateTimeOffset utc) =>
        LocalTime.ToLocal(utc).ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    private static string Describe(Category category, ReportFilter filter)
    {
        var parts = new List<string>();
        if (filter.From is not null || filter.To is not null)
            parts.Add(
                $"zgłoszone {(filter.From is { } f ? "od " + f.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : "")}"
                    + $"{(filter.From is not null && filter.To is not null ? " " : "")}"
                    + $"{(filter.To is { } t ? "do " + t.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : "")}"
            );
        if (filter.Group is { } group)
            parts.Add($"rynek: {group}");
        if (filter.SubcategoryKey is { } key && category.Subcategories.FirstOrDefault(s => s.Key == key) is { } sub)
            parts.Add($"podkategoria: {sub.DisplayName}");
        if (filter.Completion != CompletionFilter.Any)
            parts.Add(filter.Completion == CompletionFilter.Open ? "tylko niezrealizowane" : "tylko zrealizowane");
        return parts.Count == 0 ? "Wszystkie zgłoszenia" : string.Join(", ", parts);
    }
}
