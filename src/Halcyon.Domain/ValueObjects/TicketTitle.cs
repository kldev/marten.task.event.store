namespace Halcyon.Domain.ValueObjects;

/// <summary>What a ticket is about, in one line: required, trimmed, at most <see cref="MaxLength"/> characters.</summary>
public sealed record TicketTitle
{
    public const int MaxLength = 200;
    public const string RequiredMessage = "Pole „Tytuł” jest wymagane.";
    public const string TooLongMessage = "Pole „Tytuł” może mieć najwyżej 200 znaków.";

    private TicketTitle(string value) => Value = value;

    public string Value { get; }

    public static TicketTitle Create(string? value)
    {
        var (title, error) = TryCreate(value);
        return error is not null ? throw new DomainException(error) : title!;
    }

    public static (TicketTitle? value, string? error) TryCreate(string? value)
    {
        var (text, error) = TextRules.Required(value, MaxLength, RequiredMessage, TooLongMessage);
        return error is not null ? (null, error) : (new TicketTitle(text!), null);
    }

    /// <summary>A value already stored in an event, read back without the rules: folding a stream never refuses (ADR-0003).</summary>
    public static TicketTitle FromStored(string value) => new(value);

    public override string ToString() => Value;
}
