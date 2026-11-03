using Halcyon.Domain.Tickets;
using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Spec 009: the charts page in the browser.</summary>
public class ChartsTests(E2EEnvironment env)
{
    private static readonly string[] Cards =
    [
        "key-figures",
        "chart-flow",
        "chart-backlog",
        "chart-on-time",
        "chart-sla",
        "chart-queue",
        "chart-categories",
        "chart-agents",
        "chart-heatmap",
    ];

    [Fact]
    public async Task EveryChartRenders_AndTheRangeAndTheCategoryChangeThem()
    {
        await env.ReportAsync(
            "Umowa z dostawcą do przejrzenia",
            Priority.High,
            TimeSpan.FromHours(1),
            "legal.contracts"
        );
        var page = await env.SignInAsync();

        await page.GetByTestId("nav-charts").ClickAsync();
        foreach (var card in Cards)
            await Expect(page.GetByTestId(card)).ToBeVisibleAsync();
        await Expect(page.GetByTestId("flow-chart-bucket")).ToHaveCountAsync(30);
        await Expect(page.GetByTestId("heat-cell")).ToHaveCountAsync(7 * 24);

        await page.GetByTestId("filter-range-12w").ClickAsync();
        await Expect(page.GetByTestId("flow-chart-bucket")).ToHaveCountAsync(12);
        await Expect(page.GetByTestId("chart-flow")).ToContainTextAsync("na tydzień");

        await page.GetByTestId("filter-category").SelectOptionAsync("legal");
        await Expect(page.GetByTestId("chart-categories")).ToContainTextAsync("Według podkategorii");
        await Expect(page.GetByTestId("category-chart-row").Filter(new() { HasText = "Umowy" })).ToBeVisibleAsync();

        // The same numbers as a table.
        var table = page.GetByTestId("chart-queue-table");
        await table.Locator("summary").ClickAsync();
        await Expect(table.GetByTestId("chart-table")).ToContainTextAsync("Wysoki");
    }

    [Fact]
    public async Task SignedOut_GoesToSignIn()
    {
        var page = await env.AnonymousPageAsync();
        await page.GotoAsync("/wykresy");
        await Expect(page.GetByTestId("signin-form")).ToBeVisibleAsync();
    }
}
