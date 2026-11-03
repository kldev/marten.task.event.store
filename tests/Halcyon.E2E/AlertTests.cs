using Halcyon.Domain.Tickets;
using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Spec 004 in the browser: two different alerts, priority change, an alert that goes away.</summary>
public class AlertTests(E2EEnvironment env)
{
    private static ILocator Row(IPage page, string title) =>
        page.GetByTestId("ticket-row").Filter(new() { HasText = title });

    [Fact]
    public async Task OverdueTicketFromTheSeed_HasTheAlert_AndTheCounterShowsIt()
    {
        var page = await env.SignInAsync();

        var row = await E2EEnvironment.SearchAsync(page, "Przegląd umowy NDA z dostawcą");
        await Expect(row).ToHaveAttributeAsync("data-state", "overdue");
        await Expect(row.GetByTestId("time-left")).ToHaveAttributeAsync("data-state", "overdue");
        await Expect(row.GetByTestId("time-left")).ToContainTextAsync("po terminie o");
        var overdue = int.Parse((await page.GetByTestId("counter-overdue").GetAttributeAsync("data-count"))!);
        overdue.ShouldBeGreaterThan(0);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task DueSoon_IsAWarning_ThatLooksDifferentFromOverdue()
    {
        var title = E2EEnvironment.Unique("Serwer pocztowy");
        await env.ReportAsync(title, Priority.Critical, TimeSpan.FromMinutes(150));
        var page = await env.SignInAsync();

        var timeLeft = (await E2EEnvironment.SearchAsync(page, title)).GetByTestId("time-left");
        await Expect(timeLeft).ToHaveAttributeAsync("data-state", "duesoon");
        await Expect(timeLeft).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("time-duesoon"));
        await Expect(timeLeft).ToContainTextAsync(new System.Text.RegularExpressions.Regex(@"zostało \d+ min"));
        await Expect(Row(page, title)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("row-duesoon"));
        int.Parse((await page.GetByTestId("counter-duesoon").GetAttributeAsync("data-count"))!).ShouldBeGreaterThan(0);
        await E2EEnvironment.SearchAsync(page, "");

        var overdue = page.GetByTestId("ticket-row")
            .Filter(new() { Has = page.Locator("[data-state=overdue][data-testid=time-left]") })
            .First;
        await Expect(overdue.GetByTestId("time-left"))
            .ToHaveClassAsync(new System.Text.RegularExpressions.Regex("time-overdue"));
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task OnTime_ShowsHours_WithoutAWarning()
    {
        var title = E2EEnvironment.Unique("Nowe konto");
        await env.ReportAsync(title, Priority.High, TimeSpan.FromHours(1));
        var page = await env.SignInAsync();

        var timeLeft = (await E2EEnvironment.SearchAsync(page, title)).GetByTestId("time-left");
        await Expect(timeLeft).ToHaveAttributeAsync("data-state", "ontime");
        await Expect(timeLeft).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^\s*6 h \d+ min\s*$"));
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ResolvingAnOverdueTicket_RemovesTheAlert_AndMarksItLate()
    {
        var title = E2EEnvironment.Unique("Drukarka sieciowa");
        var (id, _) = await env.ReportAsync(title, Priority.Critical, TimeSpan.FromHours(4));
        await env.TakeAsync(id, "magdalena.kaminska@company.io");
        var page = await env.SignInAsync("magdalena.kaminska@company.io");
        await page.GotoAsync($"/zgloszenia/{id}");
        await Expect(page.GetByTestId("time-left")).ToHaveAttributeAsync("data-state", "overdue");
        var before = int.Parse((await page.GetByTestId("counter-overdue").GetAttributeAsync("data-count"))!);

        await page.GetByTestId("resolve").ClickAsync();

        await Expect(page.GetByTestId("time-left")).ToHaveAttributeAsync("data-state", "done-late");
        await Expect(page.GetByTestId("counter-overdue")).ToHaveAttributeAsync("data-count", (before - 1).ToString());
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task RaisingThePriority_OfAFourHourOldTicket_GivesTheOverdueAlert_AndATimelineEntry()
    {
        var title = E2EEnvironment.Unique("Prośba o monitor");
        var (id, _) = await env.ReportAsync(title, Priority.Low, TimeSpan.FromHours(4));
        var page = await env.SignInAsync();
        await page.GotoAsync($"/zgloszenia/{id}");
        await Expect(page.GetByTestId("time-left")).ToHaveAttributeAsync("data-state", "ontime");

        await page.GetByTestId("priority-select").SelectOptionAsync("Critical");
        await Expect(page.GetByTestId("priority-preview")).ToBeVisibleAsync();
        await page.GetByTestId("priority-reason").FillAsync("Prezentacja dla zarządu za godzinę.");
        await page.GetByTestId("priority-submit").ClickAsync();

        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("Niski → Krytyczny");
        await Expect(page.GetByTestId("time-left")).ToHaveAttributeAsync("data-state", "overdue");
        await Expect(
                page.GetByTestId("timeline-entry").Filter(new() { HasText = "Zmiana priorytetu: Niski → Krytyczny" })
            )
            .ToBeVisibleAsync();
        await page.Context.CloseAsync();
    }
}
