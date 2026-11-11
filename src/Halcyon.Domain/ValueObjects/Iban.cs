namespace Halcyon.Domain.ValueObjects;

/// <summary>
/// A bank account: an IBAN with valid check digits (ISO 13616, mod 97), stored in capitals without spaces. A Polish
/// 26-digit account number is accepted and gets "PL" in front.
/// </summary>
public sealed record Iban
{
    private const string DefaultField = "Numer rachunku";

    public static string InvalidMessage(string field = DefaultField) =>
        TextRules.Field(field, "nie jest poprawnym numerem rachunku (IBAN).");

    private Iban(string value) => Value = value;

    public string Value { get; }

    public static Iban Create(string? value, string field = DefaultField)
    {
        var (iban, error) = TryCreate(value, field);
        return error is not null ? throw new DomainException(error) : iban!;
    }

    public static (Iban? value, string? error) TryCreate(string? value, string field = DefaultField)
    {
        var iban = new string([.. (value ?? "").Where(c => !char.IsWhiteSpace(c))]).ToUpperInvariant();
        if (iban.Length == 26 && iban.All(char.IsAsciiDigit))
            iban = "PL" + iban;
        if (
            iban.Length is < 15 or > 34
            || !char.IsAsciiLetter(iban[0])
            || !char.IsAsciiLetter(iban[1])
            || !iban.All(char.IsAsciiLetterOrDigit)
        )
            return (null, InvalidMessage(field));
        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            var digits = char.IsAsciiDigit(c) ? c - '0' : c - 'A' + 10;
            remainder = digits >= 10 ? (remainder * 100 + digits) % 97 : (remainder * 10 + digits) % 97;
        }
        return remainder == 1 ? (new Iban(iban), null) : (null, InvalidMessage(field));
    }

    public override string ToString() => Value;
}
