using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Net.Http.Headers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Halcyon.App.Features.Reports;

public enum ExportFormat
{
    Csv,
    Xlsx,
    Pdf,
}

/// <summary>
/// The three exports of a report (spec 008), all from the same <see cref="ReportTable"/>: what the page shows is what
/// the file has.
/// </summary>
public static class ReportExport
{
    static ReportExport()
    {
        // QuestPDF's Community licence: free for companies below USD 1M annual revenue (the README says more).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string ContentType(ExportFormat format) =>
        format switch
        {
            ExportFormat.Csv => "text/csv; charset=utf-8",
            ExportFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/pdf",
        };

    public static string FileName(ReportTable table, ExportFormat format) =>
        $"raport-{table.Slug}-{CategoryReportQueries.Format(table.GeneratedAt)[..10]}.{format.ToString().ToLowerInvariant()}";

    public static byte[] Write(ReportTable table, ExportFormat format) =>
        format switch
        {
            ExportFormat.Csv => Csv(table),
            ExportFormat.Xlsx => Xlsx(table),
            _ => Pdf(table),
        };

    /// <summary>
    /// Semicolon-separated with a BOM and decimal commas, so a Polish Excel opens it in columns, with the right letters
    /// and with amounts as numbers. A cell starting
    /// with = + - @ (or a tab / CR) gets a leading apostrophe: a spreadsheet must not run a title as a formula.
    /// </summary>
    public static byte[] Csv(ReportTable table)
    {
        var text = new StringBuilder();
        text.AppendJoin(';', table.Columns.Select(c => CsvCell(c.Header))).Append("\r\n");
        foreach (var row in table.Rows)
            text.AppendJoin(
                    ';',
                    row.Cells.Select(
                        (cell, i) =>
                            // A Polish Excel reads "1234,56" as a number and "1234.56" as text.
                            table.Columns[i].Kind == ReportColumnKind.Number
                                ? cell.Replace('.', ',')
                                : CsvCell(cell)
                    )
                )
                .Append("\r\n");
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    internal static string CsvCell(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        return value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    /// <summary>A real table with a filter row; dates are dates, so the sheet sorts and filters them as such.</summary>
    public static byte[] Xlsx(ReportTable table)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(table.Title);

        for (var c = 0; c < table.Columns.Count; c++)
            sheet.Cell(1, c + 1).Value = table.Columns[c].Header;

        for (var r = 0; r < table.Rows.Count; r++)
        for (var c = 0; c < table.Columns.Count; c++)
        {
            var cell = sheet.Cell(r + 2, c + 1);
            var value = table.Rows[r].Cells[c];
            switch (table.Columns[c].Kind)
            {
                case ReportColumnKind.Date when TryParse(value, "yyyy-MM-dd", out var day):
                    cell.Value = day;
                    cell.Style.DateFormat.Format = "yyyy-mm-dd";
                    break;
                case ReportColumnKind.DateTime when TryParse(value, CategoryReportQueries.DateTimeFormat, out var at):
                    cell.Value = at;
                    cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                    break;
                case ReportColumnKind.Number
                    when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number):
                    cell.Value = number;
                    if (value.Contains('.', StringComparison.Ordinal))
                        cell.Style.NumberFormat.Format = "#,##0.00";
                    break;
                default:
                    // Always text: a value is never read as a formula or a number with its leading zeros cut.
                    cell.Value = value;
                    cell.Style.NumberFormat.Format = "@";
                    break;
            }
        }

        var range = sheet.Range(1, 1, Math.Max(table.Rows.Count + 1, 2), table.Columns.Count);
        range.CreateTable("Raport").ShowAutoFilter = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, Math.Min(table.Rows.Count + 1, 200), 8, 60);

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static bool TryParse(string value, string format, out DateTime result) =>
        DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);

    /// <summary>Landscape A4, or A3 when there are many columns; the header row repeats on every page.</summary>
    public static byte[] Pdf(ReportTable table)
    {
        var size = table.Columns.Count > 13 ? PageSizes.A3.Landscape() : PageSizes.A4.Landscape();
        return Document
            .Create(document =>
                document.Page(page =>
                {
                    page.Size(size);
                    page.Margin(24);
                    page.DefaultTextStyle(style => style.FontSize(7.5f).FontFamily("Lato"));

                    page.Header()
                        .PaddingBottom(10)
                        .Column(column =>
                        {
                            column.Item().Text($"Raport: {table.Title}").FontSize(14).SemiBold();
                            column
                                .Item()
                                .Text(
                                    $"{table.FilterText} · {table.Count} {table.RowNoun} · wygenerowano {CategoryReportQueries.Format(table.GeneratedAt)}"
                                )
                                .FontColor(Colors.Grey.Darken1);
                        });

                    page.Content()
                        .Table(t =>
                        {
                            t.ColumnsDefinition(columns =>
                            {
                                foreach (var column in table.Columns)
                                    if (column.Key == "title")
                                        columns.RelativeColumn(2);
                                    else
                                        columns.RelativeColumn();
                            });
                            t.Header(header =>
                            {
                                foreach (var column in table.Columns)
                                    header
                                        .Cell()
                                        .Background(Colors.Grey.Lighten3)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Medium)
                                        .Padding(3)
                                        .Element(cell =>
                                            column.Kind == ReportColumnKind.Number ? cell.AlignRight() : cell
                                        )
                                        .Text(column.Header)
                                        .SemiBold();
                            });
                            foreach (var row in table.Rows)
                                for (var c = 0; c < table.Columns.Count; c++)
                                {
                                    var column = table.Columns[c];
                                    var box = t.Cell()
                                        .Background(row.IsTotal ? Colors.Grey.Lighten4 : Colors.White)
                                        .BorderBottom(0.5f)
                                        .BorderColor(Colors.Grey.Lighten2)
                                        .Padding(3);
                                    if (column.Kind == ReportColumnKind.Number)
                                        box = box.AlignRight();
                                    var text = box.Text(ReportFormat.Display(column, row.Cells[c]));
                                    if (row.IsTotal)
                                        text.SemiBold();
                                }
                        });

                    page.Footer()
                        .AlignRight()
                        .Text(text =>
                        {
                            text.Span("Strona ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                })
            )
            .GeneratePdf();
    }
}

/// <summary>
/// <c>GET /raporty/{slug}/eksport.{csv|xlsx|pdf}</c> with the page's filters in the query - for signed-in agents only.
/// </summary>
public static class ReportExportEndpoints
{
    public static string UrlFor(CategoryReport report, ReportFilter filter, ExportFormat format) =>
        $"raporty/{report.Slug}/eksport.{format.ToString().ToLowerInvariant()}{filter.ToQueryString()}";

    public static string AgentsUrlFor(AgentPeriods periods, ExportFormat format) =>
        $"raporty/agenci/eksport.{format.ToString().ToLowerInvariant()}{AgentReportQueries.QueryFor(periods)}";

    public static IEndpointRouteBuilder MapReportExports(this IEndpointRouteBuilder app)
    {
        // The literal "agenci" wins over {slug}.
        app.MapGet("/raporty/agenci/eksport.{format}", ExportAgentsAsync).RequireAuthorization();
        app.MapGet("/raporty/{slug}/eksport.{format}", ExportAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> ExportAgentsAsync(
        string format,
        string? okresy,
        HttpContext http,
        AgentReportQueries reports,
        CancellationToken ct
    )
    {
        if (ParseFormat(format) is not { } kind)
            return Results.NotFound();
        var table = await reports.RunAsync(AgentReportQueries.ParsePeriods(okresy), ct);
        return File(http, table, kind);
    }

    private static ExportFormat? ParseFormat(string format) =>
        !int.TryParse(format, out _)
        && Enum.TryParse<ExportFormat>(format, ignoreCase: true, out var kind)
        && Enum.IsDefined(kind)
            ? kind
            : null;

    private static IResult File(HttpContext http, ReportTable table, ExportFormat kind)
    {
        var disposition = new ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(ReportExport.FileName(table, kind));
        http.Response.Headers.ContentDisposition = disposition.ToString();
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.CacheControl = "no-store";
        return Results.Bytes(ReportExport.Write(table, kind), ReportExport.ContentType(kind));
    }

    private static async Task<IResult> ExportAsync(
        string slug,
        string format,
        string? od,
        string? @do,
        string? rynek,
        string? podkategoria,
        string? stan,
        HttpContext http,
        CategoryReportQueries reports,
        CancellationToken ct
    )
    {
        if (CategoryReport.Find(slug) is not { } report || ParseFormat(format) is not { } kind)
            return Results.NotFound();

        var table = await reports.RunAsync(report, ReportFilter.Parse(od, @do, rynek, podkategoria, stan), ct);
        return File(http, table, kind);
    }
}
