using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Halcyon.App.Localization;

/// <summary>
/// The two UI languages (ADR-0012): Polish, the default and the language of the data, and English. The choice lives
/// in the culture cookie and is read on every request; a Blazor circuit keeps the culture of the request that opened
/// it, so the switch reloads the page.
/// </summary>
public static class Language
{
    public const string Polish = "pl-PL";
    public const string English = "en-GB";

    /// <summary>GET /jezyk/{pl|en}?returnUrl=… - the switch in the top bar and on the sign-in page.</summary>
    private const string SwitchPath = "/jezyk";

    private static readonly string[] Supported = [Polish, English];

    public static bool IsEnglish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

    /// <summary>Numbers and dates the way the reader writes them: "1 234,5" in Polish, "1,234.5" in English.</summary>
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(IsEnglish ? English : Polish);

    /// <summary>"pl" or "en": the page's lang attribute and the switch's active button.</summary>
    public static string Code => IsEnglish ? "en" : "pl";

    /// <summary>The address that switches to <paramref name="code"/> and comes back to <paramref name="returnUrl"/>.</summary>
    public static string SwitchUrl(string code, string returnUrl) =>
        $"{SwitchPath.TrimStart('/')}/{code}?returnUrl={Uri.EscapeDataString(returnUrl)}";

    public static IServiceCollection AddHalcyonLanguages(this IServiceCollection services) =>
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SetDefaultCulture(Polish).AddSupportedCultures(Supported).AddSupportedUICultures(Supported);
            // The cookie only: a browser's Accept-Language does not change the language the team works in.
            options.RequestCultureProviders = [new CookieRequestCultureProvider()];
        });

    public static IEndpointRouteBuilder MapLanguageSwitch(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                SwitchPath + "/{code}",
                (string code, string? returnUrl, HttpContext http) =>
                {
                    var culture = code == "en" ? English : Polish;
                    http.Response.Cookies.Append(
                        CookieRequestCultureProvider.DefaultCookieName,
                        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                        new CookieOptions
                        {
                            Expires = DateTimeOffset.UtcNow.AddYears(1),
                            IsEssential = true,
                            HttpOnly = true,
                            SameSite = SameSiteMode.Lax,
                            Secure = http.Request.IsHttps,
                        }
                    );
                    // Only a path of this application: never an absolute address (an open redirect).
                    var back =
                        returnUrl is { Length: > 0 }
                        && returnUrl.StartsWith('/')
                        && !returnUrl.StartsWith("//")
                        && !returnUrl.StartsWith("/\\")
                            ? returnUrl
                            : "/";
                    return Results.LocalRedirect(back);
                }
            )
            .AllowAnonymous();
        return app;
    }
}
