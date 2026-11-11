namespace Halcyon.Domain.ValueObjects;

/// <summary>
/// A phone number as people write it ("+48 600 100 101", "(22) 555-01-02"): digits with an optional leading "+",
/// spaces, dashes and brackets; 7 to 15 digits (E.164 has at most 15). Stored as written, with single spaces.
/// </summary>
public sealed record PhoneNumber
{
    private const int MinDigits = 7;
    private const int MaxDigits = 15;
    public const string RequiredMessage = "Pole „Telefon” jest wymagane.";
    public const string InvalidMessage =
        "Pole „Telefon” musi być numerem telefonu: 7-15 cyfr, opcjonalnie „+” na początku, spacje, myślniki i nawiasy.";

    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    /// <summary>What a <c>tel:</c> link dials: the "+" and the digits.</summary>
    public string Dial => new([.. Value.Where(c => char.IsAsciiDigit(c) || c == '+')]);

    public static PhoneNumber Create(string? value)
    {
        var (phone, error) = TryCreate(value);
        return error is not null ? throw new DomainException(error) : phone!;
    }

    public static (PhoneNumber? value, string? error) TryCreate(string? value)
    {
        var compact = string.Join(' ', (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (compact.Length == 0)
            return (null, RequiredMessage);
        var digits = compact.Count(char.IsAsciiDigit);
        var allowed = compact.Select(
            (c, i) => char.IsAsciiDigit(c) || c is ' ' or '-' or '(' or ')' || (c == '+' && i == 0)
        );
        return allowed.All(ok => ok) && digits is >= MinDigits and <= MaxDigits
            ? (new PhoneNumber(compact), null)
            : (null, InvalidMessage);
    }

    public static PhoneNumber FromStored(string value) => new(value);

    public override string ToString() => Value;
}
