using System.ComponentModel.DataAnnotations;
using Wolverine;

namespace Halcyon.App.Messaging;

/// <summary>How Wolverine runs in this host (ADR-0016), from <c>Messaging__*</c>; the defaults are the production ones.</summary>
public sealed class MessagingOptions
{
    public const string Section = "Messaging";

    /// <summary>
    /// <c>Solo</c> for one instance (the default, ADR-0001); <c>Balanced</c> when several instances share the database,
    /// so they elect who recovers the inbox and outbox.
    /// </summary>
    public DurabilityMode Durability { get; set; } = DurabilityMode.Solo;

    /// <summary>How many mails are sent at once; SMTP servers limit connections per client.</summary>
    [Range(1, 20)]
    public int MailParallelism { get; set; } = 2;
}
