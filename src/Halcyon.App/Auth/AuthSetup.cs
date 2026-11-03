using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Halcyon.App.Auth;

public static class AuthSetup
{
    public const string SignInRateLimit = "sign-in";
    public const string SignInPath = "/logowanie";

    public static WebApplicationBuilder AddHalcyonAuth(this WebApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<AuthOptions>()
            .Bind(builder.Configuration.GetSection(AuthOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder
            .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.LoginPath = SignInPath;
                cookie.LogoutPath = "/auth/logout";
                cookie.Cookie.Name = "halcyon.session";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.SlidingExpiration = true;
            });
        builder
            .Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>(
                (cookie, auth) => cookie.ExpireTimeSpan = TimeSpan.FromHours(auth.Value.SessionHours)
            );

        // Pages require a signed-in agent through [Authorize] in Components/Pages/_Imports.razor, not a fallback policy:
        // a fallback policy would also close Blazor's own endpoints (_blazor) to the anonymous public form.
        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();

        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Back to the form with a message, instead of a bare 429.
            limiter.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Redirect($"{SignInPath}?error=limit");
                return ValueTask.CompletedTask;
            };
            limiter.AddPolicy(
                SignInRateLimit,
                http =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = http
                                .RequestServices.GetRequiredService<IOptions<AuthOptions>>()
                                .Value.SignInAttemptsPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                        }
                    )
            );
        });

        builder.Services.AddSingleton<SignInService>();
        return builder;
    }

    /// <summary>The sign-in form posts here; a plain form, because an interactive circuit cannot write a cookie.</summary>
    public static IEndpointRouteBuilder MapHalcyonAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", LoginAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .RequireRateLimiting(SignInRateLimit);
        app.MapPost("/auth/login-default", LoginDefaultAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .RequireRateLimiting(SignInRateLimit);
        app.MapPost(
                "/auth/logout",
                async (HttpContext http) =>
                {
                    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return Results.LocalRedirect(SignInPath);
                }
            )
            .AllowAnonymous()
            .DisableAntiforgery();
        return app;
    }

    private static async Task<IResult> LoginAsync(HttpContext http, SignInService signIn, CancellationToken ct)
    {
        var form = await http.Request.ReadFormAsync(ct);
        var returnUrl = SafeReturnUrl(form["returnUrl"].ToString());
        var principal = await signIn.VerifyAsync(form["email"], form["password"], ct);
        if (principal is null)
        {
            var back = $"{SignInPath}?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}";
            return Results.LocalRedirect(back);
        }

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Results.LocalRedirect(returnUrl);
    }

    private static async Task<IResult> LoginDefaultAsync(HttpContext http, SignInService signIn, CancellationToken ct)
    {
        var form = await http.Request.ReadFormAsync(ct);
        var returnUrl = SafeReturnUrl(form["returnUrl"].ToString());
        var principal = await signIn.DefaultAgentAsync(ct);
        if (principal is null)
        {
            return Results.NotFound();
        }

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Results.LocalRedirect(returnUrl);
    }

    /// <summary>Only a local path; anything else (another site, "//x") becomes the start page.</summary>
    public static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : "/";
}
