using Halcyon.Domain.Categories;
using Halcyon.Domain.Tickets;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 008: the report fields of a subcategory - what is stored, what is refused.</summary>
public class ReportFieldsTests
{
    private static readonly IReadOnlyList<FieldDefinition> Posting = CategoryCatalog
        .Default.FindSubcategory("hr-abroad.be.posting")!
        .Value.Subcategory.ReportFields;

    private static readonly IReadOnlyList<FieldDefinition> Onboarding = CategoryCatalog
        .Default.FindSubcategory("hr-poland.pl.onboarding")!
        .Value.Subcategory.ReportFields;

    private static Dictionary<string, string?> V(params (string Key, string? Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value);

    private static Dictionary<string, string?> NewEmployee(params (string Key, string? Value)[] extra)
    {
        var values = V(("first-name", "Olena"), ("last-name", "Kovalenko"), ("department", "Logistyka"));
        foreach (var (key, value) in extra)
            values[key] = value;
        return values;
    }

    [Fact]
    public void Values_AreTrimmed_AndEmptyOnesAreNotStored() =>
        ReportFields
            .Check(Posting, V(("limosa-number", "  L1-1  "), ("a1-number", "   "), ("site", null)))
            .ShouldBe(new Dictionary<string, string> { ["limosa-number"] = "L1-1" });

    [Fact]
    public void AFieldOfAnotherSubcategory_IsRefused() =>
        Should
            .Throw<DomainException>(() => ReportFields.Check(Posting, V(("sipsi-number", "S-1"))))
            .Message.ShouldContain("sipsi-number");

    [Fact]
    public void AValue_HasAMaximumLength()
    {
        ReportFields.Check(Posting, V(("site", new string('x', ReportFields.ValueMax)))).ShouldContainKey("site");
        Should.Throw<DomainException>(() =>
            ReportFields.Check(Posting, V(("site", new string('x', ReportFields.ValueMax + 1))))
        );
    }

    [Theory]
    [InlineData("2026-11-09", "2026-11-09")]
    [InlineData(" 2026-02-28 ", "2026-02-28")]
    public void ADate_IsADay(string value, string stored) =>
        ReportFields.Check(Posting, V(("period-from", value)))["period-from"].ShouldBe(stored);

    [Theory]
    [InlineData("09.11.2026")]
    [InlineData("2026-02-30")]
    [InlineData("jutro")]
    public void ADate_InAnotherFormat_IsRefused(string value) =>
        Should.Throw<DomainException>(() => ReportFields.Check(Posting, V(("period-from", value))));

    [Fact]
    public void AChoice_TakesTheListsSpelling_AndRefusesTheRest()
    {
        ReportFields.Check(Onboarding, NewEmployee(("department", "logistyka")))["department"].ShouldBe("Logistyka");
        Should.Throw<DomainException>(() => ReportFields.Check(Onboarding, NewEmployee(("department", "Kosmos"))));
    }

    [Theory]
    [InlineData("olena.kovalenko@company.io", true)]
    [InlineData("olena", false)]
    [InlineData("Olena <olena@company.io>", false)]
    public void AnEmail_IsAnAddress(string value, bool ok)
    {
        IReadOnlyDictionary<string, string> Check() => ReportFields.Check(Onboarding, NewEmployee(("email", value)));
        if (ok)
            Check()["email"].ShouldBe(value);
        else
            Should.Throw<DomainException>(Check);
    }

    [Theory]
    [InlineData("first-name")]
    [InlineData("last-name")]
    [InlineData("department")]
    public void ANewEmployee_NeedsNameAndDepartment(string missing)
    {
        var values = NewEmployee();
        values[missing] = " ";
        Should.Throw<DomainException>(() => ReportFields.Check(Onboarding, values)).Message.ShouldContain("Uzupełnij");
    }

    [Fact]
    public void ANewEmployee_HasTheOwnersFields() =>
        Onboarding
            .Select(f => f.Label)
            .ShouldBe([
                "Imię",
                "Nazwisko",
                "E-mail",
                "Dział",
                "Kraj pochodzenia",
                "Dokument tożsamości",
                "Numer PESEL / dowodu osobistego",
                "Data rozpoczęcia pracy",
            ]);

    [Fact]
    public void Country_IsEuropean_WithTheFavoritesFirst()
    {
        var country = KnownFields.Country;
        country.Favorites.ShouldBe(["Polska", "Czechy", "Niemcy", "Belgia", "Ukraina", "Rumunia"]);
        country.Options!.ShouldContain("Słowacja");
        country.Options!.ShouldNotContain("Chiny");
        foreach (var favorite in country.Favorites!)
            country.Options!.ShouldContain(favorite);
    }

    [Theory]
    [InlineData("92031501232", true)]
    [InlineData("92031501233", false)]
    [InlineData("9203150123", false)]
    [InlineData("9203150123a", false)]
    public void Pesel_HasItsCheckDigit(string value, bool valid) => ReportFields.IsValidPesel(value).ShouldBe(valid);

    [Fact]
    public void Pesel_MustBeGiven_AndValid()
    {
        ReportFields
            .Check(Onboarding, NewEmployee(("identity-document", "PESEL"), ("identity-number", "92031501232")))
            .ShouldContainKeyAndValue("identity-number", "92031501232");
        Should.Throw<DomainException>(() =>
            ReportFields.Check(Onboarding, NewEmployee(("identity-document", "PESEL")))
        );
        Should.Throw<DomainException>(() =>
            ReportFields.Check(
                Onboarding,
                NewEmployee(("identity-document", "PESEL"), ("identity-number", "12345678901"))
            )
        );
    }

    [Fact]
    public void IdCard_NumberIsWrittenWithoutSpaces_InCapitals() =>
        ReportFields
            .Check(Onboarding, NewEmployee(("identity-document", "Dowód osobisty"), ("identity-number", "abc 123456")))
            .ShouldContainKeyAndValue("identity-number", "ABC123456");

    [Fact]
    public void NoDocuments_HasNoNumber()
    {
        ReportFields
            .Check(Onboarding, NewEmployee(("identity-document", "Brak dokumentów")))
            .ShouldNotContainKey("identity-number");
        Should.Throw<DomainException>(() =>
            ReportFields.Check(
                Onboarding,
                NewEmployee(("identity-document", "Brak dokumentów"), ("identity-number", "123"))
            )
        );
    }

    [Fact]
    public void ANumber_WithoutADocument_IsRefused() =>
        Should.Throw<DomainException>(() => ReportFields.Check(Onboarding, NewEmployee(("identity-number", "123"))));

    [Fact]
    public void OneKey_MeansOneThing_InTheWholeCatalog()
    {
        var byKey = CategoryCatalog
            .Default.Categories.SelectMany(c => c.Subcategories)
            .SelectMany(s => s.ReportFields)
            .GroupBy(f => f.Key);
        // Only "required" may differ between subcategories; the meaning (label, kind, options) may not.
        foreach (var group in byKey)
            group
                .Select(f => (f.Label, f.Kind, Options: string.Join("|", f.Options ?? [])))
                .Distinct()
                .Count()
                .ShouldBe(1, $"field {group.Key} is defined in more than one way");
    }

    [Fact]
    public void EveryChoice_HasOptions_AndItsFavoritesAreAmongThem()
    {
        foreach (
            var field in CategoryCatalog
                .Default.Categories.SelectMany(c => c.Subcategories)
                .SelectMany(s => s.ReportFields)
                .Where(f => f.Kind == FieldKind.Choice)
        )
        {
            field.Options.ShouldNotBeNull().ShouldNotBeEmpty();
            (field.Favorites ?? []).Except(field.Options!).ShouldBeEmpty();
        }
    }

    [Fact]
    public void Postings_AskForTheirNumbers()
    {
        string[] Keys(string sub) =>
            [.. CategoryCatalog.Default.FindSubcategory(sub)!.Value.Subcategory.ReportFields.Select(f => f.Key)];
        Keys("hr-abroad.be.posting").ShouldContain("limosa-number");
        Keys("hr-abroad.de.posting").ShouldContain("anmeldung-number");
        Keys("hr-abroad.fr.posting").ShouldContain("sipsi-number");
        Keys("hr-abroad.feedback").ShouldBeEmpty();
    }
}

/// <summary>Spec 008: recruitment and the marketing budget - counts, several channels, a linked ticket.</summary>
public class RecruitmentFieldsTests
{
    private static IReadOnlyList<FieldDefinition> Fields(string sub) =>
        CategoryCatalog.Default.FindSubcategory(sub)!.Value.Subcategory.ReportFields;

    private static Dictionary<string, string?> Campaign(params (string Key, string? Value)[] extra)
    {
        var values = new Dictionary<string, string?> { ["channels"] = "Facebook", ["amount"] = "2000" };
        foreach (var (key, value) in extra)
            values[key] = value;
        return values;
    }

    [Fact]
    public void Recruitment_IsForTheCompany_AndForClients()
    {
        var recruitment = CategoryCatalog.Default.FindCategory("recruitment")!;
        recruitment.Name.ShouldBe("Zgłoszenie do rekrutacji");
        recruitment
            .Subcategories.Select(s => s.Name)
            .ShouldBe([
                "Rekrutacja do firmy",
                "Rekrutacja dla klienta (agencja pracy)",
                "Kandydaci i rozmowy",
                "Feedback",
            ]);
        Fields("recruitment.client").Where(f => f.Required).Select(f => f.Key).ShouldBe(["client", "position"]);
    }

    [Theory]
    [InlineData("12", "12")]
    [InlineData("1 200", "1200")]
    public void Headcount_IsAWholeNumberFromOne(string value, string stored) =>
        ReportFields
            .Check(
                Fields("recruitment.client"),
                new Dictionary<string, string?>
                {
                    ["client"] = "Bau GmbH",
                    ["position"] = "Spawacz",
                    ["headcount"] = value,
                }
            )["headcount"]
            .ShouldBe(stored);

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("2,5")]
    [InlineData("10001")]
    public void Headcount_ThatIsNotACount_IsRefused(string value) =>
        Should.Throw<DomainException>(() =>
            ReportFields.Check(
                Fields("recruitment.internal"),
                new Dictionary<string, string?> { ["position"] = "Księgowa", ["headcount"] = value }
            )
        );

    [Fact]
    public void Channels_AreSeveral_InTheListsOrder_WithTheListsSpelling() =>
        ReportFields
            .Check(Fields("marketing.budget"), Campaign(("channels", "tiktok, Facebook ,TikTok")))["channels"]
            .ShouldBe("Facebook, TikTok");

    [Fact]
    public void AChannelOffTheList_IsRefused() =>
        Should
            .Throw<DomainException>(() =>
                ReportFields.Check(Fields("marketing.budget"), Campaign(("channels", "Facebook, MySpace")))
            )
            .Message.ShouldContain("MySpace");

    [Fact]
    public void ACampaign_NeedsChannelsAndAnAmount()
    {
        Should.Throw<DomainException>(() => ReportFields.Check(Fields("marketing.budget"), Campaign(("channels", ""))));
        Should.Throw<DomainException>(() => ReportFields.Check(Fields("marketing.budget"), Campaign(("amount", null))));
        // The same amount field is optional elsewhere.
        ReportFields.Check(Fields("finance.budget"), new Dictionary<string, string?>()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("hal-1a2b3c4d", "HAL-1A2B3C4D")]
    [InlineData(" HAL-0137243B ", "HAL-0137243B")]
    public void RelatedTicket_IsATicketNumber(string value, string stored) =>
        ReportFields
            .Check(Fields("marketing.budget"), Campaign(("related-ticket", value)))["related-ticket"]
            .ShouldBe(stored);

    [Theory]
    [InlineData("1A2B3C4D")]
    [InlineData("HAL-1A2B3C4")]
    [InlineData("HAL-1A2B3C4G")]
    public void RelatedTicket_ThatIsNotANumber_IsRefused(string value) =>
        Should.Throw<DomainException>(() =>
            ReportFields.Check(Fields("marketing.budget"), Campaign(("related-ticket", value)))
        );

    [Fact]
    public void Departments_IncludeRecruitment() => KnownFields.Department.Options!.ShouldContain("Rekrutacja");
}

/// <summary>Spec 008: the finance fields - money and bank accounts.</summary>
public class FinanceFieldsTests
{
    private static readonly IReadOnlyList<FieldDefinition> Payment = CategoryCatalog
        .Default.FindSubcategory("finance.payments")!
        .Value.Subcategory.ReportFields;

    [Theory]
    [InlineData("1234.56", "1234.56")]
    [InlineData("1 234,56", "1234.56")]
    [InlineData("1\u00A0234,5", "1234.50")]
    [InlineData("18450", "18450.00")]
    [InlineData("-250,00", "-250.00")]
    public void Amount_IsStoredWithTwoDecimals(string value, string stored) =>
        ReportFields.Check(Payment, new Dictionary<string, string?> { ["amount"] = value })["amount"].ShouldBe(stored);

    [Theory]
    [InlineData("12,345")]
    [InlineData("1.234,56")]
    [InlineData("dużo")]
    [InlineData("1e5")]
    [InlineData("1000000000000")]
    public void Amount_ThatIsNotMoney_IsRefused(string value) =>
        Should
            .Throw<DomainException>(() =>
                ReportFields.Check(Payment, new Dictionary<string, string?> { ["amount"] = value })
            )
            .Message.ShouldContain("kwotą");

    [Theory]
    [InlineData("PL61 1090 1014 0000 0712 1981 2874", "PL61109010140000071219812874")]
    [InlineData("61109010140000071219812874", "PL61109010140000071219812874")]
    [InlineData("de89 3704 0044 0532 0130 00", "DE89370400440532013000")]
    public void Iban_IsCheckedAndWrittenCompact(string value, string stored) =>
        ReportFields.NormalizeIban(value).ShouldBe(stored);

    [Theory]
    [InlineData("PL62 1090 1014 0000 0712 1981 2874")]
    [InlineData("1090 1014")]
    [InlineData("PL61-1090-1014-0000-0712-1981-2874")]
    public void Iban_WithWrongCheckDigits_IsRefused(string value) =>
        Should.Throw<DomainException>(() =>
            ReportFields.Check(Payment, new Dictionary<string, string?> { ["bank-account"] = value })
        );

    [Fact]
    public void Finance_HasItsSubcategories_AndEveryMoneyOneAsksForAnAmountAndCurrency()
    {
        var finance = CategoryCatalog.Default.FindCategory("finance")!;
        finance.Name.ShouldBe("Zgłoszenie do Finansów");
        foreach (var sub in finance.Subcategories.Where(s => s.Key != "finance.feedback"))
        {
            sub.ReportFields.Select(f => f.Key).ShouldContain("amount");
            sub.ReportFields.Select(f => f.Key).ShouldContain("currency");
        }
        KnownFields.Currency.Favorites.ShouldBe(["PLN", "EUR"]);
    }
}

/// <summary>Spec 008: report data on the ticket - at the report and later.</summary>
public class TicketFieldsTests
{
    private static readonly Guid Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static TicketDraft Posting(Dictionary<string, string?>? fields) =>
        TestData.Draft(category: "hr-abroad", subcategory: "hr-abroad.be.posting") with
        {
            Fields = fields,
        };

    private static Ticket Reported(Dictionary<string, string?>? fields = null) =>
        Ticket.FromEvents([Ticket.Report(Id, Posting(fields), TestData.Now)]);

    [Fact]
    public void Report_StoresTheGivenFields()
    {
        var e = Ticket.Report(Id, Posting(new() { ["site"] = "Antwerpia", ["a1-number"] = "" }), TestData.Now);
        e.Fields.ShouldBe(new Dictionary<string, string> { ["site"] = "Antwerpia" });
        Ticket.FromEvents([e]).Fields.ShouldBe(new Dictionary<string, string> { ["site"] = "Antwerpia" });
    }

    [Fact]
    public void Report_WithoutFields_StoresNone() =>
        Ticket.Report(Id, Posting(null), TestData.Now).Fields.ShouldBeNull();

    [Fact]
    public void Report_OfANewEmployee_WithoutTheName_IsRefused() =>
        Should.Throw<DomainException>(() =>
            Ticket.Report(
                Id,
                TestData.Draft(category: "hr-poland", subcategory: "hr-poland.pl.onboarding"),
                TestData.Now
            )
        );

    [Fact]
    public void Report_WithAFieldOfAnotherSubcategory_IsRefused() =>
        Should.Throw<DomainException>(() =>
            Ticket.Report(
                Id,
                TestData.Draft() with
                {
                    Fields = new Dictionary<string, string?> { ["site"] = "x" },
                },
                TestData.Now
            )
        );

    [Fact]
    public void Update_RecordsOnlyWhatChanged_WithTheLabel()
    {
        var ticket = Reported(new() { ["site"] = "Antwerpia", ["employee"] = "Marcin Lis" });

        var e = ticket.UpdateFields(
            TestData.Anna,
            new Dictionary<string, string?>
            {
                ["site"] = "Antwerpia",
                ["employee"] = "",
                ["limosa-number"] = "L1-77",
            },
            TestData.Now
        )!;

        e.Changes.ShouldBe([
            new FieldChange("employee", "Pracownik (imię i nazwisko)", "Marcin Lis", null),
            new FieldChange("limosa-number", "Numer zgłoszenia Limosa-1", null, "L1-77"),
        ]);
        ticket.Then(e).Fields.OrderBy(f => f.Key).ShouldBe([new("limosa-number", "L1-77"), new("site", "Antwerpia")]);
    }

    [Fact]
    public void Update_WithTheSameValues_IsNothing() =>
        Reported(new() { ["site"] = "Antwerpia" })
            .UpdateFields(TestData.Anna, new Dictionary<string, string?> { ["site"] = " Antwerpia " }, TestData.Now)
            .ShouldBeNull();

    [Fact]
    public void Update_IsAllowed_AfterTheTicketIsResolved()
    {
        var ticket = Reported();
        ticket = ticket.Then(ticket.Take(TestData.Anna, TestData.Now));
        ticket = ticket.Then(ticket.Resolve(TestData.Anna, TestData.Now));

        ticket
            .UpdateFields(TestData.Piotr, new Dictionary<string, string?> { ["a1-number"] = "A1/1" }, TestData.Now)
            .ShouldNotBeNull();
    }

    [Fact]
    public void Update_OfASubcategoryWithoutFields_IsRefused() =>
        Should.Throw<DomainException>(() =>
            TestData.Reported().UpdateFields(TestData.Anna, new Dictionary<string, string?>(), TestData.Now)
        );

    [Fact]
    public void Update_KeepsTheRules() =>
        Should.Throw<DomainException>(() =>
            Reported()
                .UpdateFields(
                    TestData.Anna,
                    new Dictionary<string, string?> { ["period-to"] = "31.12.2026" },
                    TestData.Now
                )
        );
}
