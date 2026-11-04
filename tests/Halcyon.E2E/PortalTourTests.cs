using Halcyon.Domain.Tickets;
using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>
/// A walk through the whole portal for <c>docs/Portal.md</c>: one ticket's life from the pool to done, the conflict of
/// two agents, a handler from Kadry, the recruitment campaign, people, the reports and the phone. Screenshots go to
/// <c>screens/portal/</c> (and <c>docs/screens/portal/</c> with <c>HALCYON_SCREENS_TO_DOCS=1</c>). Every step asserts
/// what the screen should show, so the tour is also a test.
/// </summary>
public class PortalTourTests(E2EEnvironment env)
{
    private readonly Screens screens = new("portal");

    [Fact]
    public async Task ATicketsLife_FromThePool_ToDone()
    {
        var title = E2EEnvironment.Unique("Drukarka na 2. piętrze drukuje puste strony");
        var (id, number) = await env.ReportAsync(
            title,
            Priority.Medium,
            TimeSpan.FromHours(5),
            "it.hardware",
            "Od rana drukarka HP w pokoju 204 wysuwa puste kartki. Toner wymieniony w zeszłym tygodniu."
        );

        var anna = await env.SignInAsync("anna.nowak@company.io");
        await anna.SetViewportSizeAsync(1440, 900);

        // In the pool: anybody can take it.
        await anna.GotoAsync($"/zgloszenia/{id}");
        await Expect(anna.GetByTestId("take")).ToBeVisibleAsync();
        await screens.ShotAsync(anna, "01-ticket-in-pool");

        await anna.GetByTestId("take").ClickAsync();
        await Expect(anna.GetByTestId("notice-success")).ToContainTextAsync("Przejęto");
        await Expect(anna.GetByTestId("work-actions")).ToBeVisibleAsync();

        // A priority change: the new deadline is shown before saving, counted from the report date.
        await anna.GetByTestId("priority-select").SelectOptionAsync("High");
        await Expect(anna.GetByTestId("priority-preview")).ToContainTextAsync("Nowy termin");
        await anna.GetByTestId("priority-reason").FillAsync("Dział sprzedaży drukuje dziś umowy.");
        await screens.ShotAsync(anna, "02-priority-preview");
        await anna.GetByTestId("priority-submit").ClickAsync();
        await Expect(anna.GetByTestId("notice-success")).ToBeVisibleAsync();

        // Hand over with a reason, in a dialog.
        await anna.GetByTestId("open-handover").ClickAsync();
        await Expect(anna.GetByTestId("work-dialog")).ToBeVisibleAsync();
        var michal = await env.AgentAsync("michal.lewandowski@company.io");
        await anna.GetByTestId("handover-to").SelectOptionAsync(michal.Id.ToString());
        await anna.GetByTestId("work-reason").FillAsync("Michał zna ten model drukarki.");
        await screens.ShotAsync(anna, "03-handover-dialog", fullPage: false);
        await anna.GetByTestId("work-confirm").ClickAsync();
        await Expect(anna.GetByTestId("notice-success")).ToContainTextAsync("Michał Lewandowski");

        // Michał finds it under "Moje", comments and resolves it.
        var to = await env.SignInAsync("michal.lewandowski@company.io");
        await to.SetViewportSizeAsync(1440, 900);
        await to.GotoAsync("/moje");
        await E2EEnvironment.SearchAsync(to, title);
        await to.GetByTestId("filter-search").FillAsync("");
        await Expect(to.GetByTestId("ticket-row").First).ToBeVisibleAsync();
        await screens.ShotAsync(to, "04-my-tickets");

        await to.GotoAsync($"/zgloszenia/{id}");
        await to.GetByTestId("comment-input").FillAsync("Wymieniony bęben światłoczuły, wydruk testowy w porządku.");
        await to.GetByTestId("comment-submit").ClickAsync();
        await Expect(to.GetByTestId("comment")).ToHaveCountAsync(1);
        await to.GetByTestId("resolve").ClickAsync();
        await Expect(to.GetByTestId("notice-success")).ToContainTextAsync("zrealizowane");
        await Expect(to.GetByTestId("time-left")).ToHaveAttributeAsync("data-state", "done");
        await Expect(to.GetByTestId("timeline-entry")).ToHaveCountAsync(6);
        await screens.ShotAsync(to, "05-ticket-done-timeline");

        // The short address by number works too.
        await to.GotoAsync($"/zgloszenia/numer/{number}");
        await Expect(to.GetByTestId("ticket-number")).ToHaveTextAsync(number);
        await to.GotoAsync("/zgloszenia/numer/HAL-00000000");
        await Expect(to.GetByTestId("ticket-missing")).ToBeVisibleAsync();
        await screens.ShotAsync(to, "06-ticket-missing", fullPage: false);

        await anna.Context.CloseAsync();
        await to.Context.CloseAsync();
    }

    [Fact]
    public async Task TwoAgents_OneClick_TheSlowerOneIsToldWhoWasFaster()
    {
        var title = E2EEnvironment.Unique("Monitor miga po podłączeniu do stacji dokującej");
        await env.ReportAsync(title, Priority.High, TimeSpan.FromMinutes(20));
        var first = await env.SignInAsync("pawel.szymanski@company.io");
        var second = await env.SignInAsync("krzysztof.dabrowski@company.io");
        await second.SetViewportSizeAsync(1440, 900);
        var firstRow = await E2EEnvironment.SearchAsync(first, title);
        var secondRow = await E2EEnvironment.SearchAsync(second, title);

        await firstRow.GetByTestId("take").ClickAsync();
        await Expect(first.GetByTestId("notice-success")).ToBeVisibleAsync();
        await secondRow.GetByTestId("take").ClickAsync();
        await Expect(second.GetByTestId("notice-error")).ToContainTextAsync("Paweł Szymański");
        await screens.ShotAsync(second, "07-someone-was-faster", fullPage: false);

        await first.Context.CloseAsync();
        await second.Context.CloseAsync();
    }

    [Fact]
    public async Task HrTicket_AHandlerFromKadry_AndTheFieldsOnTheTicket()
    {
        var title = E2EEnvironment.Unique("Korekta listy płac za październik");
        var (id, _) = await env.ReportAsync(title, Priority.Medium, TimeSpan.FromHours(3), "hr-poland.pl.payroll");
        await env.TakeAsync(id, "anna.nowak@company.io");
        var page = await env.SignInAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await page.GotoAsync($"/zgloszenia/{id}");

        await page.GetByTestId("handler-select")
            .SelectOptionAsync(new SelectOptionValue { Label = "Halina Nowicka (Kadry)" });
        await page.GetByTestId("handler-reason").FillAsync("System płacowy nie pozwala mi zrobić korekty.");
        await page.GetByTestId("handler-submit").ClickAsync();
        await Expect(page.GetByTestId("ticket-handler")).ToHaveTextAsync("Halina Nowicka");
        await screens.ShotAsync(page, "08-hr-handler");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Recruitment_OrdersACampaign_FromMarketing()
    {
        var page = await env.SignInAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var position = E2EEnvironment.Unique("Spawacz MIG/MAG");
        await page.GotoAsync("/nowe");
        await page.GetByTestId("category-recruitment").ClickAsync();
        await page.GetByTestId("subcategory-recruitment.client").ClickAsync();
        await page.GetByTestId("field-client").FillAsync("Bau-Technik GmbH");
        await page.GetByTestId("field-position").FillAsync(position);
        await page.GetByTestId("field-headcount").FillAsync("6");
        await page.GetByTestId("title").FillAsync($"Rekrutacja: {position}");
        await page.GetByTestId("description")
            .FillAsync("6 osób do Dortmundu od stycznia, zakwaterowanie po stronie klienta.");
        await page.GetByTestId("priority-High").ClickAsync();
        await page.GetByTestId("requester-search").FillAsync("Kubiak");
        await page.GetByTestId("requester-option").First.ClickAsync();
        await page.GetByTestId("submit").ClickAsync();
        await Expect(page.GetByTestId("ticket-field-headcount")).ToHaveTextAsync("6");

        await page.GetByTestId("order-campaign").ClickAsync();
        await Expect(page.GetByTestId("title")).ToHaveValueAsync($"Kampania rekrutacyjna: {position}");
        await page.GetByTestId("field-channels-Facebook").ClickAsync();
        await page.GetByTestId("field-channels-OLX Praca").ClickAsync();
        await page.GetByTestId("field-amount").FillAsync("2 500");
        await page.GetByTestId("priority-Medium").ClickAsync();
        await screens.ShotAsync(page, "09-campaign-prefilled");
        await page.GetByTestId("submit").ClickAsync();
        await Expect(page.GetByTestId("ticket-field-amount")).ToContainTextAsync("2 500,00");

        await page.GetByTestId("related-ticket-link").ClickAsync();
        await Expect(page.GetByTestId("campaign")).ToContainTextAsync($"Kampania rekrutacyjna: {position}");
        await screens.ShotAsync(page, "10-recruitment-with-campaign");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task People_Reports_AndCharts()
    {
        var page = await env.SignInAsync();
        await page.SetViewportSizeAsync(1440, 900);

        await page.GotoAsync("/osoby");
        await Expect(page.GetByTestId("person-row").First).ToBeVisibleAsync();
        await screens.ShotAsync(page, "11-people");

        await page.GotoAsync("/raporty");
        await Expect(page.GetByTestId("report-link-agenci")).ToBeVisibleAsync();
        await Expect(page.GetByTestId("report-link-wykresy")).ToBeVisibleAsync();
        await screens.ShotAsync(page, "12-reports-index");

        await page.GotoAsync("/raporty/kadry-polska");
        await Expect(page.GetByTestId("report-table")).ToBeVisibleAsync();
        await screens.ShotAsync(page, "13-report-hr-poland");

        await page.GotoAsync("/raporty/finanse");
        await Expect(page.GetByTestId("report-table")).ToBeVisibleAsync();
        await screens.ShotAsync(page, "14-report-finance");

        await page.GotoAsync("/raporty/rekrutacja");
        await Expect(page.GetByTestId("report-table")).ToBeVisibleAsync();
        await screens.ShotAsync(page, "15-report-recruitment");

        await page.GotoAsync("/wykresy");
        await Expect(page.GetByTestId("key-figures")).ToBeVisibleAsync();
        await page.GetByTestId("filter-category").SelectOptionAsync("hr-abroad");
        await Expect(page.GetByTestId("chart-heatmap")).ToBeVisibleAsync();
        await screens.ShotAsync(page, "16-charts-hr-abroad");

        await page.GotoAsync("/wszystkie");
        await page.GetByTestId("count-total").ClickAsync();
        await Expect(page.GetByTestId("total")).ToBeVisibleAsync();
        await page.GetByTestId("page-next").ClickAsync();
        await Expect(page.GetByTestId("page-number")).ToContainTextAsync("Strona 2 z");
        await screens.ShotAsync(page, "17-all-tickets-paged");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task OnAPhone_ThePublicForm_AndATicket()
    {
        var anonymous = await env.AnonymousPageAsync();
        await anonymous.SetViewportSizeAsync(390, 844);
        await anonymous.GotoAsync("/zglos");
        await anonymous.GetByTestId("category-legalization").ClickAsync();
        await Expect(anonymous.GetByTestId("report-form")).ToBeVisibleAsync();
        await screens.ShotAsync(anonymous, "18-public-form-phone");

        var (id, _) = await env.ReportAsync(
            E2EEnvironment.Unique("Laptop nie łączy się z VPN"),
            Priority.Critical,
            TimeSpan.FromMinutes(170),
            "it.application",
            "Po aktualizacji Windows klient VPN zgłasza błąd certyfikatu. Pracuję zdalnie i nie mam dostępu do CRM."
        );
        var page = await env.SignInAsync();
        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync($"/zgloszenia/{id}");
        await Expect(page.GetByTestId("time-left")).ToHaveAttributeAsync("data-state", "duesoon");
        await screens.ShotAsync(page, "19-ticket-phone");

        await anonymous.Context.CloseAsync();
        await page.Context.CloseAsync();
    }
}
