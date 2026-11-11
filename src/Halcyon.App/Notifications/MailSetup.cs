using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Halcyon.App.Notifications;

public static class MailSetup
{
    public static WebApplicationBuilder AddHalcyonMail(this WebApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<MailOptions>()
            .Bind(builder.Configuration.GetSection(MailOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<MailComposer>();
        builder.Services.AddSingleton<MailDelivery>();

        // Picked here, by type, not by a factory: Wolverine generates the handlers' code from the registrations, and a
        // dependency built by a lambda makes `codegen write` fail (Ambit's lesson). Try: a test brings its own.
        var provider = builder.Configuration.GetSection(MailOptions.Section).Get<MailOptions>()?.Provider;
        if (provider == MailProvider.Smtp)
            builder.Services.TryAddSingleton<ISendEmail, SmtpEmailSender>();
        else
            builder.Services.TryAddSingleton<ISendEmail, LogEmailSender>();
        return builder;
    }
}
