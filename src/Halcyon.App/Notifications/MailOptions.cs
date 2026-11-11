using System.ComponentModel.DataAnnotations;
using Halcyon.Domain.ValueObjects;

namespace Halcyon.App.Notifications;

public enum MailProvider
{
    /// <summary>Writes each mail to the log and sends nothing: a host without SMTP still runs (the default).</summary>
    Log,

    /// <summary>Sends over SMTP (<see cref="SmtpOptions"/>): Mailpit locally, the company's server in production.</summary>
    Smtp,
}

public enum SmtpSecurity
{
    /// <summary>Plain SMTP - Mailpit on 1025, never across a network you do not own.</summary>
    None,

    /// <summary>STARTTLS on 587 (the default).</summary>
    StartTls,

    /// <summary>TLS from the first byte (SMTPS, 465).</summary>
    SslOnConnect,
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;
    public string? Username { get; set; }
    public string? Password { get; set; }

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Mail (spec 013), from <c>Mail__*</c>. Production sets <c>Mail__Provider=Smtp</c>, the server, the sender and the
/// app's address; a wrong set stops the host at start, naming the setting.
/// </summary>
public sealed class MailOptions : IValidatableObject
{
    public const string Section = "Mail";

    public MailProvider Provider { get; set; } = MailProvider.Log;

    /// <summary>The From address; a mailbox the SMTP server lets this host send from.</summary>
    public string FromAddress { get; set; } = "halcyon@company.io";

    public string FromName { get; set; } = "Halcyon";

    /// <summary>The app's public address, for the links in the agents' mails (<c>https://halcyon.company.io</c>).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Who hears a deadline warning for a ticket nobody has taken (still in the pool): a team mailbox. Empty = nobody;
    /// the pool's counters show it anyway.
    /// </summary>
    public string? PoolAddress { get; set; }

    public SmtpOptions Smtp { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Email.TryCreate(FromAddress).error is not null)
            yield return new ValidationResult("Mail:FromAddress must be an e-mail address.");
        if (!string.IsNullOrWhiteSpace(PoolAddress) && Email.TryCreate(PoolAddress).error is not null)
            yield return new ValidationResult("Mail:PoolAddress must be an e-mail address (or empty).");
        if (
            !string.IsNullOrWhiteSpace(BaseUrl)
            && !(Uri.TryCreate(BaseUrl, UriKind.Absolute, out var url) && url.Scheme is "http" or "https")
        )
            yield return new ValidationResult("Mail:BaseUrl must be an absolute http(s) address.");
        if (Provider != MailProvider.Smtp)
            yield break;
        if (string.IsNullOrWhiteSpace(Smtp.Host))
            yield return new ValidationResult("Mail:Smtp:Host is required with Mail:Provider=Smtp.");
        if (string.IsNullOrWhiteSpace(BaseUrl))
            yield return new ValidationResult(
                "Mail:BaseUrl is required with Mail:Provider=Smtp: the agents' mails link to tickets."
            );
        if (string.IsNullOrEmpty(Smtp.Username) != string.IsNullOrEmpty(Smtp.Password))
            yield return new ValidationResult("Mail:Smtp:Username and Mail:Smtp:Password go together.");
        foreach (var result in SmtpResults())
            yield return result;
    }

    private IEnumerable<ValidationResult> SmtpResults()
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(Smtp, new ValidationContext(Smtp), results, validateAllProperties: true);
        return results.Select(r => new ValidationResult($"Mail:Smtp: {r.ErrorMessage}"));
    }
}
