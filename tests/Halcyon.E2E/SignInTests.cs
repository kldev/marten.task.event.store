using Halcyon.E2E.Infrastructure;

namespace Halcyon.E2E;

/// <summary>Spec 001 in the browser.</summary>
public class SignInTests(E2EEnvironment env)
{
    [Fact]
    public async Task Agent_SignsIn_SeesThePool_AndSignsOut()
    {
        var page = await env.SignInAsync("Piotr.Kowalski@company.io");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Pula zgłoszeń" })).ToBeVisibleAsync();
        await Expect(page.GetByTestId("current-agent")).ToHaveTextAsync("Piotr Kowalski");

        await page.GetByTestId("logout").ClickAsync();
        await Expect(page.GetByTestId("signin-form")).ToBeVisibleAsync();
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task WrongPassword_ShowsOneMessage()
    {
        var page = await env.AnonymousPageAsync();
        await page.GotoAsync("/logowanie");
        await page.GetByTestId("signin-email").FillAsync("anna.nowak@company.io");
        await page.GetByTestId("signin-password").FillAsync("wrong");
        await page.GetByTestId("signin-submit").ClickAsync();

        await Expect(page.GetByTestId("notice-error")).ToHaveTextAsync("Nieprawidłowy e-mail lub hasło.");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task AnonymousVisitor_IsSentToSignIn_AndComesBackAfter()
    {
        var page = await env.AnonymousPageAsync();
        await page.GotoAsync("/agenci");

        await Expect(page.GetByTestId("signin-form")).ToBeVisibleAsync();
        await page.GetByTestId("signin-email").FillAsync("anna.nowak@company.io");
        await page.GetByTestId("signin-password").FillAsync(E2EEnvironment.Password);
        await page.GetByTestId("signin-submit").ClickAsync();

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Agenci" })).ToBeVisibleAsync();
        await page.Context.CloseAsync();
    }
}
