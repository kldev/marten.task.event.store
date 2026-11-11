using System.Net.Mail;

namespace Halcyon.Domain.ValueObjects;

/// <summary>
/// An e-mail address: of an agent or a person, or a report field. <see cref="Value"/> is as typed (trimmed);
/// <see cref="Normalized"/> is the form every address is compared and indexed in.
/// </summary>
public sealed record Email
{
    private const int MaxLength = 320;
    private const string DefaultField = "E-mail";

    public static string RequiredMessage(string field = DefaultField) => TextRules.Field(field, "jest wymagane.");

    private static string InvalidMessage(string field = DefaultField) =>
        TextRules.Field(field, "musi być adresem e-mail.");

    private Email(string value) => Value = value;

    public string Value { get; }

    public string Normalized => Normalize(Value);

    public static Email Create(string? value, string field = DefaultField)
    {
        var (email, error) = TryCreate(value, field);
        return error is not null ? throw new DomainException(error) : email!;
    }

    /// <summary>A bare address only: no display name ("Anna &lt;a@b.c&gt;"), nothing around it.</summary>
    public static (Email? value, string? error) TryCreate(string? value, string field = DefaultField)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            return (null, RequiredMessage(field));
        if (
            trimmed.Length > MaxLength
            || !MailAddress.TryCreate(trimmed, out var address)
            || address.Address != trimmed
        )
            return (null, InvalidMessage(field));
        return (new Email(trimmed), null);
    }

    /// <summary>
    /// The lookup form of whatever was typed (sign-in): trimmed, lower case. Not a check - an address that is not one
    /// simply finds nobody.
    /// </summary>
    public static string Normalize(string value) => value.Trim().ToLowerInvariant();

    public override string ToString() => Value;
}
