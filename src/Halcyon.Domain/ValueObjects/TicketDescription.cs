namespace Halcyon.Domain.ValueObjects;

/// <summary>What happened and what is needed: required, trimmed, at most <see cref="MaxLength"/> characters.</summary>
public sealed record TicketDescription
{
    public const int MaxLength = 4000;
    public const string RequiredMessage = "Pole „Opis” jest wymagane.";
    public const string TooLongMessage = "Pole „Opis” może mieć najwyżej 4000 znaków.";

    private TicketDescription(string value) => Value = value;

    public string Value { get; }

    public static TicketDescription Create(string? value)
    {
        var (description, error) = TryCreate(value);
        return error is not null ? throw new DomainException(error) : description!;
    }

    public static (TicketDescription? value, string? error) TryCreate(string? value)
    {
        var (text, error) = TextRules.Required(value, MaxLength, RequiredMessage, TooLongMessage);
        return error is not null ? (null, error) : (new TicketDescription(text!), null);
    }

    /// <summary>A value already stored in an event, read back without the rules: folding a stream never refuses (ADR-0003).</summary>
    public static TicketDescription FromStored(string value) => new(value);

    public override string ToString() => Value;
}
