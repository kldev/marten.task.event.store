namespace Halcyon.Domain.ValueObjects;

/// <summary>
/// The short number a requester is given: "HAL-" and 8 hex digits in capitals, unique (ADR-0014). Made from the
/// ticket's id (<see cref="For"/>) or read from what somebody typed (<see cref="TryCreate"/>).
/// </summary>
public sealed record TicketNumber
{
    private const string Prefix = "HAL-";
    private const string DefaultField = "Numer zgłoszenia";

    public static string InvalidMessage(string field = DefaultField) =>
        TextRules.Field(field, "musi być numerem zgłoszenia, np. HAL-1A2B3C4D.");

    private TicketNumber(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// For a version 7 id (every reported ticket) the first digits are its timestamp - two tickets within ~65 s would
    /// share them - so its last 8 (random) digits are used; other ids (the seed's name-based ones) keep their first 8.
    /// </summary>
    public static TicketNumber For(Guid ticketId)
    {
        var hex = ticketId.ToString("N").ToUpperInvariant();
        return new TicketNumber(Prefix + (ticketId.Version == 7 ? hex[^8..] : hex[..8]));
    }

    public static TicketNumber Create(string? value, string field = DefaultField)
    {
        var (number, error) = TryCreate(value, field);
        return error is not null ? throw new DomainException(error) : number!;
    }

    /// <summary>Trimmed and in capitals: "hal-1a2b3c4d" is HAL-1A2B3C4D.</summary>
    public static (TicketNumber? value, string? error) TryCreate(string? value, string field = DefaultField)
    {
        var number = (value ?? "").Trim().ToUpperInvariant();
        return
            number.Length == Prefix.Length + 8
            && number.StartsWith(Prefix, StringComparison.Ordinal)
            && number[Prefix.Length..].All(char.IsAsciiHexDigit)
            ? (new TicketNumber(number), null)
            : (null, InvalidMessage(field));
    }

    public override string ToString() => Value;
}
