using System.Text.RegularExpressions;
using Halcyon.Domain.Categories;
using Halcyon.Domain.Localization;

namespace Halcyon.Database.Seed;

/// <summary>
/// The demo data in English (ADR-0012), for a database shown to people who do not read Polish: the same agents, people,
/// keys and states as <see cref="SeedData"/>, with the tickets' titles, descriptions, comments, reasons and free-text
/// report values translated. Names stay as they are, and so do choice values: the domain checks them against the
/// catalog, whose values are Polish, and the UI shows them in English anyway. Chosen with <c>Seed:Language=en</c>.
/// </summary>
public static partial class SeedEnglish
{
    private static SeedTicket Translate(SeedTicket ticket) =>
        ticket with
        {
            Title = Title(ticket.Title),
            Description = Text(ticket.Description),
            Steps = [.. ticket.Steps.Select(step => Step(step, ticket.SubcategoryKey))],
            Fields = ticket.Fields is null ? null : Values(ticket.Fields, ticket.SubcategoryKey),
        };

    private static SeedStep Step(SeedStep step, string subcategoryKey) =>
        step switch
        {
            Comment c => c with { Text = Text(c.Text) },
            HandOver h => h with { Reason = Optional(h.Reason) },
            Return r => r with { Reason = Optional(r.Reason) },
            ChangePriority p => p with { Reason = Optional(p.Reason) },
            AssignHandler a => a with { Reason = Optional(a.Reason) },
            FillFields f => f with { Values = Values(f.Values, subcategoryKey) },
            _ => step,
        };

    /// <summary>Free text only: a choice's value is the catalog's and stays Polish.</summary>
    private static Dictionary<string, string?> Values(
        IReadOnlyDictionary<string, string?> values,
        string subcategoryKey
    )
    {
        var fields = CategoryCatalog.Default.FindSubcategory(subcategoryKey)?.Subcategory.ReportFields ?? [];
        return values.ToDictionary(
            v => v.Key,
            v =>
                v.Value is not null && fields.FirstOrDefault(f => f.Key == v.Key)?.Kind == FieldKind.Text
                    ? Text(v.Value)
                    : v.Value
        );
    }

    /// <summary>A generated title is "{opening}: {topic} #{n}"; a hand-written one is in the dictionary.</summary>
    private static string Title(string title)
    {
        var match = GeneratedTitle().Match(title);
        if (match.Success && Openings.TryGetValue(match.Groups[1].Value, out var opening))
        {
            var topic = string.Join(" · ", match.Groups[2].Value.Split(" · ").Select(Name));
            return $"{opening}: {topic} #{match.Groups[3].Value}";
        }
        return Text(title);
    }

    private static string Name(string polish) => EnglishText.English.GetValueOrDefault(polish, polish);

    private static string Text(string polish) => Texts.GetValueOrDefault(polish, polish);

    private static string? Optional(string? polish) => polish is null ? null : Text(polish);

    [GeneratedRegex("^(Prośba|Pytanie|Pilne|Do wyjaśnienia|Zgłoszenie|Proszę o pomoc): (.+) #(\\d+)$")]
    private static partial Regex GeneratedTitle();

    private static readonly Dictionary<string, string> Openings = new()
    {
        ["Prośba"] = "Request",
        ["Pytanie"] = "Question",
        ["Pilne"] = "Urgent",
        ["Do wyjaśnienia"] = "To clarify",
        ["Zgłoszenie"] = "Issue",
        ["Proszę o pomoc"] = "Help needed",
    };

    private static readonly Dictionary<string, string> Texts = new()
    {
        // Generated tickets.
        ["Zgłoszenie z danych przykładowych do przeglądania list i stronicowania."] =
            "A sample ticket for browsing the lists and paging.",
        ["Przejmie osoba z dyżuru."] = "The person on duty will take it.",
        ["Sprawdzone, w trakcie załatwiania."] = "Checked, being sorted out.",
        // IT.
        ["Laptop nie ładuje baterii"] = "Laptop does not charge the battery",
        ["Od wczoraj laptop nie ładuje się po podłączeniu zasilacza. Dioda nie świeci."] =
            "Since yesterday the laptop does not charge when the power adapter is plugged in. The LED is off.",
        ["Brak dostępu do VPN"] = "No VPN access",
        ["Nie mogę połączyć się z VPN z domu, komunikat o wygasłym certyfikacie."] =
            "I cannot connect to the VPN from home - a message about an expired certificate.",
        ["CRM wylogowuje co kilka minut"] = "CRM signs me out every few minutes",
        ["Po aktualizacji CRM sesja wygasa co 3-4 minuty, tracimy wpisane notatki."] =
            "Since the CRM update the session expires every 3-4 minutes and we lose the notes we typed.",
        ["Serwer plików nie odpowiada"] = "File server not responding",
        ["Udział sieciowy działu logistyki jest niedostępny dla wszystkich."] =
            "The logistics department's network share is unavailable for everybody.",
        ["Tomasz ma uprawnienia do serwerowni."] = "Tomasz has access to the server room.",
        ["Dysk w macierzy do wymiany, czekamy na kuriera."] =
            "A disk in the array needs replacing, waiting for the courier.",
        ["Drukarka na 2 piętrze drukuje pasy"] = "The printer on the 2nd floor prints stripes",
        ["Wydruki mają szare pasy na całej szerokości."] = "Printouts have grey stripes across the whole width.",
        ["Licencja na program graficzny"] = "Licence for the graphics software",
        ["Prośba o dodatkową licencję dla nowej osoby w zespole."] =
            "Please add a licence for a new person on the team.",
        ["Nowa osoba zaczyna jutro."] = "The new person starts tomorrow.",
        // Legal.
        ["Przegląd umowy NDA z dostawcą"] = "Review of a supplier's NDA",
        ["Dostawca przysłał własny wzór NDA, prośba o przegląd przed podpisem."] =
            "The supplier sent their own NDA template - please review it before we sign.",
        ["Wniosek o usunięcie danych klienta"] = "Request to erase a customer's data",
        ["Klient zażądał usunięcia danych osobowych, prośba o weryfikację podstawy."] =
            "A customer asked for their personal data to be erased - please check the legal basis.",
        ["Opinia w sprawie regulaminu promocji"] = "Opinion on the promotion's terms",
        ["Czy regulamin promocji wymaga zgody na przetwarzanie danych?"] =
            "Do the promotion's terms need consent to process personal data?",
        ["Potrzebujemy pełnej treści regulaminu."] = "We need the full text of the terms.",
        // Marketing.
        ["Kampania na targi branżowe"] = "Campaign for the trade fair",
        ["Potrzebujemy planu kampanii przed targami w przyszłym miesiącu."] =
            "We need a campaign plan before next month's trade fair.",
        ["Zebrałem budżet z zeszłego roku, czekam na listę wystawców."] =
            "I have last year's budget; waiting for the list of exhibitors.",
        ["Lista wystawców jest na dysku działu."] = "The list of exhibitors is on the department's drive.",
        ["Baner na stronę główną"] = "Banner for the home page",
        ["Nowy baner z promocją jesienną w trzech rozmiarach."] = "A new autumn promotion banner in three sizes.",
        ["Wyjeżdżam na szkolenie, niech przejmie ktoś inny."] =
            "I am off to a training course - someone else please take it.",
        ["Post o nowym produkcie"] = "Post about the new product",
        ["Prośba o przygotowanie posta na LinkedIn o premierze produktu."] =
            "Please prepare a LinkedIn post about the product launch.",
        ["Kampania rekrutacyjna: Spawacz MIG/MAG"] = "Recruitment campaign: MIG/MAG welder",
        ["Kampania na spawaczy do Niemiec - zapotrzebowanie z rekrutacji."] =
            "A campaign for welders for Germany - requested by recruitment.",
        ["Kreacje gotowe, startujemy w poniedziałek."] = "The creatives are ready, we start on Monday.",
        ["Kampania na TikToku - praca sezonowa"] = "TikTok campaign - seasonal work",
        ["Kampania wizerunkowa pod pracę sezonową w Holandii, krótkie filmy z pracownikami."] =
            "A brand campaign for seasonal work in the Netherlands, short videos with our workers.",
        // HR abroad.
        ["Limosa dla ekipy na budowę w Antwerpii"] = "Limosa for the crew on the Antwerp site",
        ["Od poniedziałku 4 monterów jedzie do Antwerpii na 6 tygodni. Potrzebne zgłoszenie Limosa i formularze A1."] =
            "From Monday 4 fitters go to Antwerp for 6 weeks. We need the Limosa declaration and A1 forms.",
        ["A1 złożone w ZUS dla 3 osób, czwarty pracownik nie ma aktualnego meldunku."] =
            "A1 filed with ZUS for 3 people; the fourth worker has no current registered address.",
        ["Nie mam dostępu do portalu Limosa - zgłoszenie złoży Kadry."] =
            "I have no access to the Limosa portal - HR will file the declaration.",
        ["Port Antwerpia, terminal 5"] = "Port of Antwerp, terminal 5",
        ["Dopłata do płacy minimalnej w Niemczech"] = "Top-up to the minimum wage in Germany",
        ["Rozliczenie za październik: czy diety liczą się do Mindestlohn? Dwóch kierowców pyta o różnicę."] =
            "October's settlement: do daily allowances count towards the Mindestlohn? Two drivers ask about the difference.",
        ["Hamburg, centrum logistyczne"] = "Hamburg, logistics centre",
        ["Deklaracja SIPSI - montaż w Lyonie"] = "SIPSI declaration - installation in Lyon",
        ["Wyjazd serwisowy do Lyonu w przyszłym tygodniu, potrzebna deklaracja SIPSI przed wyjazdem."] =
            "A service trip to Lyon next week - the SIPSI declaration is needed before we leave.",
        ["Lyon, zakład klienta"] = "Lyon, client's plant",
        ["Anmeldung i A1 - serwis w Monachium"] = "Anmeldung and A1 - service in Munich",
        ["Dwóch serwisantów na 3 miesiące w Monachium. Potrzebny meldunek i A1."] =
            "Two service technicians in Munich for 3 months. Registration and A1 needed.",
        ["Monachium, Werk Nord"] = "Munich, Werk Nord",
        ["Limosa - montaż w Gandawie"] = "Limosa - installation in Ghent",
        ["Jeden monter na 2 tygodnie w Gandawie."] = "One fitter in Ghent for 2 weeks.",
        ["Gandawa, hala B"] = "Ghent, hall B",
        ["SIPSI - szkolenie u klienta w Nantes"] = "SIPSI - training at a client's in Nantes",
        ["Wyjazd szkoleniowy na 3 dni."] = "A 3-day training trip.",
        ["Nantes, biuro klienta"] = "Nantes, client's office",
        // HR Poland.
        ["Korekta urlopu po zwolnieniu L4"] = "Leave correction after sick leave",
        ["Urlop wypoczynkowy przypadł na zwolnienie lekarskie, proszę o przywrócenie 3 dni."] =
            "My annual leave fell on sick leave - please give me back 3 days.",
        ["Duplikat PIT-11 za 2025"] = "Duplicate PIT-11 for 2025",
        ["Zgubiłem PIT-11, potrzebuję duplikatu do wspólnego rozliczenia."] =
            "I lost my PIT-11 and need a duplicate for the joint tax return.",
        ["PIT-11 za 2025"] = "PIT-11 for 2025",
        ["Nowy pracownik: Olena Kovalenko (logistyka)"] = "New employee: Olena Kovalenko (logistics)",
        [
            "Od 16 listopada nowa osoba w magazynie. Proszę o przygotowanie umowy, skierowania na badania i szkolenia BHP."
        ] =
            "A new person in the warehouse from 16 November. Please prepare the contract, the medical referral and the safety training.",
        ["System kadrowo-płacowy nie pozwala mi założyć teczki pracownika."] =
            "The HR and payroll system does not let me create the employee's file.",
        ["Umowa gotowa, czekamy na orzeczenie lekarskie."] =
            "The contract is ready, waiting for the medical certificate.",
        ["Nowy pracownik: Tomáš Novák (utrzymanie ruchu)"] = "New employee: Tomáš Novák (maintenance)",
        ["Elektryk z Ostrawy, zaczyna od poniedziałku. Bez numeru PESEL - wniosek w toku."] =
            "An electrician from Ostrava, starting on Monday. No PESEL number yet - the application is in progress.",
        ["Nowy pracownik: Andrei Popescu (produkcja)"] = "New employee: Andrei Popescu (production)",
        ["Przyjeżdża w przyszłym tygodniu, na razie bez dokumentów - paszport w konsulacie do odbioru."] =
            "Arrives next week, no documents yet - the passport is waiting at the consulate.",
        // Legalisation.
        ["PESEL dla 3 nowych spawaczy z Kolumbii"] = "PESEL for 3 new welders from Colombia",
        [
            "Trzech spawaczy przyjeżdża w środę na wizie D. Potrzebny wniosek o PESEL w urzędzie gminy przed podpisaniem umów."
        ] =
            "Three welders arrive on Wednesday on a D visa. We need a PESEL application at the municipal office before the contracts are signed.",
        ["Termin w urzędzie zarezerwowany na czwartek 9:00, potrzebne paszporty i adres pobytu."] =
            "Appointment booked for Thursday 9:00 - passports and the address of stay are needed.",
        ["Nocleg na pierwsze 2 tygodnie dla ekipy z Filipin"] =
            "Lodging for the first 2 weeks for the crew from the Philippines",
        [
            "5 osób przylatuje w poniedziałek, mieszkanie będzie wolne dopiero od 15. Potrzebny hostel lub kwatera pracownicza blisko zakładu."
        ] =
            "5 people fly in on Monday and the flat is free only from the 15th. We need a hostel or workers' lodging near the plant.",
        ["Umowa najmu i zameldowanie - mieszkanie przy ul. Fabrycznej"] =
            "Lease and registration - the flat on Fabryczna Street",
        ["Mieszkanie dla 4 pracowników z Uzbekistanu: umowa najmu, protokół przekazania i zameldowanie czasowe."] =
            "A flat for 4 workers from Uzbekistan: the lease, the handover report and temporary registration.",
        ["Umowa podpisana, meldunek złożony w urzędzie gminy."] =
            "Lease signed, registration filed at the municipal office.",
        ["Oświadczenie o powierzeniu pracy - 2 kierowców z Gruzji"] = "Work declaration - 2 drivers from Georgia",
        [
            "Proszę o przygotowanie i złożenie oświadczeń w PUP przez praca.gov.pl. Skany paszportów w załączeniu u kierownika."
        ] =
            "Please prepare and file the declarations with the labour office via praca.gov.pl. The manager has the passport scans.",
        ["Powiadomienie starostwa o podjęciu pracy"] = "Notifying the district office of the start of work",
        ["Pracownik z Ukrainy zaczął pracę w poniedziałek - trzeba zgłosić podjęcie pracy w ustawowym terminie."] =
            "A worker from Ukraine started on Monday - the start of work must be reported within the statutory deadline.",
        ["Wniosek o kartę pobytu - przedłużenie przed końcem wizy"] =
            "Residence card application - extension before the visa ends",
        [
            "Wiza pracownika kończy się za 6 tygodni. Potrzebny wniosek o zezwolenie na pobyt czasowy i pracę w urzędzie wojewódzkim."
        ] =
            "The worker's visa ends in 6 weeks. We need an application for a temporary residence and work permit at the voivodeship office.",
        // Finance.
        ["Faktura za paliwo do zaksięgowania"] = "Fuel invoice to book",
        ["Faktura za paliwo do floty z października, termin płatności za tydzień."] =
            "The October fuel invoice for the fleet, payment due in a week.",
        ["Rozliczenie delegacji - Antwerpia"] = "Business trip settlement - Antwerp",
        ["Rozliczenie diet i noclegów z wyjazdu do Antwerpii, paragony w załączniku u kierownika."] =
            "Settlement of allowances and lodging from the Antwerp trip; the manager has the receipts.",
        ["Pilny przelew do dostawcy części"] = "Urgent transfer to a parts supplier",
        ["Dostawca wstrzymał wysyłkę do czasu zapłaty zaległej faktury."] =
            "The supplier has stopped shipping until the overdue invoice is paid.",
        ["Zaliczka na wyjazd do Lyonu"] = "Advance for the Lyon trip",
        ["Zaliczka na noclegi i paliwo dla ekipy serwisowej."] =
            "An advance for lodging and fuel for the service crew.",
        // Recruitment.
        ["12 spawaczy MIG/MAG dla Bau-Technik GmbH"] = "12 MIG/MAG welders for Bau-Technik GmbH",
        [
            "Klient potrzebuje 12 spawaczy do hali w Dortmundzie od 1 grudnia na 3 miesiące. Zakwaterowanie po stronie klienta."
        ] =
            "The client needs 12 welders for a hall in Dortmund from 1 December for 3 months. Accommodation on the client's side.",
        ["Mamy 5 kandydatów z bazy, resztę zbieramy z kampanii."] =
            "We have 5 candidates from our database; the rest come from the campaign.",
        ["Spawacz MIG/MAG"] = "MIG/MAG welder",
        ["Dortmund, hala 3"] = "Dortmund, hall 3",
        ["Rekrutacja: specjalistka ds. księgowości"] = "Recruitment: accounting specialist",
        ["Wakat w dziale finansów po odejściu na emeryturę, start najpóźniej w styczniu."] =
            "A vacancy in finance after a retirement - starting in January at the latest.",
        ["Specjalistka ds. księgowości"] = "Accounting specialist",
        ["8 000-10 000 zł brutto"] = "8 000-10 000 PLN gross",
        ["Rozmowy z kandydatami na magazyn w Holandii"] =
            "Interviews with candidates for a warehouse in the Netherlands",
        ["Umówić rozmowy online z 3 kandydatami na operatora wózka widłowego."] =
            "Book online interviews with 3 candidates for forklift operator.",
        ["Operator wózka widłowego"] = "Forklift operator",
    };

    // Last: static initializers run in the order of the file, and this one needs Openings and Texts.
    public static SeedDefinition Definition { get; } =
        new(SeedData.Agents, SeedData.People, [.. SeedData.Tickets.Select(Translate)]);
}
