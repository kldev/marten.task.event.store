using Halcyon.Database.Documents;
using Halcyon.Domain;
using Halcyon.Domain.Tickets;
using JasperFx;
using JasperFx.Events;
using Marten;
using Wolverine.Runtime;
using static Halcyon.App.Localization.Tr;

namespace Halcyon.App.Features.Tickets;

/// <summary>
/// The one way a ticket changes (ADR-0003, ADR-0016): a command goes over Wolverine's bus to its static handler, which
/// locks the ticket's stream, lets the domain decide and returns the event with the mails it causes; Wolverine commits
/// the events, the read model and the outbox in one transaction. The pages and the tests call the typed classes
/// (<see cref="TakeTicket"/>, ...), the way Ambit's endpoints translate a request into a command.
/// </summary>
public sealed class TicketCommands(IWolverineRuntime runtime, IDocumentStore store)
{
    /// <summary>The event the handler returned, or null when nothing changed (the domain's no-op).</summary>
    public async Task<TEvent?> SendAsync<TEvent>(Guid ticketId, object command, CancellationToken ct = default)
        where TEvent : class
    {
        try
        {
            // A bus per call: IMessageBus is scoped, these classes are singletons (a Blazor circuit, a test).
            return await new MessageBus(runtime).InvokeAsync<TEvent?>(command, ct);
        }
        catch (Exception exception) when (IsConcurrency(exception))
        {
            throw new DomainException(await ConflictMessageAsync(ticketId, ct), exception);
        }
    }

    /// <summary>
    /// The handlers lock the stream, so two clicks at once are served one after the other and the slower one hears the
    /// domain's answer ("…jest już w realizacji u: …"); this is left for a lock that was not granted in time.
    /// </summary>
    private static bool IsConcurrency(Exception exception) =>
        exception is ConcurrencyException or StreamLockedException
        || exception.GetType().Name.Contains("UnexpectedMaxEventId", StringComparison.Ordinal)
        || (exception.InnerException is { } inner && IsConcurrency(inner));

    private async Task<string> ConflictMessageAsync(Guid ticketId, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        var view = await session.LoadAsync<TicketView>(ticketId, ct);
        return view switch
        {
            { Status: TicketStatus.InProgress, AssigneeName: { } name } => T(
                $"Ktoś był szybszy: zgłoszenie jest teraz w realizacji u: {name}. Lista została odświeżona.",
                $"Someone was faster: the ticket is now being handled by: {name}. The list has been refreshed."
            ),
            { Status: TicketStatus.Done } => T(
                "Ktoś był szybszy: zgłoszenie zostało już zrealizowane.",
                "Someone was faster: the ticket has already been resolved."
            ),
            _ => T(
                "Ktoś zmienił to zgłoszenie w tym samym momencie. Odśwież i spróbuj ponownie.",
                "Someone changed this ticket at the same moment. Refresh and try again."
            ),
        };
    }
}

/// <summary>What every ticket handler does first (Ambit's <c>RequestGuard</c>).</summary>
public static class TicketStreams
{
    private const string NoSuchTicket = "Nie ma takiego zgłoszenia.";

    /// <summary>
    /// The ticket's stream, locked for the rest of the handler's transaction (<c>FetchForExclusiveWriting</c> - what
    /// <c>[AggregateHandler(ConcurrencyStyle.Exclusive)]</c> does): a second command on the same ticket waits and
    /// then decides on the state the first one left.
    /// </summary>
    public static async Task<(IEventStream<Ticket> Stream, Ticket Ticket)> LockAsync(
        IDocumentSession session,
        Guid ticketId,
        CancellationToken ct
    )
    {
        var stream = await session.Events.FetchForExclusiveWriting<Ticket>(ticketId, ct);
        return (stream, stream.Aggregate ?? throw new DomainException(NoSuchTicket));
    }
}
