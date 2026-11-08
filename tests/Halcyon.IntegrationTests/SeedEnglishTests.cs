using Halcyon.Database.Seed;
using Halcyon.Domain.Categories;

namespace Halcyon.IntegrationTests;

/// <summary>ADR-0012: the English demo data - every text translated, every key and choice value the same.</summary>
public class SeedEnglishTests
{
    private static readonly IReadOnlyList<SeedTicket> Polish = SeedDefinition.Default.Tickets;
    private static readonly IReadOnlyList<SeedTicket> English = SeedEnglish.Definition.Tickets;

    [Fact]
    public void TheSameTickets_InTheSameOrder_WithTheSameKeys()
    {
        English
            .Select(t => (t.Key, t.SubcategoryKey, t.Priority, t.ReportedAgo, t.RequesterEmail))
            .ShouldBe(Polish.Select(t => (t.Key, t.SubcategoryKey, t.Priority, t.ReportedAgo, t.RequesterEmail)));
    }

    [Fact]
    public void EveryTitleAndDescription_IsTranslated()
    {
        var untranslated = Polish
            .Zip(English)
            .Where(pair => pair.First.Title == pair.Second.Title || pair.First.Description == pair.Second.Description)
            .Select(pair => pair.First.Key)
            .ToList();

        untranslated.ShouldBeEmpty();
    }

    [Fact]
    public void EveryComment_IsTranslated()
    {
        var untranslated = Polish
            .Zip(English)
            .SelectMany(pair => pair.First.Steps.OfType<Comment>().Zip(pair.Second.Steps.OfType<Comment>()))
            .Where(pair => pair.First.Text == pair.Second.Text)
            .Select(pair => pair.First.Text)
            .ToList();

        untranslated.ShouldBeEmpty();
    }

    /// <summary>The domain checks a choice against the catalog's Polish values: they must not change.</summary>
    [Fact]
    public void ChoiceValues_StayAsTheCatalogHasThem()
    {
        foreach (var (polish, english) in Polish.Zip(English).Where(p => p.First.Fields is not null))
        {
            var fields = CategoryCatalog.Default.FindSubcategory(polish.SubcategoryKey)!.Value.Subcategory.ReportFields;
            foreach (var field in fields.Where(f => f.Kind is FieldKind.Choice or FieldKind.MultiChoice))
                english.Fields!.GetValueOrDefault(field.Key).ShouldBe(polish.Fields!.GetValueOrDefault(field.Key));
        }
    }
}
