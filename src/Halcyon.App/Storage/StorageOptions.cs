using System.ComponentModel.DataAnnotations;

namespace Halcyon.App.Storage;

/// <summary>
/// The S3-compatible object storage for attachments (RustFS in compose). Keys only from the environment
/// (<c>Storage__AccessKey</c>, <c>Storage__SecretKey</c>; in compose from <c>.env</c>) or user-secrets.
/// </summary>
public sealed class StorageOptions
{
    public const string Section = "Storage";

    [Required(ErrorMessage = "Storage:ServiceUrl is required (env Storage__ServiceUrl), e.g. http://rustfs:9000.")]
    [Url]
    public string ServiceUrl { get; set; } = "";

    [Required(ErrorMessage = "Storage:AccessKey is required (env Storage__AccessKey / S3_ACCESS_KEY in .env).")]
    public string AccessKey { get; set; } = "";

    [Required(ErrorMessage = "Storage:SecretKey is required (env Storage__SecretKey / S3_SECRET_KEY in .env).")]
    public string SecretKey { get; set; } = "";

    [Required]
    public string Bucket { get; set; } = "halcyon-attachments";

    /// <summary>S3 signs requests for a region; RustFS accepts any, AWS needs the bucket's one.</summary>
    [Required]
    public string Region { get; set; } = "us-east-1";
}
