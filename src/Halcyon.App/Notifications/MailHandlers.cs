namespace Halcyon.App.Notifications;

// The consumers of the mail queue (spec 013): static classes whose names end with "Handler", found by Wolverine's
// convention (Ambit's NotificationWorker handlers). Each composes its mail and hands it to the at-most-once delivery;
// a transient failure is retried by MailFailurePolicies.

public static class ReportConfirmationMailHandler
{
    public static async Task Handle(
        ReportConfirmationMail message,
        MailComposer composer,
        MailDelivery delivery,
        CancellationToken ct
    ) => await delivery.SendOnceAsync(message, await composer.ConfirmationAsync(message, ct), ct);
}

public static class HandedOverMailHandler
{
    public static async Task Handle(
        HandedOverMail message,
        MailComposer composer,
        MailDelivery delivery,
        CancellationToken ct
    ) => await delivery.SendOnceAsync(message, await composer.HandedOverAsync(message, ct), ct);
}

public static class ResolvedMailHandler
{
    public static async Task Handle(
        ResolvedMail message,
        MailComposer composer,
        MailDelivery delivery,
        CancellationToken ct
    ) => await delivery.SendOnceAsync(message, await composer.ResolvedAsync(message, ct), ct);
}

public static class DeadlineMailHandler
{
    public static async Task Handle(
        DeadlineMail message,
        MailComposer composer,
        MailDelivery delivery,
        CancellationToken ct
    ) => await delivery.SendOnceAsync(message, await composer.DeadlineAsync(message, ct), ct);
}
