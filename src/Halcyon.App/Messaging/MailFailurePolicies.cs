using System.Net.Sockets;
using Halcyon.App.Notifications;
using JasperFx;
using JasperFx.CodeGeneration;
using MailKit;
using MailKit.Net.Smtp;
using MimeKit;
using Npgsql;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Halcyon.App.Messaging;

/// <summary>
/// What happens when a mail cannot leave (Ambit's <c>EmailFailurePolicies</c>), for the mail handlers only - the ticket
/// commands keep Wolverine's defaults, so a refused command never waits through mail cooldowns. Wolverine takes the
/// first rule whose exception matches, so the permanent failures come first: a malformed address and a 5xx refusal go
/// to the dead letters at once; everything transient (SMTP, network, timeouts, the database) is tried again after 1 s,
/// 5 s and 15 s, then dead-lettered. Without a rule the first exception would dead-letter the mail.
/// </summary>
public static class MailFailurePolicies
{
    public static void RetryMailDelivery(this WolverineOptions options) =>
        options.Policies.Add(new MailFailurePolicy());

    private sealed class MailFailurePolicy : IHandlerPolicy
    {
        public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
        {
            foreach (var chain in chains.Where(c => typeof(ITicketMail).IsAssignableFrom(c.MessageType)))
            {
                chain.OnException<ParseException>().MoveToErrorQueue();
                chain.OnException<SmtpCommandException>(e => (int)e.StatusCode >= 500).MoveToErrorQueue();
                chain
                    .OnException<SmtpCommandException>()
                    .Or<SmtpProtocolException>()
                    .Or<ServiceNotConnectedException>()
                    .Or<IOException>()
                    .Or<SocketException>()
                    .Or<TimeoutException>()
                    .Or<NpgsqlException>()
                    .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15))
                    .Then.MoveToErrorQueue();
            }
        }
    }
}
