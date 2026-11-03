using System.Globalization;
using System.Net.Mail;

namespace Halcyon.Domain.Categories;

public enum FieldKind
{
    Text,
    Date,
    Email,
    Choice,

    /// <summary>Money: stored as "1234.56" (invariant, two decimals); "1 234,56" and "1234.5" are accepted.</summary>
    Amount,

    /// <summary>A bank account: an IBAN with its check digits, or a Polish 26-digit account number (then "PL" is added).</summary>
    Iban,

    /// <summary>A whole count from 1 (how many people), stored without separators.</summary>
    Count,

    /// <summary>Several options at once (campaign channels), stored as "Facebook, TikTok" in the list's order.</summary>
    MultiChoice,

    /// <summary>Another ticket's number, "HAL-" and 8 hex digits, stored in capitals; the page links it.</summary>
    TicketNumber,
}

/// <summary>
/// An extra piece of data a subcategory collects for the reports (spec 008): a Limosa number, the dates of a
/// posting, a new employee's department. Like the categories it is code: the key is stored on tickets, so it is never
/// renamed or reused for something else - one key means one thing in every subcategory that has it.
/// </summary>
/// <param name="Options">The values of a <see cref="FieldKind.Choice"/>.</param>
/// <param name="Favorites">Options the form offers first, above the rest (they are in <see cref="Options"/> too).</param>
/// <param name="Required">Must be given when the ticket is reported (and cannot be cleared later). The one property a
/// subcategory may set differently for the same key (<c>KnownFields.Position with { Required = false }</c>).</param>
public sealed record FieldDefinition(
    string Key,
    string Label,
    FieldKind Kind = FieldKind.Text,
    IReadOnlyList<string>? Options = null,
    bool Required = false,
    string? Placeholder = null,
    IReadOnlyList<string>? Favorites = null
);

/// <summary>The rules of the values: trimmed, empty means "not given", only the subcategory's own fields.</summary>
public static class ReportFields
{
    public const int ValueMax = 200;

    /// <summary>Dates are stored as <c>yyyy-MM-dd</c>: a calendar day, not an instant, and Excel reads it as a date.</summary>
    public const string DateFormat = "yyyy-MM-dd";

    public static IReadOnlyDictionary<string, string> Check(
        IReadOnlyList<FieldDefinition> definitions,
        IReadOnlyDictionary<string, string?>? values
    )
    {
        values ??= new Dictionary<string, string?>();
        foreach (var key in values.Keys)
            if (definitions.All(d => d.Key != key))
                throw new DomainException($"Pole „{key}” nie należy do tej podkategorii.");

        var result = new Dictionary<string, string>();
        foreach (var definition in definitions)
        {
            var value = values.GetValueOrDefault(definition.Key)?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                if (definition.Required)
                    throw new DomainException($"Uzupełnij pole „{definition.Label}”.");
                continue;
            }
            result[definition.Key] = Normalize(definition, value);
        }
        CheckIdentity(result);
        return result;
    }

    public const string IdentityDocumentKey = "identity-document";
    public const string IdentityNumberKey = "identity-number";
    public const string Pesel = "PESEL";
    public const string IdCard = "Dowód osobisty";
    public const string NoDocuments = "Brak dokumentów";

    /// <summary>The document and its number go together: a PESEL must be a valid one, "no documents" has no number.</summary>
    private static void CheckIdentity(Dictionary<string, string> values)
    {
        if (!values.TryGetValue(IdentityDocumentKey, out var document))
        {
            if (values.ContainsKey(IdentityNumberKey))
                throw new DomainException("Wybierz rodzaj dokumentu, do którego należy numer.");
            return;
        }
        values.TryGetValue(IdentityNumberKey, out var number);
        switch (document)
        {
            case NoDocuments when number is not null:
                throw new DomainException("Przy „Brak dokumentów” nie podaje się numeru dokumentu.");
            case Pesel when number is null:
                throw new DomainException("Podaj numer PESEL.");
            case Pesel when !IsValidPesel(number):
                throw new DomainException("Numer PESEL jest niepoprawny (11 cyfr z poprawną cyfrą kontrolną).");
            case IdCard when number is null:
                throw new DomainException("Podaj numer dowodu osobistego.");
            case IdCard:
                values[IdentityNumberKey] = number.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
                break;
        }
    }

    /// <summary>11 digits, the last one the check digit of weights 1-3-7-9.</summary>
    public static bool IsValidPesel(string value)
    {
        if (value.Length != 11 || !value.All(char.IsAsciiDigit))
            return false;
        int[] weights = [1, 3, 7, 9, 1, 3, 7, 9, 1, 3];
        var sum = weights.Select((w, i) => w * (value[i] - '0')).Sum();
        return (10 - sum % 10) % 10 == value[10] - '0';
    }

    public const decimal AmountMax = 1_000_000_000_000m;

    public const int CountMax = 10_000;

    public const string MultiSeparator = ", ";

    /// <summary>The options of a stored (or typed) multi-choice value.</summary>
    public static IReadOnlyList<string> SplitMulti(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Spaces (also non-breaking) as thousands separators, a comma or a dot as the decimal point.</summary>
    public static decimal? ParseAmount(string value)
    {
        var compact = new string([.. value.Where(c => !char.IsWhiteSpace(c) && c != '\u00A0')]).Replace(',', '.');
        if (
            !decimal.TryParse(
                compact,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount
            )
        )
            return null;
        if (decimal.Round(amount, 2) != amount || Math.Abs(amount) >= AmountMax)
            return null;
        return amount;
    }

    /// <summary>Upper case without spaces, the check digits valid (ISO 13616, mod 97); null when it is not an IBAN.</summary>
    public static string? NormalizeIban(string value)
    {
        var iban = new string([.. value.Where(c => !char.IsWhiteSpace(c))]).ToUpperInvariant();
        if (iban.Length == 26 && iban.All(char.IsAsciiDigit))
            iban = "PL" + iban;
        if (
            iban.Length is < 15 or > 34
            || !char.IsAsciiLetter(iban[0])
            || !char.IsAsciiLetter(iban[1])
            || !iban.All(char.IsAsciiLetterOrDigit)
        )
            return null;
        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            var digits = char.IsAsciiDigit(c) ? c - '0' : c - 'A' + 10;
            remainder = digits >= 10 ? (remainder * 100 + digits) % 97 : (remainder * 10 + digits) % 97;
        }
        return remainder == 1 ? iban : null;
    }

    private static string Normalize(FieldDefinition definition, string value)
    {
        if (value.Length > ValueMax)
            throw new DomainException($"Pole „{definition.Label}” może mieć najwyżej {ValueMax} znaków.");

        switch (definition.Kind)
        {
            case FieldKind.Date:
                if (
                    !DateOnly.TryParseExact(
                        value,
                        DateFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var date
                    )
                )
                    throw new DomainException($"Pole „{definition.Label}” musi być datą (rrrr-mm-dd).");
                return date.ToString(DateFormat, CultureInfo.InvariantCulture);
            case FieldKind.Email:
                if (!MailAddress.TryCreate(value, out var address) || address.Address != value)
                    throw new DomainException($"Pole „{definition.Label}” musi być adresem e-mail.");
                return value;
            case FieldKind.Amount:
                return ParseAmount(value) is { } amount
                    ? amount.ToString("0.00", CultureInfo.InvariantCulture)
                    : throw new DomainException(
                        $"Pole „{definition.Label}” musi być kwotą, np. 1 234,56 (najwyżej 2 miejsca po przecinku)."
                    );
            case FieldKind.Iban:
                return NormalizeIban(value)
                    ?? throw new DomainException(
                        $"Pole „{definition.Label}” nie jest poprawnym numerem rachunku (IBAN)."
                    );
            case FieldKind.Count:
                return
                    int.TryParse(
                        value.Replace(" ", "", StringComparison.Ordinal),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var count
                    ) && count is >= 1 and <= CountMax
                    ? count.ToString(CultureInfo.InvariantCulture)
                    : throw new DomainException($"Pole „{definition.Label}” musi być liczbą od 1 do {CountMax}.");
            case FieldKind.MultiChoice:
                var picked = SplitMulti(value);
                var unknown = picked.FirstOrDefault(p =>
                    definition.Options?.Any(o => string.Equals(o, p, StringComparison.OrdinalIgnoreCase)) != true
                );
                if (unknown is not null)
                    throw new DomainException($"„{unknown}” nie jest na liście pola „{definition.Label}”.");
                return string.Join(
                    MultiSeparator,
                    definition.Options!.Where(o => picked.Contains(o, StringComparer.OrdinalIgnoreCase))
                );
            case FieldKind.TicketNumber:
                var number = value.ToUpperInvariant();
                return
                    number.Length == 12
                    && number.StartsWith("HAL-", StringComparison.Ordinal)
                    && number[4..].All(char.IsAsciiHexDigit)
                    ? number
                    : throw new DomainException(
                        $"Pole „{definition.Label}” musi być numerem zgłoszenia, np. HAL-1A2B3C4D."
                    );
            case FieldKind.Choice:
                var option =
                    definition.Options?.FirstOrDefault(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase))
                    ?? throw new DomainException($"Wybierz wartość pola „{definition.Label}” z listy.");
                return option;
            default:
                return value;
        }
    }
}
