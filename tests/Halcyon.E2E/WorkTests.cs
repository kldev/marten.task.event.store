using Halcyon.Domain.Tickets;
using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Spec 003 in the browser: take, resolve, hand over, return - and two agents at once.</summary>
public class WorkTests(E2EEnvironment env)
{
    private static ILocator Row(IPage page, string title) =>
        page.GetByTestId("ticket-row").Filter(new() { HasText = title });

    [Fact]
    public async Task Take_ThenResolve()
    {
        var title = E2EEnvironment.Unique("Mysz nie działa");
        await env.ReportAsync(title, Priority.Medium, TimeSpan.FromMinutes(10));
        var page = await env.SignInAsync("joanna.wozniak@company.io");

        await (await E2EEnvironment.SearchAsync(page, title)).GetByTestId("take").ClickAsync();
        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("Przejęto");
        await Expect(Row(page, title)).ToHaveCountAsync(0);

        await page.GetByTestId("nav-mine").ClickAsync();
        await (await E2EEnvironment.SearchAsync(page, title)).GetByTestId("resolve").ClickAsync();
        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("zrealizowane");
        await Expect(Row(page, title)).ToHaveCountAsync(0);

        await page.GetByTestId("nav-all").ClickAsync();
        await Expect((await E2EEnvironment.SearchAsync(page, title)).GetByTestId("time-left"))
            .ToHaveAttributeAsync("data-state", "done");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task TwoAgentsTakeTheSameTicket_TheSecondIsToldWhoWasFaster()
    {
        var title = E2EEnvironment.Unique("Klawiatura");
        await env.ReportAsync(title, Priority.High, TimeSpan.FromMinutes(5));
        var first = await env.SignInAsync("pawel.szymanski@company.io");
        var second = await env.SignInAsync("krzysztof.dabrowski@company.io");
        await E2EEnvironment.SearchAsync(first, title);
        await E2EEnvironment.SearchAsync(second, title);

        await Row(first, title).GetByTestId("take").ClickAsync();
        await Expect(first.GetByTestId("notice-success")).ToBeVisibleAsync();
        await Row(second, title).GetByTestId("take").ClickAsync();

        await Expect(second.GetByTestId("notice-error")).ToContainTextAsync("Paweł Szymański");
        await Expect(Row(second, title)).ToHaveCountAsync(0);
        await first.Context.CloseAsync();
        await second.Context.CloseAsync();
    }

    [Fact]
    public async Task HandOver_MovesTheTicketToTheOtherAgent_WithTheSameDeadline()
    {
        var title = E2EEnvironment.Unique("Toner");
        var (id, _) = await env.ReportAsync(title, Priority.Medium, TimeSpan.FromHours(1));
        await env.TakeAsync(id, "agnieszka.zielinska@company.io");
        var from = await env.SignInAsync("agnieszka.zielinska@company.io");
        await from.GotoAsync($"/zgloszenia/{id}");
        var deadline = await from.GetByTestId("ticket-deadline").TextContentAsync();
        await from.GotoAsync("/moje");

        await (await E2EEnvironment.SearchAsync(from, title)).GetByTestId("open-handover").ClickAsync();
        var michal = await env.AgentAsync("michal.lewandowski@company.io");
        await from.GetByTestId("handover-to").SelectOptionAsync(michal.Id.ToString());
        await from.GetByTestId("work-reason").FillAsync("Michał zna ten model drukarki.");
        await from.GetByTestId("work-confirm").ClickAsync();

        await Expect(from.GetByTestId("notice-success")).ToContainTextAsync("Michał Lewandowski");
        await Expect(Row(from, title)).ToHaveCountAsync(0);

        var to = await env.SignInAsync("michal.lewandowski@company.io");
        await to.GotoAsync("/moje");
        await E2EEnvironment.SearchAsync(to, title);
        await to.GotoAsync($"/zgloszenia/{id}");
        await Expect(to.GetByTestId("ticket-deadline")).ToHaveTextAsync(deadline!);
        await Expect(to.GetByTestId("timeline-entry").Filter(new() { HasText = "Przekazanie do: Michał Lewandowski" }))
            .ToBeVisibleAsync();
        await from.Context.CloseAsync();
        await to.Context.CloseAsync();
    }

    [Fact]
    public async Task ReturnToPool_PutsTheTicketBack_ForAnyone()
    {
        var title = E2EEnvironment.Unique("Licencja");
        var (id, _) = await env.ReportAsync(title, Priority.Low, TimeSpan.FromHours(2));
        await env.TakeAsync(id, "tomasz.wojcik@company.io");
        var page = await env.SignInAsync("tomasz.wojcik@company.io");
        await page.GotoAsync("/moje");

        await (await E2EEnvironment.SearchAsync(page, title)).GetByTestId("open-return").ClickAsync();
        await page.GetByTestId("work-reason").FillAsync("Urlop od jutra.");
        await page.GetByTestId("work-confirm").ClickAsync();

        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("wróciło do puli");
        await page.GetByTestId("nav-pool").ClickAsync();
        await Expect((await E2EEnvironment.SearchAsync(page, title)).GetByTestId("take")).ToBeVisibleAsync();
        await page.Context.CloseAsync();
    }
}
