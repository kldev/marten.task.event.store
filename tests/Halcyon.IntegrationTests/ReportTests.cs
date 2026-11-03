using System.Net;
using System.Text;
using ClosedXML.Excel;
using Halcyon.App.Features.Reports;
using Halcyon.App.Features.Tickets;
using Halcyon.Domain;
using Halcyon.Domain.Tickets;
using Halcyon.IntegrationTests.Infrastructure;
using Marten;

namespace Halcyon.IntegrationTests;

/// <summary>Spec 008: report fields on tickets, the HR reports, their exports and the agents' report.</summary>
public class ReportTests(PostgresContainer postgres) : TicketTestBase(postgres)
{
    private static readonly CategoryReport Abroad = CategoryReport.Find("kadry-zagranica")!;
    private static readonly CategoryReport Poland = CategoryReport.Find("kadry-polska")!;

    private async Task<Guid> ReportWithFieldsAsync(
        string subcategory,
        Dictionary<string, string?> fields,
        TimeSpan? ago = null,
        string title = "Zgłoszenie kadrowe"
    )
    {
        var command = Command(reportedAt: Now - (ago ?? TimeSpan.Zero), subcategory: subcategory, title: title) with
        {
            Fields = fields,
        };
        return (await App.Get<ReportTicket>().ByAgentAsync(Anna, command)).TicketId;
    }

    private Task<ReportTable> RunAsync(CategoryReport report, ReportFilter? filter = null) =>
        App.Get<CategoryReportQueries>().RunAsync(report, filter ?? new ReportFilter());

    private static string Cell(ReportTable table, ReportRow row, string key) =>
        row.Cells[table.Columns.ToList().FindIndex(c => c.Key == key)];

    [Fact]
    public async Task FieldsGivenAtTheReport_AreOnTheView_AndUpdatesFollow()
    {
        var id = await ReportWithFieldsAsync(
            "hr-abroad.be.posting",
            new() { ["employee"] = "Marcin Lis", ["site"] = " Antwerpia " }
        );
        (await ViewAsync(id)).Fields.ShouldBe(
            new Dictionary<string, string> { ["employee"] = "Marcin Lis", ["site"] = "Antwerpia" },
            ignoreOrder: true
        );

        var updated = await App.Get<UpdateTicketFields>()
            .ExecuteAsync(
                id,
                Piotr,
                new Dictionary<string, string?> { ["site"] = "Antwerpia", ["limosa-number"] = "L1-9" }
            );

        updated!.Changes.Select(c => c.Key).ShouldBe(["employee", "limosa-number"]);
        (await ViewAsync(id)).Fields.ShouldBe(
            new Dictionary<string, string> { ["site"] = "Antwerpia", ["limosa-number"] = "L1-9" },
            ignoreOrder: true
        );
        var timeline = (await App.Get<TicketQueries>().DetailsAsync(id))!.Timeline;
        var entry = timeline.Single(e => e.Kind == TimelineKind.FieldsUpdated);
        entry.Actor.ShouldBe(Piotr.FullName);
        entry.Note.ShouldBe("Pracownik (imię i nazwisko): Marcin Lis → -; Numer zgłoszenia Limosa-1: - → L1-9");
    }

    [Fact]
    public async Task Update_WithoutChanges_AddsNoEvent()
    {
        var id = await ReportWithFieldsAsync("hr-abroad.be.posting", new() { ["site"] = "Antwerpia" });
        await using var session = App.Store.QuerySession();
        var before = (await session.Events.FetchStreamStateAsync(id))!.Version;

        (
            await App.Get<UpdateTicketFields>()
                .ExecuteAsync(id, Anna, new Dictionary<string, string?> { ["site"] = "Antwerpia" })
        ).ShouldBeNull();

        (await session.Events.FetchStreamStateAsync(id))!.Version.ShouldBe(before);
    }

    [Fact]
    public async Task ARefusedReport_StoresNothing()
    {
        var command = Command(subcategory: "hr-poland.pl.onboarding") with
        {
            Fields = new Dictionary<string, string?> { ["first-name"] = "Olena" },
        };

        await Should.ThrowAsync<DomainException>(() => App.Get<ReportTicket>().ByAgentAsync(Anna, command));

        await using var session = App.Store.QuerySession();
        (await session.Query<Database.Documents.TicketView>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AnEventStoredBeforeFieldsExisted_StillReads()
    {
        // A TicketReported as the previous version wrote it: no "fields" property at all.
        var id = Guid.CreateVersion7();
        var reported = Ticket.Report(id, TestDraft(), Now);
        var serializer = App.Store.Options.Serializer();
        var json = serializer.ToJson(reported);
        json.ShouldContain("\"fields\"", Case.Insensitive);
        var old = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        old.Remove(old.First(p => p.Key.Equals("fields", StringComparison.OrdinalIgnoreCase)).Key);

        var read = serializer.FromJson<TicketReported>(new MemoryStream(Encoding.UTF8.GetBytes(old.ToJsonString())));

        read.Fields.ShouldBeNull();
        read.Title.ShouldBe(reported.Title);
        Ticket.FromEvents([read]).Fields.ShouldBeEmpty();
    }

    private TicketDraft TestDraft() =>
        new(
            "Laptop",
            "Opis",
            "it",
            "it.hardware",
            Priority.Low,
            Now,
            Requester.For(Adam.ToRef()),
            ReportChannel.Agent,
            Anna
        );

    [Fact]
    public async Task HrReport_HasTheCategorysTickets_InReportOrder_WithFieldColumnsThatHaveData()
    {
        var later = await ReportWithFieldsAsync(
            "hr-abroad.de.posting",
            new() { ["anmeldung-number"] = "MUC-1" },
            title: "b"
        );
        var earlier = await ReportWithFieldsAsync(
            "hr-abroad.be.posting",
            new() { ["limosa-number"] = "L1-1" },
            TimeSpan.FromHours(3),
            "a"
        );
        await ReportAsync(subcategory: "it.hardware");
        await ReportWithFieldsAsync("hr-poland.pl.payroll", new() { ["settlement-month"] = "2026-10" });

        var table = await RunAsync(Abroad);

        table.Rows.Select(r => r.TicketId).ShouldBe([earlier, later]);
        var keys = table.Columns.Select(c => c.Key).ToList();
        keys.ShouldContain("group");
        keys.Where(k => k.StartsWith("field:", StringComparison.Ordinal))
            .ShouldBe(["field:limosa-number", "field:anmeldung-number"]);
        Cell(table, table.Rows[0], "group").ShouldBe("Belgia");
        Cell(table, table.Rows[0], "field:limosa-number").ShouldBe("L1-1");
        Cell(table, table.Rows[0], "field:anmeldung-number").ShouldBe("");
        Cell(table, table.Rows[0], "reported-at").ShouldBe("2026-11-03 08:00"); // 07:00 UTC in Warsaw (CET)
    }

    [Fact]
    public async Task HrReport_FiltersCombine()
    {
        var be = await ReportWithFieldsAsync("hr-abroad.be.posting", new() { ["site"] = "Antwerpia" });
        var beOld = await ReportWithFieldsAsync(
            "hr-abroad.be.payroll",
            new() { ["site"] = "Gandawa" },
            TimeSpan.FromDays(10)
        );
        var de = await ReportWithFieldsAsync("hr-abroad.de.posting", new() { ["site"] = "Monachium" });
        await App.Get<TakeTicket>().ExecuteAsync(be, Anna);
        await App.Get<ResolveTicket>().ExecuteAsync(be, Anna);

        (await RunAsync(Abroad, new ReportFilter(Group: "Belgia"))).Rows.Select(r => r.TicketId).ShouldBe([beOld, be]);
        (await RunAsync(Abroad, new ReportFilter(SubcategoryKey: "hr-abroad.de.posting")))
            .Rows.Select(r => r.TicketId)
            .ShouldBe([de]);
        (await RunAsync(Abroad, new ReportFilter(Completion: CompletionFilter.Done)))
            .Rows.Select(r => r.TicketId)
            .ShouldBe([be]);
        (await RunAsync(Abroad, new ReportFilter(Completion: CompletionFilter.Open)))
            .Rows.Select(r => r.TicketId)
            .ShouldBe([beOld, de]);
        (await RunAsync(Abroad, new ReportFilter(Group: "Belgia", Completion: CompletionFilter.Open)))
            .Rows.Select(r => r.TicketId)
            .ShouldBe([beOld]);

        var oneMarket = await RunAsync(Abroad, new ReportFilter(Group: "Belgia"));
        oneMarket.Columns.Select(c => c.Key).ShouldNotContain("group");
    }

    [Fact]
    public async Task HrReport_Days_AreWarsawDays_BothEndsIncluded()
    {
        // Now is 03.11 11:00 in Warsaw; 35 h earlier is 02.11 00:00, 35 h + 1 min earlier is 01.11 23:59.
        var firstMinute = await ReportWithFieldsAsync("hr-poland.pl.payroll", new(), TimeSpan.FromHours(35));
        var dayBefore = await ReportWithFieldsAsync(
            "hr-poland.pl.payroll",
            new(),
            TimeSpan.FromHours(35) + TimeSpan.FromMinutes(1)
        );

        var day = new DateOnly(2026, 11, 2);
        (await RunAsync(Poland, new ReportFilter(From: day, To: day)))
            .Rows.Select(r => r.TicketId)
            .ShouldBe([firstMinute]);
        (await RunAsync(Poland, new ReportFilter(To: day.AddDays(-1))))
            .Rows.Select(r => r.TicketId)
            .ShouldBe([dayBefore]);
    }

    [Fact]
    public void Filter_RoundTripsThroughTheExportLink()
    {
        var filter = new ReportFilter(
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            "Niemcy",
            null,
            CompletionFilter.Done
        );
        var query = System.Web.HttpUtility.ParseQueryString(filter.ToQueryString());

        ReportFilter
            .Parse(query["od"], query["do"], query["rynek"], query["podkategoria"], query["stan"])
            .ShouldBe(filter);
        ReportFilter.Parse("01.10.2026", null, " ", null, "x").ShouldBe(new ReportFilter());
    }

    [Fact]
    public async Task Csv_IsForAPolishExcel_AndNeverRunsAFormula()
    {
        await ReportWithFieldsAsync(
            "hr-poland.pl.payroll",
            new() { ["employee"] = "=HYPERLINK(\"x\")" },
            title: "Pasek; październik"
        );

        var bytes = ReportExport.Csv(await RunAsync(Poland));

        bytes.Take(3).ShouldBe(Encoding.UTF8.GetPreamble());
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n");
        lines[0].ShouldStartWith("Numer;Data zgłoszenia;Podkategoria;Tytuł;");
        lines[1].ShouldContain(";\"Pasek; październik\";");
        lines[1].ShouldContain(";\"'=HYPERLINK(\"\"x\"\")\"");
    }

    [Fact]
    public async Task Xlsx_IsATable_WithRealDates_AndTextAsText()
    {
        await ReportWithFieldsAsync(
            "hr-abroad.be.posting",
            new() { ["period-from"] = "2026-11-09", ["employee"] = "=1+1" }
        );
        var table = await RunAsync(Abroad);

        using var workbook = new XLWorkbook(new MemoryStream(ReportExport.Xlsx(table)));
        var sheet = workbook.Worksheet(1);
        sheet.Cell(1, 1).GetString().ShouldBe("Numer");
        var from = table.Columns.ToList().FindIndex(c => c.Key == "field:period-from") + 1;
        var employee = table.Columns.ToList().FindIndex(c => c.Key == "field:employee") + 1;
        sheet.Cell(2, from).GetDateTime().ShouldBe(new DateTime(2026, 11, 9));
        sheet.Cell(2, employee).HasFormula.ShouldBeFalse();
        sheet.Cell(2, employee).GetString().ShouldBe("=1+1");
        sheet.Tables.Count().ShouldBe(1);
    }

    [Fact]
    public async Task FinanceReport_HasAmountsAsNumbers_InEveryFormat()
    {
        await ReportWithFieldsAsync(
            "finance.invoices",
            new()
            {
                ["amount"] = "18 450,27",
                ["currency"] = "PLN",
                ["invoice-number"] = "FV/1",
            }
        );
        var table = await RunAsync(CategoryReport.Find("finanse")!);

        var amount = table.Columns.ToList().FindIndex(c => c.Key == "field:amount");
        table.Columns[amount].Kind.ShouldBe(ReportColumnKind.Number);
        table.Rows[0].Cells[amount].ShouldBe("18450.27");

        using var workbook = new XLWorkbook(new MemoryStream(ReportExport.Xlsx(table)));
        workbook.Worksheet(1).Cell(2, amount + 1).GetDouble().ShouldBe(18450.27);

        var csv = Encoding.UTF8.GetString(ReportExport.Csv(table)[3..]);
        csv.ShouldContain(";18450,27;");
    }

    [Fact]
    public async Task Recruitment_OrdersACampaign_AndSeesItLinked()
    {
        var recruitment = await ReportWithFieldsAsync(
            "recruitment.client",
            new()
            {
                ["client"] = "Bau-Technik GmbH",
                ["position"] = "Spawacz",
                ["headcount"] = "12",
            }
        );
        var view = await ViewAsync(recruitment);

        // The link the ticket page offers, read back the way the new-ticket page reads it.
        var url = ReportPrefill.CampaignUrlFor(view);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri("http://x/" + url).Query);
        var prefill = ReportPrefill.FromQuery(query.ToDictionary(q => q.Key, q => q.Value.ToString()))!;
        prefill.SubcategoryKey.ShouldBe("marketing.budget");
        prefill.Title.ShouldBe("Kampania rekrutacyjna: Spawacz");
        prefill.RequesterPersonId.ShouldBe(Adam.Id);
        prefill.Fields.ShouldBe(
            new Dictionary<string, string?>
            {
                ["campaign-goal"] = "Rekrutacja",
                ["related-ticket"] = view.Number,
                ["position"] = "Spawacz",
                ["headcount"] = "12",
            },
            ignoreOrder: true
        );

        var fields = new Dictionary<string, string?>(prefill.Fields)
        {
            ["channels"] = "TikTok, Facebook",
            ["amount"] = "4 500",
        };
        var campaign = await ReportWithFieldsAsync("marketing.budget", fields);
        await ReportWithFieldsAsync("marketing.budget", new() { ["channels"] = "LinkedIn", ["amount"] = "100" });

        var linked = await App.Get<TicketQueries>().LinkedCampaignsAsync(view.Number);
        linked.Select(t => t.Id).ShouldBe([campaign]);
        linked[0].Fields["channels"].ShouldBe("Facebook, TikTok");
        (await App.Get<TicketQueries>().FindByNumberAsync(view.Number.ToLowerInvariant())).ShouldBe(recruitment);
        (await App.Get<TicketQueries>().FindByNumberAsync("HAL-00000000")).ShouldBeNull();

        var report = await RunAsync(CategoryReport.Find("marketing")!);
        var headcount = report.Columns.Single(c => c.Key == "field:headcount");
        headcount.Kind.ShouldBe(ReportColumnKind.Number);
    }

    [Fact]
    public async Task TheAgent_NamesAHandlerFromKadry_ShownOnTheListTimelineAndReport()
    {
        var id = await ReportWithFieldsAsync("hr-poland.pl.leave", new() { ["employee"] = "Jan Nowak" });
        await App.Get<TakeTicket>().ExecuteAsync(id, Anna);
        var halina = await App.PersonAsync("halina.nowicka@company.io"); // Kadry
        var igor = await App.PersonAsync("igor.adamczyk@company.io"); // Logistyka
        var assign = App.Get<AssignTicketHandler>();

        (
            await Should.ThrowAsync<DomainException>(() => assign.ExecuteAsync(id, Anna, igor.Id, null))
        ).Message.ShouldContain("Kadry");
        await assign.ExecuteAsync(id, Anna, halina.Id, "System nie pozwala mi wprowadzić korekty.");

        var view = await ViewAsync(id);
        view.HandlerId.ShouldBe(halina.Id);
        view.HandlerName.ShouldBe("Halina Nowicka");
        view.AssigneeId.ShouldBe(Anna.Id);
        var entry = (await App.Get<TicketQueries>().DetailsAsync(id))!.Timeline.Single(e =>
            e.Kind == TimelineKind.HandlerAssigned
        );
        entry.Text.ShouldBe("Do realizacji: Halina Nowicka");
        entry.Note.ShouldBe("System nie pozwala mi wprowadzić korekty.");
        var report = await RunAsync(Poland);
        Cell(report, report.Rows.Single(), "handler").ShouldBe("Halina Nowicka");

        await assign.ExecuteAsync(id, Anna, null, null);
        (await ViewAsync(id)).HandlerName.ShouldBeNull();
    }

    [Fact]
    public async Task Pdf_IsAPdf_EvenForAnEmptyReport()
    {
        await ReportWithFieldsAsync("hr-poland.pl.payroll", new() { ["employee"] = "Zażółć Gęślą Jaźń" });

        foreach (var table in new[] { await RunAsync(Poland), await RunAsync(Abroad) })
            Encoding.ASCII.GetString(ReportExport.Pdf(table)[..5]).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task Export_NeedsSignIn()
    {
        using var client = App.Client();
        var response = await client.GetAsync(new Uri("/raporty/kadry-polska/eksport.xlsx", UriKind.Relative));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("/logowanie");
    }

    [Theory]
    [InlineData("kadry-polska", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("kadry-zagranica", "pdf", "application/pdf")]
    [InlineData("kadry-polska", "csv", "text/csv")]
    [InlineData("agenci", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("agenci", "pdf", "application/pdf")]
    public async Task Export_IsAFileToSave(string slug, string format, string type)
    {
        using var client = await SignedInClientAsync();
        var response = await client.GetAsync(
            new Uri($"/raporty/{slug}/eksport.{format}?stan=zrealizowane", UriKind.Relative)
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(type);
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition.FileNameStar.ShouldBe($"raport-{slug}-2026-11-03.{format}");
    }

    [Theory]
    [InlineData("/raporty/kadry-marsjanskie/eksport.xlsx")]
    [InlineData("/raporty/kadry-polska/eksport.docx")]
    [InlineData("/raporty/kadry-polska/eksport.1")]
    public async Task Export_OfAnUnknownReportOrFormat_IsNotFound(string url)
    {
        using var client = await SignedInClientAsync();
        (await client.GetAsync(new Uri(url, UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AgentsReport_CountsTakenResolvedAndLate_InCalendarPeriods()
    {
        // Now: Tuesday 03.11.2026. This week = 02.11-08.11, this month = November, 3 months = September-November.
        var thisWeek = await ReportAsync(Priority.Critical, TimeSpan.FromHours(5));
        await App.Get<TakeTicket>().ExecuteAsync(thisWeek, Anna);
        await App.Get<ResolveTicket>().ExecuteAsync(thisWeek, Anna); // 5 h after a 3 h deadline: late

        var open = await ReportAsync();
        await App.Get<TakeTicket>().ExecuteAsync(open, Anna);

        var handedOver = await ReportAsync();
        await App.Get<TakeTicket>().ExecuteAsync(handedOver, Anna);
        await App.Get<HandOverTicket>().ExecuteAsync(handedOver, Anna, Piotr.Id, null);
        await App.Get<ResolveTicket>().ExecuteAsync(handedOver, Piotr);

        var table = await App.Get<AgentReportQueries>().RunAsync(AgentPeriods.Current);

        string Value(string agent, string column) => Cell(table, table.Rows.Single(r => r.Cells[0] == agent), column);

        Value(Anna.FullName, "taken:Tydzień").ShouldBe("3");
        Value(Anna.FullName, "resolved:Tydzień").ShouldBe("1");
        Value(Anna.FullName, "late:Tydzień").ShouldBe("1");
        Value(Anna.FullName, "resolved:3 miesiące").ShouldBe("1");
        Value(Anna.FullName, "in-progress").ShouldBe("1");
        Value(Piotr.FullName, "taken:Tydzień").ShouldBe("0");
        Value(Piotr.FullName, "resolved:Miesiąc").ShouldBe("1");
        Value(Piotr.FullName, "late:Miesiąc").ShouldBe("0");
        Value("Razem", "resolved:Tydzień").ShouldBe("2");
        table.Columns.Single(c => c.Key == "taken:Tydzień").Header.ShouldStartWith("Tydzień 02.11–08.11.2026");

        var previous = await App.Get<AgentReportQueries>().RunAsync(AgentPeriods.Previous);
        Cell(previous, previous.Rows.Single(r => r.Cells[0] == "Razem"), "resolved:Tydzień").ShouldBe("0");
        previous.Columns.Single(c => c.Key == "taken:Miesiąc").Header.ShouldStartWith("Miesiąc 01.10–31.10.2026");
    }

    [Fact]
    public async Task AllTickets_CanShowOnlyOpen_OrOnlyResolved()
    {
        var open = await ReportAsync();
        var done = await ReportAsync();
        await App.Get<TakeTicket>().ExecuteAsync(done, Anna);
        await App.Get<ResolveTicket>().ExecuteAsync(done, Anna);
        var queries = App.Get<TicketQueries>();

        (await queries.ListAsync(new TicketFilter(TicketScope.All, Completion: CompletionFilter.Open)))
            .Select(t => t.Id)
            .ShouldBe([open]);
        (await queries.ListAsync(new TicketFilter(TicketScope.All, Completion: CompletionFilter.Done)))
            .Select(t => t.Id)
            .ShouldBe([done]);
        (await queries.ListAsync(new TicketFilter(TicketScope.All))).Count.ShouldBe(2);
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = App.Client();
        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["email"] = "anna.nowak@company.io", ["password"] = TestApp.Password }
        );
        var login = await client.PostAsync(new Uri("/auth/login", UriKind.Relative), form);
        var cookie = login
            .Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("halcyon.session=", StringComparison.Ordinal));
        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        return client;
    }
}
