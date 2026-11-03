using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Spec 001: agents see the team and the people who report.</summary>
public class AgentsAndPeopleTests(E2EEnvironment env)
{
    [Fact]
    public async Task Agents_ShowNamePhoneAndCompanyEmail()
    {
        var page = await env.SignInAsync();
        await page.GetByTestId("nav-agents").ClickAsync();

        await Expect(page.GetByTestId("agent-card")).ToHaveCountAsync(10);
        var anna = page.Locator("[data-testid=agent-card][data-email='anna.nowak@company.io']");
        await Expect(anna.GetByTestId("agent-name")).ToHaveTextAsync("Anna Nowak");
        await Expect(anna.GetByTestId("agent-phone")).ToHaveTextAsync("+48 600 100 101");
        await Expect(anna.GetByTestId("agent-email")).ToHaveTextAsync("anna.nowak@company.io");
        await Expect(anna.GetByTestId("agent-load")).ToContainTextAsync("W realizacji");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task People_ListsThirty()
    {
        var page = await env.SignInAsync();
        await page.GetByTestId("nav-people").ClickAsync();

        await Expect(page.GetByTestId("person-row")).ToHaveCountAsync(40);
        await page.Context.CloseAsync();
    }
}
