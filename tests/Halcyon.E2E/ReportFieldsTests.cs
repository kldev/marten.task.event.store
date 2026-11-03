using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Spec 008: a new employee with report data, completed on the ticket page, in the Kadry Polska report.</summary>
public class ReportFieldsTests(E2EEnvironment env)
{
    [Fact]
    public async Task NewEmployee_IsReportedWithItsData_CompletedLater_AndIsInTheReport_WithExports()
    {
        var page = await env.SignInAsync();
        var title = E2EEnvironment.Unique("Nowy pracownik");
        var lastName = E2EEnvironment.Unique("Kovalenko").Replace(" ", "-", StringComparison.Ordinal);
        await page.GotoAsync("/nowe");

        await page.GetByTestId("category-hr-poland").ClickAsync();
        await page.GetByTestId("subcategory-hr-poland.pl.onboarding").ClickAsync();
        var fields = page.GetByTestId("report-fields");
        await Expect(fields).ToBeVisibleAsync();
        // Europe only, the favourites first.
        await Expect(page.GetByTestId("field-country").Locator("optgroup[label='Najczęściej'] option"))
            .ToHaveTextAsync(["Polska", "Czechy", "Niemcy", "Belgia", "Ukraina", "Rumunia"]);

        await page.GetByTestId("field-first-name").FillAsync("Olena");
        await page.GetByTestId("field-last-name").FillAsync(lastName);
        await page.GetByTestId("field-email").FillAsync("olena.k@company.io");
        await page.GetByTestId("field-department").SelectOptionAsync("Logistyka");
        await page.GetByTestId("field-country").SelectOptionAsync("Ukraina");
        await page.GetByTestId("field-identity-document").SelectOptionAsync("PESEL");
        await page.GetByTestId("field-identity-number").FillAsync("92031501233"); // wrong check digit

        await page.GetByTestId("title").FillAsync(title);
        await page.GetByTestId("description").FillAsync("Nowa osoba w magazynie od poniedziałku.");
        await page.GetByTestId("priority-Medium").ClickAsync();
        await page.GetByTestId("requester-search").FillAsync("Adamczyk");
        await page.GetByTestId("requester-option").First.ClickAsync();
        await page.GetByTestId("submit").ClickAsync();
        await Expect(page.GetByTestId("notice-error")).ToContainTextAsync("PESEL");

        await page.GetByTestId("field-identity-number").FillAsync("92031501232");
        await page.GetByTestId("submit").ClickAsync();
        await Expect(page.GetByTestId("ticket-title")).ToHaveTextAsync(title);
        await Expect(page.GetByTestId("ticket-field-department")).ToHaveTextAsync("Logistyka");
        await Expect(page.GetByTestId("ticket-field-start-date")).ToHaveTextAsync("-");

        // Completed later on the ticket page, with the calendar.
        await page.GetByTestId("fields-edit").ClickAsync();
        await page.GetByTestId("field-start-date-field").ClickAsync();
        await page.GetByTestId("field-start-date-today").ClickAsync();
        await page.GetByTestId("fields-submit").ClickAsync();
        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("Zapisano dane do raportu");
        await Expect(page.GetByTestId("ticket-field-start-date")).Not.ToHaveTextAsync("-");
        await Expect(page.GetByTestId("timeline-entry").Filter(new() { HasText = "Dane do raportu" }))
            .ToBeVisibleAsync();

        await page.GetByTestId("nav-report-kadry-polska").ClickAsync();
        var row = page.GetByTestId("report-row").Filter(new() { HasText = lastName });
        await Expect(row).ToBeVisibleAsync();
        await Expect(row.Locator("[data-column='field:department']")).ToHaveTextAsync("Logistyka");

        // Only resolved: the new one is still open.
        await page.GetByTestId("filter-completion-done").ClickAsync();
        await Expect(row).ToHaveCountAsync(0);
        await page.GetByTestId("filter-completion-any").ClickAsync();
        await Expect(row).ToBeVisibleAsync();

        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByTestId("export-xlsx").ClickAsync());
        download.SuggestedFilename.ShouldStartWith("raport-kadry-polska-");
        download.SuggestedFilename.ShouldEndWith(".xlsx");
        var pdf = await page.RunAndWaitForDownloadAsync(() => page.GetByTestId("export-pdf").ClickAsync());
        pdf.SuggestedFilename.ShouldEndWith(".pdf");

        await page.GetByTestId("nav-report-agents").ClickAsync();
        await Expect(page.GetByTestId("report-table")).ToBeVisibleAsync();
        await Expect(page.GetByTestId("app-version")).ToContainTextAsync("v1.1.0");
        await page.Context.CloseAsync();
    }
}
