namespace Halcyon.Domain.ValueObjects;

/// <summary>A Polish PESEL: 11 digits, the last one the check digit of the weights 1-3-7-9.</summary>
public sealed record Pesel
{
    public const string RequiredMessage = "Podaj numer PESEL.";
    public const string InvalidMessage = "Numer PESEL jest niepoprawny (11 cyfr z poprawną cyfrą kontrolną).";

    private static readonly int[] Weights = [1, 3, 7, 9, 1, 3, 7, 9, 1, 3];

    private Pesel(string value) => Value = value;

    public string Value { get; }

    public static Pesel Create(string? value)
    {
        var (pesel, error) = TryCreate(value);
        return error is not null ? throw new DomainException(error) : pesel!;
    }

    public static (Pesel? value, string? error) TryCreate(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            return (null, RequiredMessage);
        if (trimmed.Length != 11 || !trimmed.All(char.IsAsciiDigit))
            return (null, InvalidMessage);
        var sum = Weights.Select((w, i) => w * (trimmed[i] - '0')).Sum();
        return (10 - sum % 10) % 10 == trimmed[10] - '0' ? (new Pesel(trimmed), null) : (null, InvalidMessage);
    }

    public override string ToString() => Value;
}
