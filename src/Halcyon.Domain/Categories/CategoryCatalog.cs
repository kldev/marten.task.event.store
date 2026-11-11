namespace Halcyon.Domain.Categories;

/// <param name="Group">Optional heading the form groups subcategories under (e.g. a market: "Belgia").</param>
/// <param name="Fields">Extra data collected for the reports (spec 008), in the order the form shows them.</param>
public sealed record Subcategory(
    string Key,
    string Name,
    bool Active = true,
    string? Group = null,
    IReadOnlyList<FieldDefinition>? Fields = null
)
{
    /// <summary>The name outside the grouped list: "Belgia · Umowa i zatrudnienie".</summary>
    public string DisplayName => Group is null ? Name : $"{Group} · {Name}";

    public IReadOnlyList<FieldDefinition> ReportFields => Fields ?? [];
}

/// <param name="Department">The department whose people do the work (spec 008): an agent takes the ticket and picks one
/// of them, e.g. when the agent has no access to the system that needs it. Null = anybody from the list.</param>
public sealed record Category(
    string Key,
    string Name,
    IReadOnlyList<Subcategory> Subcategories,
    string? Department = null
)
{
    public IEnumerable<Subcategory> ActiveSubcategories => Subcategories.Where(s => s.Active);

    /// <summary>The markets, when the subcategories are grouped by one.</summary>
    public IReadOnlyList<string> Groups => Subcategories.Select(s => s.Group).OfType<string>().Distinct().ToList();
}

/// <summary>The report fields (spec 008). One key = one meaning everywhere; never rename a key, tickets store it.</summary>
public static class KnownFields
{
    public static readonly FieldDefinition Employee = new("employee", "Pracownik (imię i nazwisko)");
    public static readonly FieldDefinition Site = new("site", "Miejsce pracy (budowa, klient)");
    public static readonly FieldDefinition PeriodFrom = new("period-from", "Od", FieldKind.Date);
    public static readonly FieldDefinition PeriodTo = new("period-to", "Do", FieldKind.Date);
    public static readonly FieldDefinition LimosaNumber = new(
        "limosa-number",
        "Numer zgłoszenia Limosa-1",
        Placeholder: "np. L1-2026-0001234"
    );
    public static readonly FieldDefinition A1Number = new("a1-number", "Numer formularza A1");
    public static readonly FieldDefinition AnmeldungNumber = new("anmeldung-number", "Numer meldunku (Anmeldung)");
    public static readonly FieldDefinition SipsiNumber = new("sipsi-number", "Numer deklaracji SIPSI");
    public static readonly FieldDefinition SettlementMonth = new(
        "settlement-month",
        "Okres rozliczenia",
        Placeholder: "np. 2026-10"
    );
    public static readonly FieldDefinition LodgingAddress = new("lodging-address", "Adres zakwaterowania");
    public static readonly FieldDefinition ContractType = new(
        "contract-type",
        "Rodzaj umowy",
        FieldKind.Choice,
        ["Umowa o pracę", "Umowa zlecenie", "Umowa o dzieło", "B2B"]
    );
    public static readonly FieldDefinition EffectiveDate = new("effective-date", "Obowiązuje od", FieldKind.Date);
    public static readonly FieldDefinition ZlaNumber = new("zla-number", "Numer zwolnienia (e-ZLA)");
    public static readonly FieldDefinition DocumentType = new(
        "document-type",
        "Rodzaj dokumentu",
        Placeholder: "np. PIT-11 za 2025"
    );
    public static readonly FieldDefinition ExamDate = new("exam-date", "Termin badania", FieldKind.Date);
    public static readonly FieldDefinition EndDate = new("end-date", "Data rozwiązania umowy", FieldKind.Date);
    public static readonly FieldDefinition FirstName = new("first-name", "Imię", Required: true);
    public static readonly FieldDefinition LastName = new("last-name", "Nazwisko", Required: true);
    public static readonly FieldDefinition Email = new(
        "email",
        "E-mail",
        FieldKind.Email,
        Placeholder: "imie.nazwisko@company.io"
    );
    public static readonly FieldDefinition Department = new(
        "department",
        "Dział",
        FieldKind.Choice,
        [
            "Administracja",
            "Badania i rozwój",
            "Finanse",
            "IT",
            "Jakość",
            "Kadry",
            "Logistyka",
            "Marketing",
            "Obsługa klienta",
            "Prawny",
            "Produkt",
            "Rekrutacja",
            "Sprzedaż",
            "Utrzymanie ruchu",
            "Zakupy",
            "Zarząd",
        ],
        Required: true
    );

    private static readonly IReadOnlyList<string> Europe =
    [
        "Albania",
        "Andora",
        "Austria",
        "Belgia",
        "Białoruś",
        "Bośnia i Hercegowina",
        "Bułgaria",
        "Chorwacja",
        "Cypr",
        "Czarnogóra",
        "Czechy",
        "Dania",
        "Estonia",
        "Finlandia",
        "Francja",
        "Grecja",
        "Hiszpania",
        "Holandia",
        "Irlandia",
        "Islandia",
        "Kosowo",
        "Liechtenstein",
        "Litwa",
        "Luksemburg",
        "Łotwa",
        "Macedonia Północna",
        "Malta",
        "Mołdawia",
        "Monako",
        "Niemcy",
        "Norwegia",
        "Polska",
        "Portugalia",
        "Rosja",
        "Rumunia",
        "San Marino",
        "Serbia",
        "Słowacja",
        "Słowenia",
        "Szwajcaria",
        "Szwecja",
        "Ukraina",
        "Watykan",
        "Węgry",
        "Wielka Brytania",
        "Włochy",
    ];

    /// <summary>Europe only; the countries the company hires from most often come first in the form.</summary>
    public static readonly FieldDefinition Country = new(
        "country",
        "Kraj pochodzenia",
        FieldKind.Choice,
        Europe,
        Favorites: ["Polska", "Czechy", "Niemcy", "Belgia", "Ukraina", "Rumunia"]
    );
    public static readonly FieldDefinition IdentityDocument = new(
        ReportFields.IdentityDocumentKey,
        "Dokument tożsamości",
        FieldKind.Choice,
        [ReportFields.Pesel, ReportFields.IdCard, ReportFields.NoDocuments]
    );
    public static readonly FieldDefinition IdentityNumber = new(
        ReportFields.IdentityNumberKey,
        "Numer PESEL / dowodu osobistego"
    );
    public static readonly FieldDefinition StartDate = new("start-date", "Data rozpoczęcia pracy", FieldKind.Date);

    public static readonly FieldDefinition InvoiceNumber = new(
        "invoice-number",
        "Numer faktury",
        Placeholder: "np. FV/2026/11/0042"
    );
    public static readonly FieldDefinition Contractor = new("contractor", "Kontrahent", Placeholder: "nazwa lub NIP");
    public static readonly FieldDefinition Amount = new(
        "amount",
        "Kwota",
        FieldKind.Amount,
        Placeholder: "np. 1 234,56"
    );
    public static readonly FieldDefinition Currency = new(
        "currency",
        "Waluta",
        FieldKind.Choice,
        ["PLN", "EUR", "USD", "CHF", "GBP", "CZK", "SEK", "NOK", "DKK", "UAH"],
        Favorites: ["PLN", "EUR"]
    );
    public static readonly FieldDefinition DueDate = new("due-date", "Termin płatności", FieldKind.Date);
    public static readonly FieldDefinition CostCenter = new(
        "cost-center",
        "MPK (miejsce powstawania kosztów)",
        Placeholder: "np. MPK-LOG-01"
    );
    public static readonly FieldDefinition BankAccount = new(
        "bank-account",
        "Numer rachunku (IBAN)",
        FieldKind.Iban,
        Placeholder: "PL61 1090 1014 0000 0712 1981 2874"
    );
    public static readonly FieldDefinition NeededBy = new("needed-by", "Potrzebne do", FieldKind.Date);

    public static readonly FieldDefinition Position = new(
        "position",
        "Stanowisko",
        Required: true,
        Placeholder: "np. spawacz MIG/MAG"
    );
    public static readonly FieldDefinition Headcount = new("headcount", "Liczba osób", FieldKind.Count);
    public static readonly FieldDefinition Client = new("client", "Klient", Required: true, Placeholder: "nazwa firmy");
    public static readonly FieldDefinition WorkCountry = new(
        "work-country",
        "Kraj pracy",
        FieldKind.Choice,
        Europe,
        Favorites: ["Polska", "Niemcy", "Holandia", "Belgia", "Francja", "Czechy"]
    );
    public static readonly FieldDefinition HourlyRate = new(
        "hourly-rate",
        "Stawka godzinowa brutto",
        FieldKind.Amount,
        Placeholder: "np. 16,50"
    );
    public static readonly FieldDefinition SalaryRange = new(
        "salary-range",
        "Widełki wynagrodzenia",
        Placeholder: "np. 7 000-9 000 zł brutto"
    );
    public static readonly FieldDefinition Accommodation = new(
        "accommodation",
        "Zakwaterowanie",
        FieldKind.Choice,
        ["Zapewnia klient", "Zapewnia agencja", "Bez zakwaterowania"]
    );
    public static readonly FieldDefinition Candidate = new("candidate", "Kandydat (imię i nazwisko)");
    public static readonly FieldDefinition InterviewDate = new("interview-date", "Termin rozmowy", FieldKind.Date);
    public static readonly FieldDefinition Channels = new(
        "channels",
        "Kanały",
        FieldKind.MultiChoice,
        [
            "Facebook",
            "Instagram",
            "TikTok",
            "LinkedIn",
            "YouTube",
            "Google Ads",
            "Pracuj.pl",
            "OLX Praca",
            "Indeed",
            "Inne",
        ],
        Required: true
    );
    public static readonly FieldDefinition CampaignGoal = new(
        "campaign-goal",
        "Cel kampanii",
        FieldKind.Choice,
        ["Rekrutacja", "Sprzedaż", "Wizerunek i zasięg"]
    );
    public static readonly FieldDefinition RelatedTicket = new(
        "related-ticket",
        "Powiązane zgłoszenie",
        FieldKind.TicketNumber,
        Placeholder: "HAL-1A2B3C4D"
    );

    internal static IReadOnlyList<FieldDefinition> Abroad(params FieldDefinition[] extra) =>
        [Employee, Site, PeriodFrom, PeriodTo, .. extra];
}

/// <summary>
/// Categories are code (ADR-0003): stable keys stored on tickets, names only for display. Retire a subcategory with
/// <c>Active = false</c> - never delete or rename a key, stored tickets refer to it.
/// </summary>
public sealed class CategoryCatalog(IReadOnlyList<Category> categories)
{
    public IReadOnlyList<Category> Categories { get; } = categories;

    public static CategoryCatalog Default { get; } =
        new([
            new Category(
                "it",
                "Zgłoszenie do IT",
                [
                    new Subcategory("it.hardware", "Sprzęt"),
                    new Subcategory("it.outage", "Awaria"),
                    new Subcategory("it.application", "Problem z aplikacją"),
                    new Subcategory("it.feedback", "Feedback"),
                ]
            ),
            new Category(
                "legal",
                "Zgłoszenie do działu Prawnego",
                [
                    new Subcategory("legal.contracts", "Umowy"),
                    new Subcategory("legal.opinion", "Opinia prawna"),
                    new Subcategory("legal.gdpr", "Ochrona danych (RODO)"),
                    new Subcategory("legal.disputes", "Spory i windykacja"),
                    new Subcategory("legal.feedback", "Feedback"),
                ]
            ),
            new Category(
                "marketing",
                "Zgłoszenie do Marketingu",
                [
                    new Subcategory("marketing.campaign", "Kampania"),
                    new Subcategory("marketing.social", "Social media"),
                    new Subcategory("marketing.graphics", "Materiały graficzne"),
                    new Subcategory("marketing.website", "Strona www"),
                    new Subcategory("marketing.events", "Wydarzenia"),
                    new Subcategory(
                        "marketing.budget",
                        "Budżet na kampanię (Facebook, TikTok…)",
                        Fields:
                        [
                            KnownFields.Channels,
                            KnownFields.CampaignGoal,
                            KnownFields.Amount with
                            {
                                Required = true,
                            },
                            KnownFields.Currency,
                            KnownFields.PeriodFrom,
                            KnownFields.PeriodTo,
                            KnownFields.Position with
                            {
                                Required = false,
                            },
                            KnownFields.Headcount,
                            KnownFields.RelatedTicket,
                        ]
                    ),
                    new Subcategory("marketing.feedback", "Feedback"),
                ]
            ),
            new Category(
                "recruitment",
                "Zgłoszenie do rekrutacji",
                [
                    new Subcategory(
                        "recruitment.internal",
                        "Rekrutacja do firmy",
                        Fields:
                        [
                            KnownFields.Position,
                            KnownFields.Department with
                            {
                                Required = false,
                            },
                            KnownFields.Headcount,
                            KnownFields.ContractType,
                            KnownFields.SalaryRange,
                            KnownFields.StartDate,
                        ]
                    ),
                    new Subcategory(
                        "recruitment.client",
                        "Rekrutacja dla klienta (agencja pracy)",
                        Fields:
                        [
                            KnownFields.Client,
                            KnownFields.Position,
                            KnownFields.Headcount,
                            KnownFields.WorkCountry,
                            KnownFields.Site,
                            KnownFields.PeriodFrom,
                            KnownFields.PeriodTo,
                            KnownFields.ContractType,
                            KnownFields.HourlyRate,
                            KnownFields.Currency,
                            KnownFields.Accommodation,
                        ]
                    ),
                    new Subcategory(
                        "recruitment.candidates",
                        "Kandydaci i rozmowy",
                        Fields:
                        [
                            KnownFields.Position with
                            {
                                Required = false,
                            },
                            KnownFields.Client with
                            {
                                Required = false,
                            },
                            KnownFields.Candidate,
                            KnownFields.InterviewDate,
                        ]
                    ),
                    new Subcategory("recruitment.feedback", "Feedback"),
                ],
                Department: "Rekrutacja"
            ),
            new Category(
                "finance",
                "Zgłoszenie do Finansów",
                [
                    new Subcategory(
                        "finance.invoices",
                        "Faktury kosztowe",
                        Fields:
                        [
                            KnownFields.InvoiceNumber,
                            KnownFields.Contractor,
                            KnownFields.Amount,
                            KnownFields.Currency,
                            KnownFields.DueDate,
                            KnownFields.CostCenter,
                        ]
                    ),
                    new Subcategory(
                        "finance.sales-invoices",
                        "Faktury sprzedażowe i korekty",
                        Fields:
                        [
                            KnownFields.InvoiceNumber,
                            KnownFields.Contractor,
                            KnownFields.Amount,
                            KnownFields.Currency,
                        ]
                    ),
                    new Subcategory(
                        "finance.expenses",
                        "Rozliczenie delegacji i wydatków",
                        Fields:
                        [
                            KnownFields.Employee,
                            KnownFields.Amount,
                            KnownFields.Currency,
                            KnownFields.PeriodFrom,
                            KnownFields.PeriodTo,
                            KnownFields.CostCenter,
                        ]
                    ),
                    new Subcategory(
                        "finance.advance",
                        "Zaliczka",
                        Fields: [KnownFields.Employee, KnownFields.Amount, KnownFields.Currency, KnownFields.NeededBy]
                    ),
                    new Subcategory(
                        "finance.payments",
                        "Przelewy i płatności",
                        Fields:
                        [
                            KnownFields.Contractor,
                            KnownFields.Amount,
                            KnownFields.Currency,
                            KnownFields.DueDate,
                            KnownFields.BankAccount,
                        ]
                    ),
                    new Subcategory(
                        "finance.budget",
                        "Budżet i MPK",
                        Fields: [KnownFields.CostCenter, KnownFields.Amount, KnownFields.Currency]
                    ),
                    new Subcategory("finance.feedback", "Feedback"),
                ],
                Department: "Finanse"
            ),
            new Category(
                "hr-abroad",
                "Kadry Zagranica",
                [
                    new Subcategory(
                        "hr-abroad.be.contract",
                        "Umowa i warunki zatrudnienia",
                        Group: "Belgia",
                        Fields: KnownFields.Abroad(KnownFields.ContractType)
                    ),
                    new Subcategory(
                        "hr-abroad.be.posting",
                        "Zgłoszenie Limosa i formularz A1",
                        Group: "Belgia",
                        Fields: KnownFields.Abroad(KnownFields.LimosaNumber, KnownFields.A1Number)
                    ),
                    new Subcategory(
                        "hr-abroad.be.payroll",
                        "Wynagrodzenie, diety i rozliczenie",
                        Group: "Belgia",
                        Fields: KnownFields.Abroad(KnownFields.SettlementMonth)
                    ),
                    new Subcategory(
                        "hr-abroad.be.tax",
                        "Podatki i składki",
                        Group: "Belgia",
                        Fields: KnownFields.Abroad(KnownFields.SettlementMonth)
                    ),
                    new Subcategory(
                        "hr-abroad.be.travel",
                        "Zakwaterowanie i podróż",
                        Group: "Belgia",
                        Fields: KnownFields.Abroad(KnownFields.LodgingAddress)
                    ),
                    new Subcategory(
                        "hr-abroad.be.leave",
                        "Urlopy, L4 i powroty",
                        Group: "Belgia",
                        Fields: KnownFields.Abroad()
                    ),
                    new Subcategory(
                        "hr-abroad.de.contract",
                        "Umowa i warunki zatrudnienia",
                        Group: "Niemcy",
                        Fields: KnownFields.Abroad(KnownFields.ContractType)
                    ),
                    new Subcategory(
                        "hr-abroad.de.posting",
                        "Formularz A1 i meldunek (Anmeldung)",
                        Group: "Niemcy",
                        Fields: KnownFields.Abroad(KnownFields.A1Number, KnownFields.AnmeldungNumber)
                    ),
                    new Subcategory(
                        "hr-abroad.de.payroll",
                        "Wynagrodzenie, diety i rozliczenie",
                        Group: "Niemcy",
                        Fields: KnownFields.Abroad(KnownFields.SettlementMonth)
                    ),
                    new Subcategory(
                        "hr-abroad.de.tax",
                        "Podatki i składki",
                        Group: "Niemcy",
                        Fields: KnownFields.Abroad(KnownFields.SettlementMonth)
                    ),
                    new Subcategory(
                        "hr-abroad.de.travel",
                        "Zakwaterowanie i podróż",
                        Group: "Niemcy",
                        Fields: KnownFields.Abroad(KnownFields.LodgingAddress)
                    ),
                    new Subcategory(
                        "hr-abroad.de.leave",
                        "Urlopy, L4 i powroty",
                        Group: "Niemcy",
                        Fields: KnownFields.Abroad()
                    ),
                    new Subcategory(
                        "hr-abroad.fr.contract",
                        "Umowa i warunki zatrudnienia",
                        Group: "Francja",
                        Fields: KnownFields.Abroad(KnownFields.ContractType)
                    ),
                    new Subcategory(
                        "hr-abroad.fr.posting",
                        "Deklaracja SIPSI i formularz A1",
                        Group: "Francja",
                        Fields: KnownFields.Abroad(KnownFields.SipsiNumber, KnownFields.A1Number)
                    ),
                    new Subcategory(
                        "hr-abroad.fr.payroll",
                        "Wynagrodzenie, diety i rozliczenie",
                        Group: "Francja",
                        Fields: KnownFields.Abroad(KnownFields.SettlementMonth)
                    ),
                    new Subcategory(
                        "hr-abroad.fr.tax",
                        "Podatki i składki",
                        Group: "Francja",
                        Fields: KnownFields.Abroad(KnownFields.SettlementMonth)
                    ),
                    new Subcategory(
                        "hr-abroad.fr.travel",
                        "Zakwaterowanie i podróż",
                        Group: "Francja",
                        Fields: KnownFields.Abroad(KnownFields.LodgingAddress)
                    ),
                    new Subcategory(
                        "hr-abroad.fr.leave",
                        "Urlopy, L4 i powroty",
                        Group: "Francja",
                        Fields: KnownFields.Abroad()
                    ),
                    new Subcategory("hr-abroad.feedback", "Feedback"),
                ],
                Department: "Kadry"
            ),
            new Category(
                "hr-poland",
                "Kadry Polska",
                [
                    new Subcategory(
                        "hr-poland.pl.contract",
                        "Umowa, aneksy i zmiana warunków",
                        Group: "Polska",
                        Fields: [KnownFields.Employee, KnownFields.ContractType, KnownFields.EffectiveDate]
                    ),
                    new Subcategory(
                        "hr-poland.pl.payroll",
                        "Wynagrodzenie i pasek płacowy",
                        Group: "Polska",
                        Fields: [KnownFields.Employee, KnownFields.SettlementMonth]
                    ),
                    new Subcategory(
                        "hr-poland.pl.leave",
                        "Urlopy, L4 i nieobecności",
                        Group: "Polska",
                        Fields:
                        [
                            KnownFields.Employee,
                            KnownFields.PeriodFrom,
                            KnownFields.PeriodTo,
                            KnownFields.ZlaNumber,
                        ]
                    ),
                    new Subcategory(
                        "hr-poland.pl.certificates",
                        "Zaświadczenia i dokumenty (np. PIT-11)",
                        Group: "Polska",
                        Fields: [KnownFields.Employee, KnownFields.DocumentType]
                    ),
                    new Subcategory(
                        "hr-poland.pl.medical",
                        "Badania lekarskie i szkolenia BHP",
                        Group: "Polska",
                        Fields: [KnownFields.Employee, KnownFields.ExamDate]
                    ),
                    new Subcategory(
                        "hr-poland.pl.benefits",
                        "Benefity (opieka medyczna, karta sportowa)",
                        Group: "Polska",
                        Fields: [KnownFields.Employee]
                    ),
                    new Subcategory(
                        "hr-poland.pl.offboarding",
                        "Rozwiązanie umowy i świadectwo pracy",
                        Group: "Polska",
                        Fields: [KnownFields.Employee, KnownFields.EndDate]
                    ),
                    new Subcategory(
                        "hr-poland.pl.onboarding",
                        "Nowy pracownik",
                        Group: "Polska",
                        Fields:
                        [
                            KnownFields.FirstName,
                            KnownFields.LastName,
                            KnownFields.Email,
                            KnownFields.Department,
                            KnownFields.Country,
                            KnownFields.IdentityDocument,
                            KnownFields.IdentityNumber,
                            KnownFields.StartDate,
                        ]
                    ),
                    new Subcategory("hr-poland.feedback", "Feedback"),
                ],
                Department: "Kadry"
            ),
            new Category(
                "legalization",
                "Legalizacja",
                [
                    new Subcategory("legalization.pesel", "Nadanie numeru PESEL"),
                    new Subcategory("legalization.lodging", "Nocleg tymczasowy (przed mieszkaniem)"),
                    new Subcategory("legalization.housing", "Mieszkanie i zameldowanie"),
                    new Subcategory("legalization.pup", "Dokumenty do PUP (oświadczenie o powierzeniu pracy)"),
                    new Subcategory("legalization.starosty", "Zgłoszenie do starostwa"),
                    new Subcategory("legalization.work-permit", "Zezwolenie na pracę"),
                    new Subcategory("legalization.residence", "Karta pobytu i wiza"),
                    new Subcategory("legalization.feedback", "Feedback"),
                ],
                Department: "Kadry"
            ),
        ]);

    public Category? FindCategory(string? key) => Categories.FirstOrDefault(c => c.Key == key);

    public (Category Category, Subcategory Subcategory)? FindSubcategory(string? key)
    {
        foreach (var category in Categories)
        {
            var sub = category.Subcategories.FirstOrDefault(s => s.Key == key);
            if (sub is not null)
                return (category, sub);
        }
        return null;
    }

    /// <summary>Display names for a stored ticket - also for a retired subcategory or an unknown key.</summary>
    public (string Category, string Subcategory) NamesFor(string categoryKey, string subcategoryKey)
    {
        var category = FindCategory(categoryKey);
        var sub = category?.Subcategories.FirstOrDefault(s => s.Key == subcategoryKey);
        return (category?.Name ?? categoryKey, sub?.DisplayName ?? subcategoryKey);
    }

    /// <summary>For a new ticket: the subcategory exists, belongs to the category and is not retired.</summary>
    public void EnsureSelectable(string? categoryKey, string? subcategoryKey)
    {
        var category = FindCategory(categoryKey) ?? throw new DomainException("Wybierz kategorię z listy.");
        var sub =
            category.Subcategories.FirstOrDefault(s => s.Key == subcategoryKey)
            ?? throw new DomainException("Wybierz podkategorię należącą do wybranej kategorii.");
        if (!sub.Active)
            throw new DomainException($"Podkategoria „{sub.Name}” nie przyjmuje już nowych zgłoszeń.");
    }
}
