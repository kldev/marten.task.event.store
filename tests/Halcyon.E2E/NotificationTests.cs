using Halcyon.Domain.Tickets;
using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>
/// Spec 013 in the browser: what an agent or a requester clicks ends up as a mail in Mailpit - through the app's
/// Wolverine handlers, the outbox and real SMTP.
/// </summary>
public class NotificationTests(E2EEnvironment env)
{
    [Fact]
    public async Task APublicReport_SendsTheRequesterTheConfirmation_WithTheNumberTheyWereShown()
    {
        var page = await env.AnonymousPageAsync();
        var title = E2EEnvironment.Unique("Nowa grafika na stronę");
        await page.GotoAsync("/zglos");
        await page.GetByTestId("title").FillAsync(title);
        await page.GetByTestId("description").FillAsync("Baner na konferencję.");
        await page.GetByTestId("category-marketing").ClickAsync();
        await page.GetByTestId("subcategory-marketing.social").ClickAsync();
        await page.GetByTestId("priority-Medium").ClickAsync();
        await page.GetByTestId("requester-search").FillAsync("Wieczorek");
        await page.GetByTestId("requester-option").First.ClickAsync();
        await page.GetByTestId("submit").ClickAsync();
        await Expect(page.GetByTestId("public-sent")).ToContainTextAsync("HAL-");
        var shown = await page.GetByTestId("public-sent").TextContentAsync();

        var mail = await env.MailAsync("lena.wieczorek@company.io", title);

        mail.Subject.ShouldStartWith("Przyjęliśmy zgłoszenie HAL-");
        shown.ShouldNotBeNull().ShouldContain(mail.Subject["Przyjęliśmy zgłoszenie ".Length..].Split(':')[0]);
        mail.Text.ShouldContain("Termin realizacji:");
        mail.Text.ShouldContain("Priorytet: Średni");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task AHandoverInThePage_SendsTheOtherAgentAMail_WithTheLink()
    {
        var title = E2EEnvironment.Unique("Skaner");
        var (id, _) = await env.ReportAsync(title, Priority.Medium, TimeSpan.FromHours(1));
        await env.TakeAsync(id, "agnieszka.zielinska@company.io");
        var from = await env.SignInAsync("agnieszka.zielinska@company.io");
        await from.GotoAsync("/moje");

        await (await E2EEnvironment.SearchAsync(from, title)).GetByTestId("open-handover").ClickAsync();
        var michal = await env.AgentAsync("michal.lewandowski@company.io");
        await from.GetByTestId("handover-to").SelectOptionAsync(michal.Id.ToString());
        await from.GetByTestId("work-reason").FillAsync("Michał ma klucze do serwerowni.");
        await from.GetByTestId("work-confirm").ClickAsync();
        await Expect(from.GetByTestId("notice-success")).ToContainTextAsync("Michał Lewandowski");

        var mail = await env.MailAsync("michal.lewandowski@company.io", title);
        mail.Subject.ShouldStartWith("Przekazano Ci zgłoszenie");
        mail.Text.ShouldContain("Agnieszka Zielińska przekazał(a) Ci zgłoszenie");
        mail.Text.ShouldContain("Powód: Michał ma klucze do serwerowni.");
        mail.Text.ShouldContain($"{env.BaseUrl}/zgloszenia/{id}");
        await from.Context.CloseAsync();
    }

    [Fact]
    public async Task ResolvingInThePage_TellsTheRequester()
    {
        var title = E2EEnvironment.Unique("Klawiatura");
        await env.ReportAsync(title, Priority.Low, TimeSpan.FromMinutes(5));
        var page = await env.SignInAsync("joanna.wozniak@company.io");

        await (await E2EEnvironment.SearchAsync(page, title)).GetByTestId("take").ClickAsync();
        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("Przejęto");
        await page.GetByTestId("nav-mine").ClickAsync();
        await (await E2EEnvironment.SearchAsync(page, title)).GetByTestId("resolve").ClickAsync();
        await Expect(page.GetByTestId("notice-success")).ToContainTextAsync("zrealizowane");

        // The ticket was arranged around the bus (no confirmation), so the only mail with its title is this one.
        var mail = await env.MailAsync("adam.mazur@company.io", title);
        mail.Subject.ShouldStartWith("Zrealizowano zgłoszenie");
        mail.Text.ShouldContain("przez: Joanna Woźniak");
        await page.Context.CloseAsync();
    }
}
