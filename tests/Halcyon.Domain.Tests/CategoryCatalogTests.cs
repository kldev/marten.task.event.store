using Halcyon.Domain.Categories;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 002: the owner's categories and the rules of choosing one.</summary>
public class CategoryCatalogTests
{
    [Fact]
    public void HasTheOwnersCategories()
    {
        CategoryCatalog
            .Default.Categories.Select(c => c.Name)
            .ShouldBe([
                "Zgłoszenie do IT",
                "Zgłoszenie do działu Prawnego",
                "Zgłoszenie do Marketingu",
                "Zgłoszenie do rekrutacji",
                "Zgłoszenie do Finansów",
                "Kadry Zagranica",
                "Kadry Polska",
                "Legalizacja",
            ]);
    }

    [Fact]
    public void HrAbroad_IsGroupedByMarket()
    {
        var abroad = CategoryCatalog.Default.FindCategory("hr-abroad")!;
        abroad.Subcategories.Select(s => s.Group).OfType<string>().Distinct().ShouldBe(["Belgia", "Niemcy", "Francja"]);
        CategoryCatalog
            .Default.FindCategory("hr-poland")!
            .Subcategories.Select(s => s.Group)
            .OfType<string>()
            .Distinct()
            .ShouldBe(["Polska"]);
    }

    [Fact]
    public void GroupedSubcategory_IsShownWithItsMarket() =>
        CategoryCatalog
            .Default.NamesFor("hr-abroad", "hr-abroad.be.posting")
            .ShouldBe(("Kadry Zagranica", "Belgia · Zgłoszenie Limosa i formularz A1"));

    [Fact]
    public void HrPoland_HasANewEmployeeSubcategory() =>
        CategoryCatalog
            .Default.NamesFor("hr-poland", "hr-poland.pl.onboarding")
            .ShouldBe(("Kadry Polska", "Polska · Nowy pracownik"));

    [Fact]
    public void It_HasTheOwnersSubcategories() =>
        CategoryCatalog
            .Default.FindCategory("it")!
            .Subcategories.Select(s => s.Name)
            .ShouldBe(["Sprzęt", "Awaria", "Problem z aplikacją", "Feedback"]);

    [Fact]
    public void Legal_HasItsOwnSubcategories_NotACopyOfIt()
    {
        var legal = CategoryCatalog.Default.FindCategory("legal")!.Subcategories.Select(s => s.Name).ToList();
        legal.ShouldContain("Umowy");
        legal.ShouldContain("Ochrona danych (RODO)");
        legal.ShouldNotContain("Sprzęt");
    }

    [Fact]
    public void Keys_AreUniqueAcrossTheCatalog()
    {
        var keys = CategoryCatalog.Default.Categories.SelectMany(c => c.Subcategories).Select(s => s.Key).ToList();
        keys.Distinct().Count().ShouldBe(keys.Count);
    }

    [Fact]
    public void EverySubcategoryKey_StartsWithItsCategory()
    {
        foreach (var category in CategoryCatalog.Default.Categories)
        foreach (var sub in category.Subcategories)
            sub.Key.ShouldStartWith(category.Key + ".");
    }

    [Fact]
    public void RetiredSubcategory_IsNotOffered_ButStillHasItsName()
    {
        var catalog = new CategoryCatalog([
            new Category("it", "IT", [new Subcategory("it.fax", "Fax", Active: false), new Subcategory("it.pc", "PC")]),
        ]);

        catalog.FindCategory("it")!.ActiveSubcategories.Select(s => s.Key).ShouldBe(["it.pc"]);
        catalog.NamesFor("it", "it.fax").ShouldBe(("IT", "Fax"));
    }

    [Fact]
    public void UnknownKeys_ShowTheKeys() => CategoryCatalog.Default.NamesFor("x", "x.y").ShouldBe(("x", "x.y"));
}
