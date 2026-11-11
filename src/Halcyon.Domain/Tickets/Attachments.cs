using Halcyon.Domain.ValueObjects;

namespace Halcyon.Domain.Tickets;

/// <summary>
/// What may be attached to a ticket: a screenshot of an error, a contract, marketing material. The type comes from the
/// extension, never from what the browser claims, and decides whether the file may be shown in the browser.
/// </summary>
public static class AttachmentRules
{
    public const long MaxBytes = 20 * 1024 * 1024;
    public const int MaxPerTicket = 10;

    /// <summary>Extension → content type. SVG and HTML are missing on purpose: shown inline they can run scripts.</summary>
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".log"] = "text/plain",
        [".csv"] = "text/csv",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".zip"] = "application/zip",
        [".mp4"] = "video/mp4",
        [".mov"] = "video/quicktime",
    };

    /// <summary>For the file picker's <c>accept</c> attribute.</summary>
    public static string Accept { get; } = string.Join(",", Types.Keys);

    public const string AllowedText = "obrazy (PNG, JPG, GIF, WEBP), PDF, dokumenty Office, TXT/CSV, ZIP, MP4/MOV";

    public static string? ContentTypeFor(string fileName) => Types.GetValueOrDefault(Path.GetExtension(fileName));

    public static bool IsImage(string contentType) => contentType.StartsWith("image/", StringComparison.Ordinal);

    /// <summary>Raster images and PDF open in the browser; everything else is downloaded.</summary>
    public static bool ShowInline(string contentType) => IsImage(contentType) || contentType == "application/pdf";

    /// <summary>"12,4 MB" / "830 KB".</summary>
    public static string SizeText(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";

    /// <summary>The name as stored and its type (<see cref="AttachmentFileName"/>), then the size; refuses what is not allowed.</summary>
    public static (string FileName, string ContentType) Check(string? fileName, long size)
    {
        var file = AttachmentFileName.Create(fileName);
        var name = file.Value;
        var type = file.ContentType;
        if (size <= 0)
            throw new DomainException($"Plik „{name}” jest pusty.");
        if (size > MaxBytes)
            throw new DomainException($"Plik „{name}” jest za duży ({SizeText(size)}). Limit to {SizeText(MaxBytes)}.");
        return (name, type);
    }
}
