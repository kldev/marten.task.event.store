using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>ADR-0011: the System menu - status with HTTP statistics, the log, the database and the queues.</summary>
public class SystemPagesTests(E2EEnvironment env)
{
    /// <summary>The writer moves the queue to the database every 2 s; refresh until the page sees it.</summary>
    private static async Task RefreshUntilAsync(IPage page, Func<Task<bool>> seen)
    {
        for (var i = 0; i < 20 && !await seen(); i++)
        {
            await page.WaitForTimeoutAsync(500);
            await page.GetByTestId("refresh").ClickAsync();
        }
        (await seen()).ShouldBeTrue();
    }

    [Fact]
    public async Task Status_CountsTheAnswers_AndListsA404()
    {
        var page = await env.SignInAsync();
        await page.GotoAsync("/nie-ma-takiej-strony-e2e");

        await page.GotoAsync("/");
        await page.GetByTestId("nav-system-status").ClickAsync();
        await Expect(page.GetByTestId("http-figures")).ToBeVisibleAsync();
        await Expect(page.GetByTestId("health-database")).ToContainTextAsync("Działa");

        await RefreshUntilAsync(page, async () => await page.GetByTestId("kpi-404-value").InnerTextAsync() != "0");
        (await page.GetByTestId("kpi-200-value").InnerTextAsync()).ShouldNotBe("0");
        await Expect(page.GetByTestId("http-chart-bucket")).ToHaveCountAsync(24);

        await page.GetByTestId("filter-outcome").SelectOptionAsync("NotFound");
        await Expect(page.GetByTestId("request-row").Filter(new() { HasText = "/nie-ma-takiej-strony-e2e" }))
            .ToBeVisibleAsync();

        await page.GetByTestId("status-range-14d").ClickAsync();
        await Expect(page.GetByTestId("http-chart-bucket")).ToHaveCountAsync(14);
        await Expect(page.GetByTestId("monitoring-settings")).ToContainTextAsync("Monitoring__Retention");
    }

    [Fact]
    public async Task Logs_ShowTheApplicationLog_AndFilterIt()
    {
        var page = await env.SignInAsync();
        await page.GetByTestId("nav-system-logs").ClickAsync();

        // The start of the app ("Halcyon <version> (commit ...)") is in the log.
        await page.GetByTestId("filter-search").FillAsync("commit");
        await page.GetByTestId("filter-search").PressAsync("Enter");
        await RefreshUntilAsync(page, async () => await page.GetByTestId("log-row").CountAsync() > 0);
        // The row itself, not the list's first one: the mails (spec 013) log while the filter is being applied.
        await Expect(page.GetByTestId("log-message").Filter(new() { HasText = "commit" }).First)
            .ToContainTextAsync("Halcyon");

        await page.GetByTestId("filter-level-error").ClickAsync();
        await Expect(page.GetByTestId("log-row").Filter(new() { HasText = "commit" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Database_And_Queues_ShowTheirState_AndTheCleanupRunsOnDemand()
    {
        var page = await env.SignInAsync();

        await page.GetByTestId("nav-system-database").ClickAsync();
        await Expect(page.GetByTestId("db-version")).ToContainTextAsync("PostgreSQL");
        await Expect(page.Locator("[data-testid=db-table][data-name='halcyon.http_requests']")).ToBeVisibleAsync();
        await Expect(page.Locator("[data-testid=db-table][data-name='events.mt_events']")).ToBeVisibleAsync();

        await page.GetByTestId("nav-system-queues").ClickAsync();
        await Expect(page.Locator("[data-testid=queue-row][data-name='http-requests']")).ToBeVisibleAsync();
        await Expect(page.Locator("[data-testid=queue-row][data-name='app-logs']")).ToBeVisibleAsync();

        var cleanup = page.Locator("[data-testid=job-row][data-name='monitoring-cleanup']");
        await cleanup.GetByTestId("job-run").ClickAsync();
        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("Usunięto");
        await Expect(cleanup.GetByTestId("job-result")).ToContainTextAsync("Usunięto");
    }

    [Fact]
    public async Task SignedOut_GoesToSignIn()
    {
        var page = await env.AnonymousPageAsync();
        await page.GotoAsync("/system/status");
        await Expect(page.GetByTestId("signin-form")).ToBeVisibleAsync();
    }
}
