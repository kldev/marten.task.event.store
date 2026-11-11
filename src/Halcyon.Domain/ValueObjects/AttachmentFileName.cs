namespace Halcyon.Domain.ValueObjects;

/// <summary>
/// The name an attached file is stored under, and its type (ADR-0008): no folders, no control characters, at most
/// <see cref="MaxLength"/> characters (the extension kept), and an extension from the allowed list - the type comes
/// from it, never from what the browser claims.
/// </summary>
public sealed record AttachmentFileName
{
    public const int MaxLength = 200;
    public const string RequiredMessage = "Plik nie ma nazwy.";

    public static string TypeNotAllowedMessage(string name) =>
        $"Plik „{name}” ma niedozwolony typ. Dozwolone: {Tickets.AttachmentRules.AllowedText}.";

    private AttachmentFileName(string value, string contentType)
    {
        Value = value;
        ContentType = contentType;
    }

    public string Value { get; }
    public string ContentType { get; }

    public static AttachmentFileName Create(string? fileName)
    {
        var (name, error) = TryCreate(fileName);
        return error is not null ? throw new DomainException(error) : name!;
    }

    public static (AttachmentFileName? value, string? error) TryCreate(string? fileName)
    {
        // Both separators, whatever the server's OS: an old browser may send "C:\\Users\\anna\\x.png".
        var raw = fileName ?? "";
        raw = raw[(raw.LastIndexOfAny(['/', '\\']) + 1)..];
        var name = new string([.. raw.Where(c => !char.IsControl(c))]).Trim();
        if (name.Length == 0)
            return (null, RequiredMessage);
        if (name.Length > MaxLength)
            name = name[..(MaxLength - Path.GetExtension(name).Length)] + Path.GetExtension(name);
        return Tickets.AttachmentRules.ContentTypeFor(name) is { } type
            ? (new AttachmentFileName(name, type), null)
            : (null, TypeNotAllowedMessage(name));
    }

    public override string ToString() => Value;
}
