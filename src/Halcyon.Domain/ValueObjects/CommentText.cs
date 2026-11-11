namespace Halcyon.Domain.ValueObjects;

/// <summary>A comment on a ticket (spec 005): required, trimmed, at most <see cref="MaxLength"/> characters.</summary>
public sealed record CommentText
{
    public const int MaxLength = 2000;
    public const string RequiredMessage = "Pole „Komentarz” jest wymagane.";
    public const string TooLongMessage = "Pole „Komentarz” może mieć najwyżej 2000 znaków.";

    private CommentText(string value) => Value = value;

    public string Value { get; }

    public static CommentText Create(string? value)
    {
        var (comment, error) = TryCreate(value);
        return error is not null ? throw new DomainException(error) : comment!;
    }

    public static (CommentText? value, string? error) TryCreate(string? value)
    {
        var (text, error) = TextRules.Required(value, MaxLength, RequiredMessage, TooLongMessage);
        return error is not null ? (null, error) : (new CommentText(text!), null);
    }

    public override string ToString() => Value;
}
