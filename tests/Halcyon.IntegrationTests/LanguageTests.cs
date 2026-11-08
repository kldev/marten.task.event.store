using System.Globalization;
using System.Net;
using Halcyon.App.Localization;
using Halcyon.Domain.Categories;
using Halcyon.IntegrationTests.Infrastructure;

namespace Halcyon.IntegrationTests;

/// <summary>Spec 011 / ADR-0012: the two languages, the switch and the translated catalog and refusals.</summary>
public class LanguageTests(PostgresContainer postgres) : IAsyncLifetime
{
    private TestApp app = null!;

    public async ValueTask InitializeAsync() => app = await TestApp.StartAsync(postgres);

    public async ValueTask DisposeAsync() => await app.DisposeAsync();

    private static T In<T>(string culture, Func<T> read)
    {
        var (before, beforeUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return read();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (before, beforeUi);
        }
    }

    [Fact]
    public void T_PicksTheReadersLanguage()
    {
        In(Language.Polish, () => Tr.T("Pula", "Pool")).ShouldBe("Pula");
        In(Language.English, () => Tr.T("Pula", "Pool")).ShouldBe("Pool");
    }

    [Fact]
    public void N_TranslatesTheCatalog_AndLeavesTheUnknownAsItIs()
    {
        In(Language.English, () => Tr.N("Kadry Polska")).ShouldBe("HR Poland");
        In(Language.English, () => Tr.N("Krytyczny")).ShouldBe("Critical");
        In(Language.English, () => Tr.N("Jan Kowalski")).ShouldBe("Jan Kowalski");
        In(Language.English, () => Tr.N((string?)null)).ShouldBe("");
        In(Language.Polish, () => Tr.N("Kadry Polska")).ShouldBe("Kadry Polska");
    }

    [Fact]
    public void N_OfAGroupedTopic_TranslatesBothParts()
    {
        var topic = new Subcategory("hr-abroad.be.posting", "Zgłoszenie Limosa i formularz A1", Group: "Belgia");

        In(Language.English, () => Tr.N(topic)).ShouldBe("Belgium · Limosa declaration and A1 form");
        In(Language.Polish, () => Tr.N(topic)).ShouldBe(topic.DisplayName);
    }

    /// <summary>Every name of the catalog the UI shows has its English: a new one without a line here would read Polish.</summary>
    [Fact]
    public void EveryCatalogName_HasEnglish()
    {
        var names = CategoryCatalog
            .Default.Categories.SelectMany(c =>
                c.Subcategories.SelectMany(s => new[] { s.Name, s.Group }.Concat(s.ReportFields.Select(f => f.Label)))
                    .Append(c.Name)
            )
            .OfType<string>()
            .Distinct()
            .ToList();

        var missing = names.Where(n => In(Language.English, () => Tr.N(n)) == n && n != "Feedback").ToList();

        missing.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Zgłoszenie jest już zrealizowane.", "The ticket is already resolved.")]
    [InlineData("Pole „Tytuł” jest wymagane.", "“Title” is required.")]
    [InlineData(
        "Zgłoszenie jest już w realizacji u: Anna Nowak.",
        "The ticket is already being handled by: Anna Nowak."
    )]
    [InlineData(
        "Tylko agent realizujący zgłoszenie (Anna Nowak) może je przekazać.",
        "Only the agent handling the ticket (Anna Nowak) can hand it over."
    )]
    [InlineData("Wybierz osobę z działu Finanse.", "Pick a person from the Finance department.")]
    [InlineData("Coś zupełnie nowego.", "Coś zupełnie nowego.")]
    public void Err_TranslatesTheDomainsRefusals(string polish, string english)
    {
        In(Language.English, () => Tr.Err(polish)).ShouldBe(english);
        In(Language.Polish, () => Tr.Err(polish)).ShouldBe(polish);
    }

    [Fact]
    public async Task TheSwitch_WritesTheCookie_AndComesBack()
    {
        using var client = app.Client();

        var response = await client.GetAsync(new Uri("/jezyk/en?returnUrl=%2Fwykresy", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/wykresy");
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.Contains("c%3Den-GB"));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("")]
    public async Task TheSwitch_NeverLeavesTheApp(string returnUrl)
    {
        using var client = app.Client();

        var response = await client.GetAsync(
            new Uri($"/jezyk/pl?returnUrl={Uri.EscapeDataString(returnUrl)}", UriKind.Relative)
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/");
    }
}
