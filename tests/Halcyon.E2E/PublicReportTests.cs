using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Spec 002: a person reports without signing in, picking themselves from the list or incognito.</summary>
public class PublicReportTests(E2EEnvironment env)
{
    private static async Task FillAsync(IPage page, string title, string priority)
    {
        await page.GetByTestId("title").FillAsync(title);
        await page.GetByTestId("description").FillAsync("Proszę o pomoc.");
        await page.GetByTestId("category-marketing").ClickAsync();
        await page.GetByTestId("subcategory-marketing.social").ClickAsync();
        await page.GetByTestId($"priority-{priority}").ClickAsync();
    }

    [Fact]
    public async Task Person_PicksThemselves_SetsThePriority_AndTheAgentSeesItInThePool()
    {
        var page = await env.AnonymousPageAsync();
        var title = E2EEnvironment.Unique("Post o webinarze");
        await page.GotoAsync("/zglos");

        await Expect(page.GetByTestId("nav-pool")).ToHaveCountAsync(0);
        await Expect(page.GetByTestId("reported-at-field")).ToHaveCountAsync(0);
        await FillAsync(page, title, "Low");
        await page.GetByTestId("requester-search").FillAsync("Wieczorek");
        await page.GetByTestId("requester-option").First.ClickAsync();
        await page.GetByTestId("submit").ClickAsync();

        await Expect(page.GetByTestId("public-sent")).ToContainTextAsync("HAL-");

        var agent = await env.SignInAsync();
        var row = await E2EEnvironment.SearchAsync(agent, title);
        await Expect(row).ToContainTextAsync("Lena Wieczorek");
        await Expect(row.GetByTestId("priority")).ToHaveTextAsync("Niski");
        await page.Context.CloseAsync();
        await agent.Context.CloseAsync();
    }

    [Fact]
    public async Task Incognito_ClearsThePerson_AndShowsIncognito()
    {
        var page = await env.AnonymousPageAsync();
        var title = E2EEnvironment.Unique("Anonimowy feedback");
        await page.GotoAsync("/zglos");
        await FillAsync(page, title, "Medium");
        await page.GetByTestId("requester-option").First.ClickAsync();
        await page.GetByTestId("requester-incognito").CheckAsync();
        await Expect(page.GetByTestId("requester-list")).ToHaveCountAsync(0);
        await page.GetByTestId("submit").ClickAsync();
        await Expect(page.GetByTestId("public-sent")).ToBeVisibleAsync();

        var agent = await env.SignInAsync();
        await Expect(await E2EEnvironment.SearchAsync(agent, title)).ToContainTextAsync("Incognito");
        await page.Context.CloseAsync();
        await agent.Context.CloseAsync();
    }

    /// <summary>A screenshot of an application error, sent from the public form, opened by the agent (RustFS behind).</summary>
    [Fact]
    public async Task Screenshot_SentWithTheReport_TheAgentSeesAndOpensIt()
    {
        var page = await env.AnonymousPageAsync();
        var title = E2EEnvironment.Unique("Błąd przy zapisie");
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
        );
        await page.GotoAsync("/zglos");
        await page.GetByTestId("title").FillAsync(title);
        await page.GetByTestId("description").FillAsync("Po kliknięciu Zapisz pojawia się błąd.");
        await page.GetByTestId("category-it").ClickAsync();
        await page.GetByTestId("subcategory-it.application").ClickAsync();
        await page.GetByTestId("priority-High").ClickAsync();
        await page.GetByTestId("attachments-input")
            .SetInputFilesAsync(
                new FilePayload
                {
                    Name = "blad.png",
                    MimeType = "image/png",
                    Buffer = png,
                }
            );
        await Expect(page.GetByTestId("attachment-pending")).ToContainTextAsync("blad.png");
        await page.GetByTestId("requester-incognito").CheckAsync();
        await page.GetByTestId("submit").ClickAsync();
        await Expect(page.GetByTestId("public-sent")).ToBeVisibleAsync();

        var agent = await env.SignInAsync();
        await (await E2EEnvironment.SearchAsync(agent, title)).GetByTestId("ticket-link").ClickAsync();
        var attachment = agent.GetByTestId("attachment");
        await Expect(attachment).ToContainTextAsync("blad.png");
        await Expect(attachment).ToContainTextAsync("Incognito");

        var href = await attachment.GetByTestId("attachment-download").GetAttributeAsync("href");
        var response = await agent.Context.APIRequest.GetAsync($"{env.BaseUrl}/{href}");
        response.Status.ShouldBe(200);
        (await response.BodyAsync()).ShouldBe(png);
        await page.Context.CloseAsync();
        await agent.Context.CloseAsync();
    }

    [Fact]
    public async Task ThePublicPage_GivesNoWayIntoThePool()
    {
        var page = await env.AnonymousPageAsync();
        await page.GotoAsync("/zglos");
        await Expect(page.GetByTestId("report-form")).ToBeVisibleAsync();

        await page.GotoAsync("/pula");

        await Expect(page.GetByTestId("signin-form")).ToBeVisibleAsync();
        await page.Context.CloseAsync();
    }
}
