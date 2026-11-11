using Halcyon.Domain.Tickets;
using Halcyon.Domain.Time;
using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>
/// Spec 012 / ADR-0013 in the browser: the app runs with Low = 5 days (<see cref="E2EEnvironment.LowSlaHours"/>), and
/// every place that names or computes an SLA uses it.
/// </summary>
public class SlaTests(E2EEnvironment env)
{
    [Fact]
    public async Task AgentForm_ShowsTheConfiguredWindows()
    {
        var page = await env.SignInAsync();
        await page.GotoAsync("/nowe");

        await Expect(page.GetByTestId("priority-Low")).ToContainTextAsync("5 dni");
        await Expect(page.GetByTestId("priority-Critical")).ToContainTextAsync("3 h");
        await Expect(page.GetByTestId("priority-Medium")).ToContainTextAsync("3 dni");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task PublicForm_ShowsTheConfiguredWindows()
    {
        var page = await env.AnonymousPageAsync();
        await page.GotoAsync("/zglos");

        await Expect(page.GetByTestId("priority-Low")).ToContainTextAsync("5 dni");
        await Expect(page.GetByTestId("priority-High")).ToContainTextAsync("8 h");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task LoweringThePriority_PreviewsAndStoresTheConfiguredDeadline_FromTheReportDate()
    {
        var title = E2EEnvironment.Unique("Nowa myszka");
        var (id, number) = await env.ReportAsync(title, Priority.High, TimeSpan.FromHours(1));
        var reportedAt = (await env.ViewAsync(id)).ReportedAt;
        var expected = LocalTime.Format(reportedAt + TimeSpan.FromHours(E2EEnvironment.LowSlaHours));

        var page = await env.SignInAsync();
        await page.GotoAsync($"/zgloszenia/{id}");
        await Expect(page.GetByTestId("ticket-number")).ToHaveTextAsync(number);
        await Expect(page.GetByTestId("priority-select").Locator("option[value=Low]")).ToHaveTextAsync("Niski (5 dni)");

        await page.GetByTestId("priority-select").SelectOptionAsync("Low");
        await Expect(page.GetByTestId("priority-preview")).ToContainTextAsync(expected);
        await page.GetByTestId("priority-submit").ClickAsync();

        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("Wysoki → Niski");
        await Expect(page.GetByTestId("ticket-deadline")).ToContainTextAsync(expected);
        (await env.ViewAsync(id)).Deadline.ShouldBe(reportedAt + TimeSpan.FromHours(E2EEnvironment.LowSlaHours));
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ChartsPage_NamesTheConfiguredWindow()
    {
        var page = await env.SignInAsync();
        await page.GotoAsync("/wykresy");

        await Expect(page.GetByTestId("sla-row").Filter(new() { HasText = "Niski" })).ToContainTextAsync("SLA 5 dni");
        await page.Context.CloseAsync();
    }
}
