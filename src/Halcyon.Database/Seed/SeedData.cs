using Halcyon.Domain.Tickets;

namespace Halcyon.Database.Seed;

public sealed record SeedAgent(string FirstName, string LastName, string Phone, string Email);

public sealed record SeedPerson(string FirstName, string LastName, string Department, string Email, string? Phone);

/// <summary>A step after reporting, at an offset from the first run's "now".</summary>
public abstract record SeedStep(TimeSpan Ago);

public sealed record Take(string AgentEmail, TimeSpan Ago) : SeedStep(Ago);

public sealed record HandOver(string FromEmail, string ToEmail, string? Reason, TimeSpan Ago) : SeedStep(Ago);

public sealed record Return(string AgentEmail, string? Reason, TimeSpan Ago) : SeedStep(Ago);

public sealed record ChangePriority(string AgentEmail, Priority To, string? Reason, TimeSpan Ago) : SeedStep(Ago);

public sealed record Resolve(string AgentEmail, TimeSpan Ago) : SeedStep(Ago);

public sealed record Comment(string AgentEmail, string Text, TimeSpan Ago) : SeedStep(Ago);

/// <summary>The agent names the person who does the work (spec 008).</summary>
public sealed record AssignHandler(string AgentEmail, string PersonEmail, string? Reason, TimeSpan Ago) : SeedStep(Ago);

/// <summary>The whole new set of report fields (spec 008), as the ticket page saves it.</summary>
public sealed record FillFields(string AgentEmail, IReadOnlyDictionary<string, string?> Values, TimeSpan Ago)
    : SeedStep(Ago);

/// <param name="Key">Stable key: the ticket's id comes from it (ADR-0006).</param>
/// <param name="RequesterEmail">A person's e-mail, or null for incognito.</param>
/// <param name="RegisteredByEmail">The agent who wrote it down, or null when the person sent it themselves.</param>
public sealed record SeedTicket(
    string Key,
    string Title,
    string Description,
    string SubcategoryKey,
    Priority Priority,
    TimeSpan ReportedAgo,
    string? RequesterEmail,
    string? RegisteredByEmail,
    IReadOnlyList<SeedStep> Steps,
    IReadOnlyDictionary<string, string?>? Fields = null
);

/// <summary>
/// The demo data (ADR-0006): fictional people, addresses only in <c>company.io</c>, phone numbers from a fictional
/// range. Change it freely - records already in a database are never updated, only new keys are added.
/// </summary>
public static class SeedData
{
    private static TimeSpan H(double hours) => TimeSpan.FromHours(hours);

    private static TimeSpan D(double days) => TimeSpan.FromDays(days);

    private static Dictionary<string, string?> F(params (string Key, string Value)[] values) =>
        values.ToDictionary(v => v.Key, v => (string?)v.Value);

    public static IReadOnlyList<SeedAgent> Agents { get; } =
    [
        new("Anna", "Nowak", "+48 600 100 101", "anna.nowak@company.io"),
        new("Piotr", "Kowalski", "+48 600 100 102", "piotr.kowalski@company.io"),
        new("Katarzyna", "Wiśniewska", "+48 600 100 103", "katarzyna.wisniewska@company.io"),
        new("Tomasz", "Wójcik", "+48 600 100 104", "tomasz.wojcik@company.io"),
        new("Magdalena", "Kamińska", "+48 600 100 105", "magdalena.kaminska@company.io"),
        new("Michał", "Lewandowski", "+48 600 100 106", "michal.lewandowski@company.io"),
        new("Agnieszka", "Zielińska", "+48 600 100 107", "agnieszka.zielinska@company.io"),
        new("Paweł", "Szymański", "+48 600 100 108", "pawel.szymanski@company.io"),
        new("Joanna", "Woźniak", "+48 600 100 109", "joanna.wozniak@company.io"),
        new("Krzysztof", "Dąbrowski", "+48 600 100 110", "krzysztof.dabrowski@company.io"),
    ];

    public static IReadOnlyList<SeedPerson> People { get; } =
    [
        new("Adam", "Mazur", "Sprzedaż", "adam.mazur@company.io", "+48 600 200 201"),
        new("Barbara", "Krawczyk", "Sprzedaż", "barbara.krawczyk@company.io", null),
        new("Cezary", "Piotrowski", "Sprzedaż", "cezary.piotrowski@company.io", "+48 600 200 203"),
        new("Dorota", "Grabowska", "Finanse", "dorota.grabowska@company.io", "+48 600 200 204"),
        new("Emil", "Nowakowski", "Finanse", "emil.nowakowski@company.io", null),
        new("Fryderyka", "Pawłowska", "Finanse", "fryderyka.pawlowska@company.io", "+48 600 200 206"),
        new("Grzegorz", "Michalski", "Kadry", "grzegorz.michalski@company.io", "+48 600 200 207"),
        new("Halina", "Nowicka", "Kadry", "halina.nowicka@company.io", null),
        new("Igor", "Adamczyk", "Logistyka", "igor.adamczyk@company.io", "+48 600 200 209"),
        new("Joanna", "Dudek", "Logistyka", "joanna.dudek@company.io", "+48 600 200 210"),
        new("Kamil", "Zając", "Logistyka", "kamil.zajac@company.io", null),
        new("Lena", "Wieczorek", "Zarząd", "lena.wieczorek@company.io", "+48 600 200 212"),
        new("Marek", "Jabłoński", "Zarząd", "marek.jablonski@company.io", "+48 600 200 213"),
        new("Natalia", "Król", "Obsługa klienta", "natalia.krol@company.io", "+48 600 200 214"),
        new("Oskar", "Majewski", "Obsługa klienta", "oskar.majewski@company.io", null),
        new("Patrycja", "Olszewska", "Obsługa klienta", "patrycja.olszewska@company.io", "+48 600 200 216"),
        new("Robert", "Jaworski", "Produkt", "robert.jaworski@company.io", "+48 600 200 217"),
        new("Sylwia", "Wróbel", "Produkt", "sylwia.wrobel@company.io", null),
        new("Tadeusz", "Malinowski", "Produkt", "tadeusz.malinowski@company.io", "+48 600 200 219"),
        new("Urszula", "Pawlak", "Zakupy", "urszula.pawlak@company.io", "+48 600 200 220"),
        new("Wiktor", "Witkowski", "Zakupy", "wiktor.witkowski@company.io", null),
        new("Zofia", "Walczak", "Administracja", "zofia.walczak@company.io", "+48 600 200 222"),
        new("Artur", "Stępień", "Administracja", "artur.stepien@company.io", "+48 600 200 223"),
        new("Beata", "Górska", "Badania i rozwój", "beata.gorska@company.io", null),
        new("Daniel", "Rutkowski", "Badania i rozwój", "daniel.rutkowski@company.io", "+48 600 200 225"),
        new("Ewa", "Michalak", "Badania i rozwój", "ewa.michalak@company.io", "+48 600 200 226"),
        new("Filip", "Sikora", "Jakość", "filip.sikora@company.io", null),
        new("Gabriela", "Ostrowska", "Jakość", "gabriela.ostrowska@company.io", "+48 600 200 228"),
        new("Henryk", "Baran", "Utrzymanie ruchu", "henryk.baran@company.io", "+48 600 200 229"),
        new("Iwona", "Duda", "Utrzymanie ruchu", "iwona.duda@company.io", "+48 600 200 230"),
        // The recruiters (spec 008): they recruit for the company and for the agency's clients, and order campaigns.
        new("Aleksandra", "Kubiak", "Rekrutacja", "aleksandra.kubiak@company.io", "+48 600 200 231"),
        new("Bożena", "Sadowska", "Rekrutacja", "bozena.sadowska@company.io", "+48 600 200 232"),
        new("Celina", "Mróz", "Rekrutacja", "celina.mroz@company.io", null),
        new("Danuta", "Czerwińska", "Rekrutacja", "danuta.czerwinska@company.io", "+48 600 200 234"),
        new("Elżbieta", "Zawadzka", "Rekrutacja", "elzbieta.zawadzka@company.io", "+48 600 200 235"),
        new("Franciszka", "Kalinowska", "Rekrutacja", "franciszka.kalinowska@company.io", null),
        new("Hanna", "Wysocka", "Rekrutacja", "hanna.wysocka@company.io", "+48 600 200 237"),
        new("Izabela", "Kozłowska", "Rekrutacja", "izabela.kozlowska@company.io", "+48 600 200 238"),
        new("Justyna", "Sobczak", "Rekrutacja", "justyna.sobczak@company.io", null),
        new("Karolina", "Wilk", "Rekrutacja", "karolina.wilk@company.io", "+48 600 200 240"),
    ];

    public const int GeneratedCount = 200;

    private static IReadOnlyList<SeedTicket> Curated { get; } =
    [
        new(
            "seed-01-laptop",
            "Laptop nie ładuje baterii",
            "Od wczoraj laptop nie ładuje się po podłączeniu zasilacza. Dioda nie świeci.",
            "it.hardware",
            Priority.Medium,
            H(2),
            "adam.mazur@company.io",
            "anna.nowak@company.io",
            []
        ),
        new(
            "seed-02-vpn",
            "Brak dostępu do VPN",
            "Nie mogę połączyć się z VPN z domu, komunikat o wygasłym certyfikacie.",
            "it.outage",
            Priority.Critical,
            H(1),
            null,
            "piotr.kowalski@company.io",
            []
        ),
        new(
            "seed-03-nda",
            "Przegląd umowy NDA z dostawcą",
            "Dostawca przysłał własny wzór NDA, prośba o przegląd przed podpisem.",
            "legal.contracts",
            Priority.High,
            H(10),
            "dorota.grabowska@company.io",
            "katarzyna.wisniewska@company.io",
            []
        ),
        new(
            "seed-04-crm",
            "CRM wylogowuje co kilka minut",
            "Po aktualizacji CRM sesja wygasa co 3-4 minuty, tracimy wpisane notatki.",
            "it.application",
            Priority.Critical,
            H(2.5),
            "natalia.krol@company.io",
            "tomasz.wojcik@company.io",
            [new Take("anna.nowak@company.io", H(2.4))]
        ),
        new(
            "seed-05-campaign",
            "Kampania na targi branżowe",
            "Potrzebujemy planu kampanii przed targami w przyszłym miesiącu.",
            "marketing.campaign",
            Priority.Low,
            D(2),
            "lena.wieczorek@company.io",
            "magdalena.kaminska@company.io",
            [
                new Take("piotr.kowalski@company.io", D(1.9)),
                new Comment(
                    "piotr.kowalski@company.io",
                    "Zebrałem budżet z zeszłego roku, czekam na listę wystawców.",
                    D(1.5)
                ),
                new Comment("magdalena.kaminska@company.io", "Lista wystawców jest na dysku działu.", D(1)),
            ]
        ),
        new(
            "seed-06-server",
            "Serwer plików nie odpowiada",
            "Udział sieciowy działu logistyki jest niedostępny dla wszystkich.",
            "it.outage",
            Priority.Critical,
            H(5),
            "igor.adamczyk@company.io",
            "michal.lewandowski@company.io",
            [
                new Take("katarzyna.wisniewska@company.io", H(4.8)),
                new HandOver(
                    "katarzyna.wisniewska@company.io",
                    "tomasz.wojcik@company.io",
                    "Tomasz ma uprawnienia do serwerowni.",
                    H(4)
                ),
                new Comment("tomasz.wojcik@company.io", "Dysk w macierzy do wymiany, czekamy na kuriera.", H(3)),
            ]
        ),
        new(
            "seed-07-printer",
            "Drukarka na 2 piętrze drukuje pasy",
            "Wydruki mają szare pasy na całej szerokości.",
            "it.hardware",
            Priority.Medium,
            D(3),
            "zofia.walczak@company.io",
            "agnieszka.zielinska@company.io",
            [new Take("magdalena.kaminska@company.io", D(2.5)), new Resolve("magdalena.kaminska@company.io", D(1))]
        ),
        new(
            "seed-08-gdpr",
            "Wniosek o usunięcie danych klienta",
            "Klient zażądał usunięcia danych osobowych, prośba o weryfikację podstawy.",
            "legal.gdpr",
            Priority.High,
            D(1),
            "patrycja.olszewska@company.io",
            "pawel.szymanski@company.io",
            [new Take("michal.lewandowski@company.io", H(20)), new Resolve("michal.lewandowski@company.io", H(2))]
        ),
        new(
            "seed-09-banner",
            "Baner na stronę główną",
            "Nowy baner z promocją jesienną w trzech rozmiarach.",
            "marketing.graphics",
            Priority.Medium,
            H(6),
            "robert.jaworski@company.io",
            "joanna.wozniak@company.io",
            [
                new Take("agnieszka.zielinska@company.io", H(5)),
                new Return("agnieszka.zielinska@company.io", "Wyjeżdżam na szkolenie, niech przejmie ktoś inny.", H(4)),
            ]
        ),
        new(
            "seed-10-license",
            "Licencja na program graficzny",
            "Prośba o dodatkową licencję dla nowej osoby w zespole.",
            "it.application",
            Priority.Low,
            H(3),
            "sylwia.wrobel@company.io",
            "krzysztof.dabrowski@company.io",
            [new ChangePriority("anna.nowak@company.io", Priority.High, "Nowa osoba zaczyna jutro.", H(2.5))]
        ),
        new(
            "seed-11-social",
            "Post o nowym produkcie",
            "Prośba o przygotowanie posta na LinkedIn o premierze produktu.",
            "marketing.social",
            Priority.Medium,
            TimeSpan.FromMinutes(30),
            "ewa.michalak@company.io",
            null,
            []
        ),
        new(
            "seed-12-opinion",
            "Opinia w sprawie regulaminu promocji",
            "Czy regulamin promocji wymaga zgody na przetwarzanie danych?",
            "legal.opinion",
            Priority.Low,
            D(4),
            null,
            null,
            [new Comment("katarzyna.wisniewska@company.io", "Potrzebujemy pełnej treści regulaminu.", D(3))]
        ),
        new(
            "seed-13-limosa",
            "Limosa dla ekipy na budowę w Antwerpii",
            "Od poniedziałku 4 monterów jedzie do Antwerpii na 6 tygodni. Potrzebne zgłoszenie Limosa i formularze A1.",
            "hr-abroad.be.posting",
            Priority.High,
            H(5),
            "igor.adamczyk@company.io",
            "joanna.wozniak@company.io",
            [
                new Take("joanna.wozniak@company.io", H(4.5)),
                new Comment(
                    "joanna.wozniak@company.io",
                    "A1 złożone w ZUS dla 3 osób, czwarty pracownik nie ma aktualnego meldunku.",
                    H(3)
                ),
                new AssignHandler(
                    "joanna.wozniak@company.io",
                    "grzegorz.michalski@company.io",
                    "Nie mam dostępu do portalu Limosa - zgłoszenie złoży Kadry.",
                    H(2.5)
                ),
            ],
            F(
                ("employee", "Marcin Lis"),
                ("site", "Port Antwerpia, terminal 5"),
                ("period-from", "2026-11-09"),
                ("period-to", "2026-12-18")
            )
        ),
        new(
            "seed-14-mindestlohn",
            "Dopłata do płacy minimalnej w Niemczech",
            "Rozliczenie za październik: czy diety liczą się do Mindestlohn? Dwóch kierowców pyta o różnicę.",
            "hr-abroad.de.payroll",
            Priority.Medium,
            D(1),
            "henryk.baran@company.io",
            null,
            [],
            F(("employee", "Henryk Baran"), ("site", "Hamburg, centrum logistyczne"), ("settlement-month", "2026-10"))
        ),
        new(
            "seed-15-sipsi",
            "Deklaracja SIPSI - montaż w Lyonie",
            "Wyjazd serwisowy do Lyonu w przyszłym tygodniu, potrzebna deklaracja SIPSI przed wyjazdem.",
            "hr-abroad.fr.posting",
            Priority.Critical,
            H(1.5),
            "joanna.dudek@company.io",
            "krzysztof.dabrowski@company.io",
            [],
            F(
                ("employee", "Joanna Dudek"),
                ("site", "Lyon, zakład klienta"),
                ("period-from", "2026-11-10"),
                ("period-to", "2026-11-14")
            )
        ),
        new(
            "seed-16-urlop",
            "Korekta urlopu po zwolnieniu L4",
            "Urlop wypoczynkowy przypadł na zwolnienie lekarskie, proszę o przywrócenie 3 dni.",
            "hr-poland.pl.leave",
            Priority.Low,
            D(2),
            "halina.nowicka@company.io",
            null,
            [new Take("pawel.szymanski@company.io", D(1.8)), new Resolve("pawel.szymanski@company.io", D(1))],
            F(
                ("employee", "Halina Nowicka"),
                ("period-from", "2026-10-27"),
                ("period-to", "2026-10-29"),
                ("zla-number", "ZLA 1234567")
            )
        ),
        new(
            "seed-17-pit",
            "Duplikat PIT-11 za 2025",
            "Zgubiłem PIT-11, potrzebuję duplikatu do wspólnego rozliczenia.",
            "hr-poland.pl.certificates",
            Priority.Medium,
            H(20),
            "cezary.piotrowski@company.io",
            null,
            [],
            F(("employee", "Cezary Piotrowski"), ("document-type", "PIT-11 za 2025"))
        ),
        new(
            "seed-18-pesel",
            "PESEL dla 3 nowych spawaczy z Kolumbii",
            "Trzech spawaczy przyjeżdża w środę na wizie D. Potrzebny wniosek o PESEL w urzędzie gminy przed podpisaniem umów.",
            "legalization.pesel",
            Priority.High,
            H(7),
            "henryk.baran@company.io",
            "joanna.wozniak@company.io",
            [
                new Take("pawel.szymanski@company.io", H(6.5)),
                new Comment(
                    "pawel.szymanski@company.io",
                    "Termin w urzędzie zarezerwowany na czwartek 9:00, potrzebne paszporty i adres pobytu.",
                    H(5)
                ),
            ]
        ),
        new(
            "seed-19-nocleg",
            "Nocleg na pierwsze 2 tygodnie dla ekipy z Filipin",
            "5 osób przylatuje w poniedziałek, mieszkanie będzie wolne dopiero od 15. Potrzebny hostel lub kwatera pracownicza blisko zakładu.",
            "legalization.lodging",
            Priority.Critical,
            H(3),
            "igor.adamczyk@company.io",
            "krzysztof.dabrowski@company.io",
            []
        ),
        new(
            "seed-20-mieszkanie",
            "Umowa najmu i zameldowanie - mieszkanie przy ul. Fabrycznej",
            "Mieszkanie dla 4 pracowników z Uzbekistanu: umowa najmu, protokół przekazania i zameldowanie czasowe.",
            "legalization.housing",
            Priority.Medium,
            D(3),
            "grzegorz.michalski@company.io",
            "pawel.szymanski@company.io",
            [
                new Take("joanna.wozniak@company.io", D(2.8)),
                new Comment("joanna.wozniak@company.io", "Umowa podpisana, meldunek złożony w urzędzie gminy.", D(1.5)),
                new Resolve("joanna.wozniak@company.io", D(1)),
            ]
        ),
        new(
            "seed-21-pup",
            "Oświadczenie o powierzeniu pracy - 2 kierowców z Gruzji",
            "Proszę o przygotowanie i złożenie oświadczeń w PUP przez praca.gov.pl. Skany paszportów w załączeniu u kierownika.",
            "legalization.pup",
            Priority.Medium,
            D(1),
            "joanna.dudek@company.io",
            null,
            []
        ),
        new(
            "seed-22-starostwo",
            "Powiadomienie starostwa o podjęciu pracy",
            "Pracownik z Ukrainy zaczął pracę w poniedziałek - trzeba zgłosić podjęcie pracy w ustawowym terminie.",
            "legalization.starosty",
            Priority.High,
            D(2),
            "halina.nowicka@company.io",
            "anna.nowak@company.io",
            [new Take("krzysztof.dabrowski@company.io", D(1.9))]
        ),
        new(
            "seed-23-karta-pobytu",
            "Wniosek o kartę pobytu - przedłużenie przed końcem wizy",
            "Wiza pracownika kończy się za 6 tygodni. Potrzebny wniosek o zezwolenie na pobyt czasowy i pracę w urzędzie wojewódzkim.",
            "legalization.residence",
            Priority.Low,
            H(12),
            "kamil.zajac@company.io",
            null,
            []
        ),
        new(
            "seed-24-nowy-olena",
            "Nowy pracownik: Olena Kovalenko (logistyka)",
            "Od 16 listopada nowa osoba w magazynie. Proszę o przygotowanie umowy, skierowania na badania i szkolenia BHP.",
            "hr-poland.pl.onboarding",
            Priority.High,
            H(9),
            "igor.adamczyk@company.io",
            "anna.nowak@company.io",
            [
                new Take("pawel.szymanski@company.io", H(8)),
                new AssignHandler(
                    "pawel.szymanski@company.io",
                    "halina.nowicka@company.io",
                    "System kadrowo-płacowy nie pozwala mi założyć teczki pracownika.",
                    H(6)
                ),
                new Comment("pawel.szymanski@company.io", "Umowa gotowa, czekamy na orzeczenie lekarskie.", H(4)),
            ],
            F(
                ("first-name", "Olena"),
                ("last-name", "Kovalenko"),
                ("email", "olena.kovalenko@company.io"),
                ("department", "Logistyka"),
                ("country", "Ukraina"),
                ("identity-document", "PESEL"),
                ("identity-number", "92031501232"),
                ("start-date", "2026-11-16")
            )
        ),
        new(
            "seed-25-nowy-tomas",
            "Nowy pracownik: Tomáš Novák (utrzymanie ruchu)",
            "Elektryk z Ostrawy, zaczyna od poniedziałku. Bez numeru PESEL - wniosek w toku.",
            "hr-poland.pl.onboarding",
            Priority.Medium,
            D(4),
            "henryk.baran@company.io",
            "pawel.szymanski@company.io",
            [new Take("joanna.wozniak@company.io", D(3.9)), new Resolve("joanna.wozniak@company.io", D(2))],
            F(
                ("first-name", "Tomáš"),
                ("last-name", "Novák"),
                ("email", "tomas.novak@company.io"),
                ("department", "Utrzymanie ruchu"),
                ("country", "Czechy"),
                ("identity-document", "Dowód osobisty"),
                ("identity-number", "214587963"),
                ("start-date", "2026-11-02")
            )
        ),
        new(
            "seed-26-nowy-andrei",
            "Nowy pracownik: Andrei Popescu (produkcja)",
            "Przyjeżdża w przyszłym tygodniu, na razie bez dokumentów - paszport w konsulacie do odbioru.",
            "hr-poland.pl.onboarding",
            Priority.Medium,
            H(2),
            "henryk.baran@company.io",
            null,
            [],
            F(
                ("first-name", "Andrei"),
                ("last-name", "Popescu"),
                ("department", "Utrzymanie ruchu"),
                ("country", "Rumunia"),
                ("identity-document", "Brak dokumentów")
            )
        ),
        new(
            "seed-27-anmeldung",
            "Anmeldung i A1 - serwis w Monachium",
            "Dwóch serwisantów na 3 miesiące w Monachium. Potrzebny meldunek i A1.",
            "hr-abroad.de.posting",
            Priority.High,
            D(2),
            "joanna.dudek@company.io",
            "joanna.wozniak@company.io",
            [
                new Take("joanna.wozniak@company.io", D(1.9)),
                new FillFields(
                    "joanna.wozniak@company.io",
                    F(
                        ("employee", "Kamil Zając"),
                        ("site", "Monachium, Werk Nord"),
                        ("period-from", "2026-11-03"),
                        ("period-to", "2027-01-30"),
                        ("a1-number", "A1/2026/004512"),
                        ("anmeldung-number", "MUC-2026-118734")
                    ),
                    D(1)
                ),
            ],
            F(
                ("employee", "Kamil Zając"),
                ("site", "Monachium, Werk Nord"),
                ("period-from", "2026-11-03"),
                ("period-to", "2027-01-30")
            )
        ),
        new(
            "seed-28-limosa-gent",
            "Limosa - montaż w Gandawie",
            "Jeden monter na 2 tygodnie w Gandawie.",
            "hr-abroad.be.posting",
            Priority.Medium,
            D(5),
            "igor.adamczyk@company.io",
            "krzysztof.dabrowski@company.io",
            [
                new Take("krzysztof.dabrowski@company.io", D(4.9)),
                new FillFields(
                    "krzysztof.dabrowski@company.io",
                    F(
                        ("employee", "Igor Adamczyk"),
                        ("site", "Gandawa, hala B"),
                        ("period-from", "2026-10-26"),
                        ("period-to", "2026-11-06"),
                        ("limosa-number", "L1-2026-0458812"),
                        ("a1-number", "A1/2026/004377")
                    ),
                    D(4.5)
                ),
                new Resolve("krzysztof.dabrowski@company.io", D(4.4)),
            ],
            F(
                ("employee", "Igor Adamczyk"),
                ("site", "Gandawa, hala B"),
                ("period-from", "2026-10-26"),
                ("period-to", "2026-11-06")
            )
        ),
        new(
            "seed-29-sipsi-nantes",
            "SIPSI - szkolenie u klienta w Nantes",
            "Wyjazd szkoleniowy na 3 dni.",
            "hr-abroad.fr.posting",
            Priority.Low,
            D(6),
            "sylwia.wrobel@company.io",
            null,
            [
                new Take("joanna.wozniak@company.io", D(5.5)),
                new FillFields(
                    "joanna.wozniak@company.io",
                    F(
                        ("employee", "Sylwia Wróbel"),
                        ("site", "Nantes, biuro klienta"),
                        ("period-from", "2026-11-18"),
                        ("period-to", "2026-11-20"),
                        ("sipsi-number", "SIPSI-2026-77120"),
                        ("a1-number", "A1/2026/004601")
                    ),
                    D(5)
                ),
                new Resolve("joanna.wozniak@company.io", D(4.8)),
            ],
            F(("employee", "Sylwia Wróbel"), ("site", "Nantes, biuro klienta"))
        ),
        new(
            "seed-30-faktura-paliwo",
            "Faktura za paliwo do zaksięgowania",
            "Faktura za paliwo do floty z października, termin płatności za tydzień.",
            "finance.invoices",
            Priority.Medium,
            H(6),
            "igor.adamczyk@company.io",
            "anna.nowak@company.io",
            [new Take("magdalena.kaminska@company.io", H(5))],
            F(
                ("invoice-number", "FV/2026/10/18832"),
                ("contractor", "Orlen Paliwa Sp. z o.o."),
                ("amount", "18 450,27"),
                ("currency", "PLN"),
                ("due-date", "2026-11-10"),
                ("cost-center", "MPK-LOG-01")
            )
        ),
        new(
            "seed-31-delegacja-antwerpia",
            "Rozliczenie delegacji - Antwerpia",
            "Rozliczenie diet i noclegów z wyjazdu do Antwerpii, paragony w załączniku u kierownika.",
            "finance.expenses",
            Priority.Low,
            D(3),
            "joanna.dudek@company.io",
            null,
            [new Take("magdalena.kaminska@company.io", D(2.5)), new Resolve("magdalena.kaminska@company.io", D(1))],
            F(
                ("employee", "Joanna Dudek"),
                ("amount", "742.50"),
                ("currency", "EUR"),
                ("period-from", "2026-10-19"),
                ("period-to", "2026-10-23"),
                ("cost-center", "MPK-SER-02")
            )
        ),
        new(
            "seed-32-przelew-dostawca",
            "Pilny przelew do dostawcy części",
            "Dostawca wstrzymał wysyłkę do czasu zapłaty zaległej faktury.",
            "finance.payments",
            Priority.Critical,
            H(1),
            "urszula.pawlak@company.io",
            "krzysztof.dabrowski@company.io",
            [],
            F(
                ("contractor", "Techparts S.A."),
                ("amount", "36 900,00"),
                ("currency", "PLN"),
                ("due-date", "2026-11-03"),
                ("bank-account", "PL61 1090 1014 0000 0712 1981 2874")
            )
        ),
        new(
            "seed-33-zaliczka",
            "Zaliczka na wyjazd do Lyonu",
            "Zaliczka na noclegi i paliwo dla ekipy serwisowej.",
            "finance.advance",
            Priority.High,
            H(4),
            "joanna.dudek@company.io",
            null,
            [],
            F(("employee", "Joanna Dudek"), ("amount", "1500"), ("currency", "EUR"), ("needed-by", "2026-11-09"))
        ),
        new(
            "seed-34-spawacze-niemcy",
            "12 spawaczy MIG/MAG dla Bau-Technik GmbH",
            "Klient potrzebuje 12 spawaczy do hali w Dortmundzie od 1 grudnia na 3 miesiące. Zakwaterowanie po stronie klienta.",
            "recruitment.client",
            Priority.High,
            D(1),
            "aleksandra.kubiak@company.io",
            "anna.nowak@company.io",
            [
                new Take("katarzyna.wisniewska@company.io", H(20)),
                new Comment(
                    "katarzyna.wisniewska@company.io",
                    "Mamy 5 kandydatów z bazy, resztę zbieramy z kampanii.",
                    H(12)
                ),
            ],
            F(
                ("client", "Bau-Technik GmbH"),
                ("position", "Spawacz MIG/MAG"),
                ("headcount", "12"),
                ("work-country", "Niemcy"),
                ("site", "Dortmund, hala 3"),
                ("period-from", "2026-12-01"),
                ("period-to", "2027-02-28"),
                ("contract-type", "Umowa zlecenie"),
                ("hourly-rate", "17,50"),
                ("currency", "EUR"),
                ("accommodation", "Zapewnia klient")
            )
        ),
        new(
            "seed-35-kampania-spawacze",
            "Kampania rekrutacyjna: Spawacz MIG/MAG",
            "Kampania na spawaczy do Niemiec - zapotrzebowanie z rekrutacji.",
            "marketing.budget",
            Priority.High,
            H(18),
            "aleksandra.kubiak@company.io",
            "anna.nowak@company.io",
            [
                new Take("piotr.kowalski@company.io", H(16)),
                new Comment("piotr.kowalski@company.io", "Kreacje gotowe, startujemy w poniedziałek.", H(6)),
            ],
            F(
                ("channels", "Facebook, TikTok, OLX Praca"),
                ("campaign-goal", "Rekrutacja"),
                ("amount", "4 500"),
                ("currency", "PLN"),
                ("period-from", "2026-11-09"),
                ("period-to", "2026-11-29"),
                ("position", "Spawacz MIG/MAG"),
                ("headcount", "12"),
                ("related-ticket", Ticket.NumberOf(StableId.Ticket("seed-34-spawacze-niemcy")))
            )
        ),
        new(
            "seed-36-ksiegowa",
            "Rekrutacja: specjalistka ds. księgowości",
            "Wakat w dziale finansów po odejściu na emeryturę, start najpóźniej w styczniu.",
            "recruitment.internal",
            Priority.Medium,
            D(2),
            "dorota.grabowska@company.io",
            null,
            [new Take("joanna.wozniak@company.io", D(1.8))],
            F(
                ("position", "Specjalistka ds. księgowości"),
                ("department", "Finanse"),
                ("headcount", "1"),
                ("contract-type", "Umowa o pracę"),
                ("salary-range", "8 000-10 000 zł brutto"),
                ("start-date", "2027-01-04")
            )
        ),
        new(
            "seed-37-rozmowy-magazyn",
            "Rozmowy z kandydatami na magazyn w Holandii",
            "Umówić rozmowy online z 3 kandydatami na operatora wózka widłowego.",
            "recruitment.candidates",
            Priority.Low,
            H(10),
            "celina.mroz@company.io",
            null,
            [],
            F(
                ("position", "Operator wózka widłowego"),
                ("client", "LogiPort B.V."),
                ("candidate", "Marek Wójtowicz"),
                ("interview-date", "2026-11-06")
            )
        ),
        new(
            "seed-38-kampania-tiktok",
            "Kampania na TikToku - praca sezonowa",
            "Kampania wizerunkowa pod pracę sezonową w Holandii, krótkie filmy z pracownikami.",
            "marketing.budget",
            Priority.Medium,
            D(3),
            "hanna.wysocka@company.io",
            null,
            [new Take("agnieszka.zielinska@company.io", D(2.8)), new Resolve("agnieszka.zielinska@company.io", D(1))],
            F(
                ("channels", "Instagram, TikTok"),
                ("campaign-goal", "Wizerunek i zasięg"),
                ("amount", "2 000"),
                ("currency", "PLN"),
                ("period-from", "2026-11-02"),
                ("period-to", "2026-11-30")
            )
        ),
    ];

    /// <summary>How a generated title starts; the rest is the topic, so the title always matches its subcategory.</summary>
    private static readonly string[] Openings =
    [
        "Prośba",
        "Pytanie",
        "Pilne",
        "Do wyjaśnienia",
        "Zgłoszenie",
        "Proszę o pomoc",
    ];

    /// <summary>
    /// Deterministic (fixed seed, stable keys "seed-gen-NNN"): the same 200 tickets on every database, in every team and
    /// state, reported up to 28 days back. Only topics with no required report fields, so no data has to be invented.
    /// </summary>
    private static List<SeedTicket> Generated()
    {
        var random = new Random(2026);
        var subcategories = Domain
            .Categories.CategoryCatalog.Default.Categories.SelectMany(c => c.ActiveSubcategories)
            .Where(sub =>
                sub.ReportFields.All(f => !f.Required) && !sub.Key.EndsWith(".feedback", StringComparison.Ordinal)
            )
            .ToList();
        Priority[] priorities =
        [
            Priority.Low,
            Priority.Medium,
            Priority.Medium,
            Priority.Medium,
            Priority.High,
            Priority.Critical,
        ];
        T Pick<T>(IReadOnlyList<T> list) => list[random.Next(list.Count)];

        var tickets = new List<SeedTicket>();
        for (var n = 1; n <= GeneratedCount; n++)
        {
            var sub = Pick(subcategories);
            var ago = H(0.3 + random.NextDouble() * 28 * 24);
            var requester = random.NextDouble() < 0.15 ? null : Pick(People).Email;
            var registeredBy = random.NextDouble() < 0.3 ? null : Pick(Agents).Email;
            var agent = Pick(Agents).Email;

            var steps = new List<SeedStep>();
            // Older tickets are mostly done, as on a real desk; the pool keeps the recent ones.
            var draw = random.NextDouble();
            var fate = ago.TotalDays > 4 && draw < 0.85 ? 0.65 + draw * 0.35 / 0.85 : random.NextDouble();
            if (fate >= 0.35)
                steps.Add(new Take(agent, ago * 0.9));
            if (fate is >= 0.55 and < 0.65)
            {
                var other = Pick(Agents.Where(a => a.Email != agent).ToList()).Email;
                steps.Add(new HandOver(agent, other, "Przejmie osoba z dyżuru.", ago * 0.7));
            }
            if (fate >= 0.65)
            {
                if (random.NextDouble() < 0.4)
                    steps.Add(new Comment(agent, "Sprawdzone, w trakcie załatwiania.", ago * 0.8));
                steps.Add(new Resolve(agent, ago * (0.1 + random.NextDouble() * 0.6)));
            }

            tickets.Add(
                new SeedTicket(
                    $"seed-gen-{n:000}",
                    $"{Pick(Openings)}: {sub.DisplayName} #{n}",
                    "Zgłoszenie z danych przykładowych do przeglądania list i stronicowania.",
                    sub.Key,
                    Pick(priorities),
                    ago,
                    requester,
                    registeredBy,
                    steps
                )
            );
        }
        return tickets;
    }

    // Last: static initializers run in the order of the file, and this one needs Curated, Openings, Agents and People.
    /// <summary>The hand-written tickets and <see cref="GeneratedCount"/> generated ones (for paging).</summary>
    public static IReadOnlyList<SeedTicket> Tickets { get; } = [.. Curated, .. Generated()];
}
