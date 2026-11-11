using Halcyon.Database.Migrations;
using JasperFx.CodeGeneration;
using Wolverine;

namespace Halcyon.App.Messaging;

/// <summary>
/// Wolverine in this host (ADR-0016): the ticket commands' handlers, the transactional outbox over Marten and the
/// mail queue. Described once here; <see cref="HalcyonApp"/> calls it.
/// </summary>
public static class WolverineSetup
{
    /// <summary>The durable local queue every mail goes through (spec 013).</summary>
    public const string MailQueue = "mail";

    /// <summary>True in an image built with <c>-p:WolverineStaticCode=true</c> (Directory.Build.props).</summary>
    private static bool InImage { get; } =
#if WOLVERINE_STATIC_CODE
        true;
#else
        false;
#endif

    /// <summary>The JasperFx command the image build runs: <c>codegen write</c> (or <c>codegen preview</c>).</summary>
    public static bool IsCodegenCommand(string[] args) => args is ["codegen", ..];

    public static WebApplicationBuilder AddHalcyonWolverine(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var environment = builder.Environment;
        var messaging = configuration.GetSection(MessagingOptions.Section).Get<MessagingOptions>() ?? new();

        builder.Host.UseWolverine(options =>
        {
            // Named, not guessed: under a test runner the entry assembly is the tests', and the handlers are here.
            options.ApplicationAssembly = typeof(WolverineSetup).Assembly;

            // Every handler that touches Marten commits its session - events, documents and outgoing messages - in one
            // transaction, the outbox's (Ambit's AutoApplyTransactions).
            options.Policies.AutoApplyTransactions();

            // One host (ADR-0001): every agent runs here, and inbox/outbox recovery starts at once. Balanced only when
            // several instances share the database (Messaging:Durability).
            options.Durability.Mode = messaging.Durability;

            // Mail waits in PostgreSQL, not in memory: a restart or a crash between the save and the send loses
            // nothing, and a scheduled warning survives until its time (spec 013).
            options.Policies.UseDurableLocalQueues();
            // Every mail type, by its interface (PublishMessage<ITicketMail> would match the interface itself, not the
            // records, and each would get a local queue of its own).
            options.Publish(rule => rule.MessagesImplementing<Notifications.ITicketMail>().ToLocalQueue(MailQueue));
            options.LocalQueue(MailQueue).MaximumParallelMessages(messaging.MailParallelism);
            options.RetryMailDelivery();

            // The image has the handler code compiled in; a handler without it stops the host at start instead of
            // being compiled at runtime (Roslyn assemblies are never unloaded - Ambit's API died of it).
            if (InImage)
                options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;

            // The migrator builds the tables in a release (ADR-0002); locally and in the tests the host may.
            options.AutoBuildMessageStorageOnStartup = DatabaseStartup.SchemaCreation(configuration, environment);
        });
        return builder;
    }
}
