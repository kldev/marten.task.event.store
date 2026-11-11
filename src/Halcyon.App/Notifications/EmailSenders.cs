using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Halcyon.App.Notifications;

/// <summary>A mail ready to leave: who to, the subject, plain text and HTML.</summary>
public sealed record OutgoingMail(string To, string ToName, string Subject, string Text, string Html);

/// <summary>Puts a mail on the wire (Ambit's <c>ISendEmail</c>); rendering is <see cref="MailComposer"/>'s.</summary>
public interface ISendEmail
{
    Task SendAsync(OutgoingMail mail, CancellationToken ct);
}

/// <summary><see cref="MailProvider.Smtp"/>: one connection per mail - a handful a day, no pool to keep alive.</summary>
public sealed class SmtpEmailSender(IOptions<MailOptions> options, ILogger<SmtpEmailSender> logger) : ISendEmail
{
    private static MimeMessage Compose(OutgoingMail mail, MailOptions options)
    {
        var message = new MimeMessage
        {
            Subject = mail.Subject,
            Body = new BodyBuilder { TextBody = mail.Text, HtmlBody = mail.Html }.ToMessageBody(),
        };
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(new MailboxAddress(mail.ToName, mail.To));
        return message;
    }

    public async Task SendAsync(OutgoingMail mail, CancellationToken ct)
    {
        var config = options.Value;
        using var client = new SmtpClient();
        client.Timeout = config.Smtp.TimeoutSeconds * 1000;
        await client.ConnectAsync(config.Smtp.Host, config.Smtp.Port, SecurityOf(config.Smtp.Security), ct);
        if (!string.IsNullOrEmpty(config.Smtp.Username))
            await client.AuthenticateAsync(config.Smtp.Username, config.Smtp.Password ?? "", ct);
        await client.SendAsync(Compose(mail, config), ct);
        await client.DisconnectAsync(quit: true, ct);
        // The address is the recipient's personal data: the log says what left and through which server, not to whom.
        logger.LogInformation(
            "Mail sent: {Subject} through {Host}:{Port}",
            mail.Subject,
            config.Smtp.Host,
            config.Smtp.Port
        );
    }

    private static SecureSocketOptions SecurityOf(SmtpSecurity security) =>
        security switch
        {
            SmtpSecurity.None => SecureSocketOptions.None,
            SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.StartTls,
        };
}

/// <summary><see cref="MailProvider.Log"/>: the mail goes to the log only (local runs without Mailpit, demos).</summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : ISendEmail
{
    public Task SendAsync(OutgoingMail mail, CancellationToken ct)
    {
        logger.LogInformation("Mail not sent (Mail:Provider=Log): {Subject}", mail.Subject);
        return Task.CompletedTask;
    }
}
