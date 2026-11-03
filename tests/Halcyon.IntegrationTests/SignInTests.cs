using System.Net;
using Halcyon.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Options;

namespace Halcyon.IntegrationTests;

/// <summary>Spec 001 / ADR-0005 over HTTP.</summary>
public class SignInTests(PostgresContainer postgres) : IAsyncLifetime
{
    private TestApp app = null!;

    public async ValueTask InitializeAsync()
    {
        app = await TestApp.StartAsync(postgres);
        await app.SeedPeopleAsync();
    }

    public async ValueTask DisposeAsync() => await app.DisposeAsync();

    private async Task<HttpResponseMessage> SignInAsync(string email, string password, string returnUrl = "/moje")
    {
        using var client = app.Client();
        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["email"] = email,
                ["password"] = password,
                ["returnUrl"] = returnUrl,
            }
        );
        return await client.PostAsync(new Uri("/auth/login", UriKind.Relative), form);
    }

    [Theory]
    [InlineData("anna.nowak@company.io")]
    [InlineData("ANNA.Nowak@Company.IO")]
    [InlineData("  anna.nowak@company.io ")]
    public async Task RightPassword_SignsIn_WhateverTheLetterCase(string email)
    {
        var response = await SignInAsync(email, TestApp.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/moje");
        response
            .Headers.GetValues("Set-Cookie")
            .ShouldContain(c => c.StartsWith("halcyon.session=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WrongPassword_AndUnknownEmail_GetTheSameAnswer()
    {
        var wrongPassword = await SignInAsync("anna.nowak@company.io", "nope");
        var unknownEmail = await SignInAsync("nobody@company.io", TestApp.Password);

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        wrongPassword.Headers.Location!.ToString().ShouldStartWith("/logowanie?error=invalid");
        unknownEmail.Headers.Location!.ToString().ShouldBe(wrongPassword.Headers.Location!.ToString());
        wrongPassword.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task APersonFromTheList_CannotSignIn() =>
        (await SignInAsync("adam.mazur@company.io", TestApp.Password))
            .Headers.Location!.ToString()
            .ShouldStartWith("/logowanie?error=invalid");

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example")]
    public async Task ForeignReturnUrl_GoesToTheStartPage(string returnUrl) =>
        (await SignInAsync("anna.nowak@company.io", TestApp.Password, returnUrl))
            .Headers.Location!.ToString()
            .ShouldBe("/");

    [Theory]
    [InlineData("/")]
    [InlineData("/pula")]
    [InlineData("/moje")]
    [InlineData("/agenci")]
    [InlineData("/osoby")]
    [InlineData("/nowe")]
    public async Task Pages_WithoutSignIn_RedirectToSignIn(string path)
    {
        using var client = app.Client();
        var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("/logowanie");
    }

    [Theory]
    [InlineData("/logowanie")]
    [InlineData("/zglos")]
    [InlineData("/health")]
    public async Task OpenPages_NeedNoSignIn(string path)
    {
        using var client = app.Client();
        (await client.GetAsync(new Uri(path, UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Regression: a fallback "authenticated user" policy also closed Blazor's own endpoints, so the anonymous public
    /// form rendered as an empty page that never connected (found by the e2e tests).
    /// </summary>
    [Fact]
    public async Task BlazorsOwnEndpoints_AreOpen_SoThePublicFormCanConnect()
    {
        using var client = app.Client();
        var response = await client.GetAsync(new Uri("/_blazor/initializers", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Regression: a rejected attempt was re-executed on a Blazor page and answered with an antiforgery error.</summary>
    [Fact]
    public async Task TooManyAttempts_GoBackToTheFormWithAMessage()
    {
        await using var limited = await TestApp.StartAsync(
            postgres,
            settings: new() { ["Auth:SignInAttemptsPerMinute"] = "2" }
        );
        using var client = limited.Client();

        HttpResponseMessage last = null!;
        for (var i = 0; i < 3; i++)
        {
            using var form = new FormUrlEncodedContent(
                new Dictionary<string, string> { ["email"] = "x@company.io", ["password"] = "x" }
            );
            last = await client.PostAsync(new Uri("/auth/login", UriKind.Relative), form);
        }

        last.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        last.Headers.Location!.ToString().ShouldBe("/logowanie?error=limit");
    }

    private static async Task<HttpResponseMessage> SignInAsDefaultAsync(TestApp host)
    {
        using var client = host.Client();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["returnUrl"] = "/moje" });
        return await client.PostAsync(new Uri("/auth/login-default", UriKind.Relative), form);
    }

    [Fact]
    public async Task DefaultAgent_SignsInWithoutAnything_AndTheButtonIsShown()
    {
        await using var demo = await TestApp.StartAsync(
            postgres,
            settings: new() { ["Auth:DefaultAgentEmail"] = "anna.nowak@company.io" }
        );
        await demo.SeedPeopleAsync();

        var response = await SignInAsDefaultAsync(demo);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/moje");
        response
            .Headers.GetValues("Set-Cookie")
            .ShouldContain(c => c.StartsWith("halcyon.session=", StringComparison.Ordinal));
        using var client = demo.Client();
        (await client.GetStringAsync(new Uri("/logowanie", UriKind.Relative))).ShouldContain("signin-default");
    }

    [Fact]
    public async Task WithoutADefaultAgent_ThereIsNoButton_AndNoShortcut()
    {
        var response = await SignInAsDefaultAsync(app);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        using var client = app.Client();
        (await client.GetStringAsync(new Uri("/logowanie", UriKind.Relative))).ShouldNotContain("signin-default");
    }

    [Fact]
    public async Task DefaultAgent_ThatIsNotAnAgent_DoesNotSignIn()
    {
        await using var demo = await TestApp.StartAsync(
            postgres,
            settings: new() { ["Auth:DefaultAgentEmail"] = "adam.mazur@company.io" }
        );
        await demo.SeedPeopleAsync();

        (await SignInAsDefaultAsync(demo)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WithoutThePassword_TheHostDoesNotStart()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var host = TestApp.Build(connectionString, new() { ["Auth:AgentPassword"] = "" }, null, out _);

        var refusal = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
        refusal.Message.ShouldContain("AgentPassword");
    }
}
