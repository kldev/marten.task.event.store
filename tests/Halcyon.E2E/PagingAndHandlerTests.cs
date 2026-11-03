using Halcyon.Domain.Tickets;
using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Spec 008 in the browser: paged lists with a total on request; a handler from Kadry named by the agent.</summary>
public class PagingAndHandlerTests(E2EEnvironment env)
{
    [Fact]
    public async Task AllTickets_ArePaged_AndTheTotalIsCountedOnRequest()
    {
        var page = await env.SignInAsync();
        await page.GotoAsync("/wszystkie");
        await Expect(page.GetByTestId("ticket-row")).ToHaveCountAsync(25);
        await Expect(page.GetByTestId("page-prev")).ToBeDisabledAsync();
        await Expect(page.GetByTestId("total")).ToHaveCountAsync(0);
        var first = await page.GetByTestId("ticket-row").First.GetAttributeAsync("data-ticket");

        await page.GetByTestId("page-next").ClickAsync();
        await Expect(page.GetByTestId("page-number")).ToHaveTextAsync("Strona 2");
        await Expect(page.GetByTestId("ticket-row").First).Not.ToHaveAttributeAsync("data-ticket", first!);

        await page.GetByTestId("count-total").ClickAsync();
        var total = int.Parse(await page.GetByTestId("total").Locator("strong").InnerTextAsync());
        total.ShouldBeGreaterThan(200);
        await Expect(page.GetByTestId("page-number")).ToContainTextAsync($"z {(total + 24) / 25}");

        // A new filter starts from page 1 and forgets the total.
        await page.GetByTestId("filter-completion-done").ClickAsync();
        await Expect(page.GetByTestId("page-number")).ToHaveTextAsync("Strona 1");
        await Expect(page.GetByTestId("count-total")).ToBeVisibleAsync();
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task TheAgent_TakesAnHrTicket_AndNamesSomebodyFromKadry()
    {
        var title = E2EEnvironment.Unique("Korekta listy płac");
        var (id, _) = await env.ReportAsync(title, Priority.Medium, TimeSpan.FromHours(1), "hr-poland.pl.payroll");
        await env.TakeAsync(id, "anna.nowak@company.io");
        var page = await env.SignInAsync();
        await page.GotoAsync($"/zgloszenia/{id}");

        // Only Kadry is offered for an HR ticket.
        var options = await page.GetByTestId("handler-select").Locator("option").AllInnerTextsAsync();
        options.Skip(1).ShouldAllBe(o => o.EndsWith("(Kadry)"));
        await page.GetByTestId("handler-select")
            .SelectOptionAsync(new SelectOptionValue { Label = "Halina Nowicka (Kadry)" });
        await page.GetByTestId("handler-reason").FillAsync("System płacowy nie pozwala mi zrobić korekty.");
        await page.GetByTestId("handler-submit").ClickAsync();

        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("Do realizacji: Halina Nowicka");
        await Expect(page.GetByTestId("ticket-handler")).ToHaveTextAsync("Halina Nowicka");
        await Expect(page.GetByTestId("ticket-assignee")).ToHaveTextAsync("Anna Nowak");
        await Expect(page.GetByTestId("timeline-entry").Filter(new() { HasText = "Do realizacji: Halina Nowicka" }))
            .ToContainTextAsync("System płacowy nie pozwala");

        await page.GotoAsync("/moje");
        await Expect((await E2EEnvironment.SearchAsync(page, title)).GetByTestId("ticket-link")).ToBeVisibleAsync();
        await page.Context.CloseAsync();
    }
}
