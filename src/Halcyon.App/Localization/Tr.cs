using System.Text.RegularExpressions;
using Halcyon.Domain.Categories;
using Halcyon.Domain.Localization;

namespace Halcyon.App.Localization;

/// <summary>
/// The UI's texts in two languages (ADR-0012), used with <c>@using static Halcyon.App.Localization.Tr</c>:
/// <list type="bullet">
/// <item><see cref="T"/> - a text of the UI written in both languages where it is used: <c>@T("Pula", "Pool")</c>.</item>
/// <item><see cref="N(string?)"/> - a name that comes from the domain's catalog (categories, topics, report fields and their
/// choices, priorities, months): the domain keeps the Polish name, stored keys never change, English is for display.</item>
/// <item><see cref="Err"/> - a refusal of the domain (<c>DomainException</c>), written in Polish where the rule lives.</item>
/// </list>
/// The data people typed (ticket titles, descriptions, names) is shown as it is.
/// </summary>
public static partial class Tr
{
    public static string T(string pl, string en) => Language.IsEnglish ? en : pl;

    /// <summary>A catalog name in the UI's language; a name the dictionary does not know is shown as it is.</summary>
    public static string N(string? pl) =>
        pl is null ? ""
        : Language.IsEnglish && EnglishText.English.TryGetValue(pl, out var en) ? en
        : pl;

    /// <summary>"Belgia · Umowa…" for a grouped topic, in the UI's language.</summary>
    public static string N(Subcategory subcategory) =>
        subcategory.Group is null ? N(subcategory.Name) : $"{N(subcategory.Group)} · {N(subcategory.Name)}";

    /// <summary>A refusal in the UI's language: exact messages first, then the ones with a name or a number in them.</summary>
    public static string Err(string? message)
    {
        if (message is null || !Language.IsEnglish)
            return message ?? "";
        if (EnglishText.Messages.TryGetValue(message, out var exact))
            return exact;
        foreach (var (pattern, english) in Patterns)
        {
            var match = pattern.Match(message);
            if (match.Success)
                return english(match.Groups);
        }
        return message;
    }

    private static string Q(Group group) => $"“{N(group.Value)}”";

    private static readonly (Regex Pattern, Func<GroupCollection, string> English)[] Patterns =
    [
        (FileEmpty(), g => $"The file {Q(g[1])} is empty."),
        (FileTooBig(), g => $"The file {Q(g[1])} is too big ({g[2].Value}). The limit is {g[3].Value}."),
        (FileType(), g => $"The file {Q(g[1])} has a type that is not allowed. Allowed: {g[2].Value}."),
        (TopicClosed(), g => $"The topic {Q(g[1])} no longer takes new tickets."),
        (FieldMax(), g => $"{Q(g[1])} can have at most {g[2].Value} characters."),
        (FieldEmail(), g => $"{Q(g[1])} must be an e-mail address."),
        (FieldDate(), g => $"{Q(g[1])} must be a date (yyyy-mm-dd)."),
        (FieldCount(), g => $"{Q(g[1])} must be a number from 1 to {g[2].Value}."),
        (FieldAmount(), g => $"{Q(g[1])} must be an amount, e.g. 1 234,56 (at most 2 decimal places)."),
        (FieldIban(), g => $"{Q(g[1])} is not a valid account number (IBAN)."),
        (FieldTicket(), g => $"{Q(g[1])} must be a ticket number, e.g. HAL-1A2B3C4D."),
        (FieldRequired(), g => $"{Q(g[1])} is required."),
        (FieldForeign(), g => $"The field {Q(g[1])} does not belong to this topic."),
        (FieldFill(), g => $"Fill in {Q(g[1])}."),
        (FieldNotListed(), g => $"{Q(g[1])} is not on the list of {Q(g[2])}."),
        (FieldPick(), g => $"Pick a value of {Q(g[1])} from the list."),
        (PickFromDepartment(), g => $"Pick a person from the {N(g[1].Value)} department."),
        (AlreadyTaken(), g => $"The ticket is already being handled by: {g[1].Value}."),
        (TooManyAttachments(), g => $"A ticket can have at most {g[1].Value} attachments."),
        (InThePool(), g => $"The ticket is in the pool - take it first to {Action(g[1].Value)}."),
        (OnlyAssignee(), g => $"Only the agent handling the ticket ({g[1].Value}) can {Action(g[2].Value)}."),
        (
            Faster(),
            g => $"Someone was faster: the ticket is now being handled by: {g[1].Value}. The list has been refreshed."
        ),
    ];

    private static string Action(string pl) =>
        pl switch
        {
            "wskazać osobę realizującą" => "choose the person who does the work",
            "przekazać" => "hand it over",
            "oddać do puli" => "return it to the pool",
            "oznaczyć jako zrealizowane" => "mark it as resolved",
            _ => pl,
        };

    [GeneratedRegex("^Plik „(.+)” jest pusty\\.$")]
    private static partial Regex FileEmpty();

    [GeneratedRegex("^Plik „(.+)” jest za duży \\((.+)\\)\\. Limit to (.+)\\.$")]
    private static partial Regex FileTooBig();

    [GeneratedRegex("^Plik „(.+)” ma niedozwolony typ\\. Dozwolone: (.+)\\.$")]
    private static partial Regex FileType();

    [GeneratedRegex("^Podkategoria „(.+)” nie przyjmuje już nowych zgłoszeń\\.$")]
    private static partial Regex TopicClosed();

    [GeneratedRegex("^Pole „(.+)” może mieć najwyżej (\\d+) znaków\\.$")]
    private static partial Regex FieldMax();

    [GeneratedRegex("^Pole „(.+)” musi być adresem e-mail\\.$")]
    private static partial Regex FieldEmail();

    [GeneratedRegex("^Pole „(.+)” musi być datą \\(rrrr-mm-dd\\)\\.$")]
    private static partial Regex FieldDate();

    [GeneratedRegex("^Pole „(.+)” musi być liczbą od 1 do (\\d+)\\.$")]
    private static partial Regex FieldCount();

    [GeneratedRegex("^Pole „(.+)” musi być kwotą, np\\. .*$")]
    private static partial Regex FieldAmount();

    [GeneratedRegex("^Pole „(.+)” nie jest poprawnym numerem rachunku \\(IBAN\\)\\.$")]
    private static partial Regex FieldIban();

    [GeneratedRegex("^Pole „(.+)” musi być numerem zgłoszenia, np\\. HAL-1A2B3C4D\\.$")]
    private static partial Regex FieldTicket();

    [GeneratedRegex("^Pole „(.+)” jest wymagane\\.$")]
    private static partial Regex FieldRequired();

    [GeneratedRegex("^Pole „(.+)” nie należy do tej podkategorii\\.$")]
    private static partial Regex FieldForeign();

    [GeneratedRegex("^Uzupełnij pole „(.+)”\\.$")]
    private static partial Regex FieldFill();

    [GeneratedRegex("^„(.+)” nie jest na liście pola „(.+)”\\.$")]
    private static partial Regex FieldNotListed();

    [GeneratedRegex("^Wybierz wartość pola „(.+)” z listy\\.$")]
    private static partial Regex FieldPick();

    [GeneratedRegex("^Wybierz osobę z działu (.+)\\.$")]
    private static partial Regex PickFromDepartment();

    [GeneratedRegex("^Zgłoszenie jest już w realizacji u: (.+)\\.$")]
    private static partial Regex AlreadyTaken();

    [GeneratedRegex("^Zgłoszenie może mieć najwyżej (\\d+) załączników\\.$")]
    private static partial Regex TooManyAttachments();

    [GeneratedRegex("^Zgłoszenie jest w puli - najpierw trzeba je przejąć, żeby (.+)\\.$")]
    private static partial Regex InThePool();

    [GeneratedRegex("^Tylko agent realizujący zgłoszenie \\((.*)\\) może je (.+)\\.$")]
    private static partial Regex OnlyAssignee();

    [GeneratedRegex("^Ktoś był szybszy: zgłoszenie jest teraz w realizacji u: (.+)\\. Lista została odświeżona\\.$")]
    private static partial Regex Faster();
}
