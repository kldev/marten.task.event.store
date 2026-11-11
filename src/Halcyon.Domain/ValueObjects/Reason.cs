namespace Halcyon.Domain.ValueObjects;

/// <summary>Why an agent did something (a handover, a return, a new priority): optional, at most 500 characters.</summary>
public sealed record Reason
{
    public const int MaxLength = 500;
    public const string TooLongMessage = "Pole „Powód” może mieć najwyżej 500 znaków.";

    private Reason(string value) => Value = value;

    public string Value { get; }

    /// <returns>Null for a blank reason: not giving one is normal.</returns>
    public static Reason? Create(string? value)
    {
        var (reason, error) = TryCreate(value);
        return error is not null ? throw new DomainException(error) : reason;
    }

    /// <returns>No reason and no error when it is blank.</returns>
    public static (Reason? value, string? error) TryCreate(string? value)
    {
        var (text, error) = TextRules.Optional(value, MaxLength, TooLongMessage);
        return error is not null ? (null, error)
            : text is null ? (null, null)
            : (new Reason(text), null);
    }

    public override string ToString() => Value;
}
