using System.Globalization;

namespace Halcyon.Domain.ValueObjects;

/// <summary>
/// Money in a report field: at most two decimal places, below 10^12 in either direction. Typed as people write it
/// ("1 234,56", "1234.5"), stored invariant with two decimals (<see cref="Stored"/>: "1234.56").
/// </summary>
public sealed record Amount
{
    private const decimal Max = 1_000_000_000_000m;
    private const string DefaultField = "Kwota";

    public static string InvalidMessage(string field = DefaultField) =>
        TextRules.Field(field, "musi być kwotą, np. 1 234,56 (najwyżej 2 miejsca po przecinku).");

    private Amount(decimal value) => Value = value;

    public decimal Value { get; }

    public string Stored => Value.ToString("0.00", CultureInfo.InvariantCulture);

    public static Amount Create(string? value, string field = DefaultField)
    {
        var (amount, error) = TryCreate(value, field);
        return error is not null ? throw new DomainException(error) : amount!;
    }

    /// <summary>Spaces (also non-breaking) as thousands separators, a comma or a dot as the decimal point.</summary>
    public static (Amount? value, string? error) TryCreate(string? value, string field = DefaultField)
    {
        var compact = new string([.. (value ?? "").Where(c => !char.IsWhiteSpace(c) && c != ' ')]).Replace(',', '.');
        if (
            !decimal.TryParse(
                compact,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount
            )
            || decimal.Round(amount, 2) != amount
            || Math.Abs(amount) >= Max
        )
            return (null, InvalidMessage(field));
        return (new Amount(amount), null);
    }

    public override string ToString() => Stored;
}
