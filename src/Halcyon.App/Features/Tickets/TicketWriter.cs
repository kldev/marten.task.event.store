using Halcyon.Database.Documents;
using Halcyon.Domain;
using Halcyon.Domain.Tickets;
using JasperFx;
using JasperFx.Events;
using Marten;
using static Halcyon.App.Localization.Tr;

namespace Halcyon.App.Features.Tickets;

/// <summary>
/// The one way a ticket changes after it was reported (ADR-0003): fold the stream, let the domain decide, append the
/// event and save. Two writers on one ticket: Marten's version check fails the slower one, who is told who was faster.
/// </summary>
public sealed class TicketWriter(IDocumentStore store, TimeProvider time)
{
    public async Task<TEvent?> ExecuteAsync<TEvent>(
        Guid ticketId,
        Func<Ticket, DateTimeOffset, IQuerySession, Task<TEvent?>> decide,
        CancellationToken ct = default
    )
        where TEvent : class
    {
        await using var session = store.LightweightSession();
        var stream = await session.Events.FetchForWriting<Ticket>(ticketId, ct);
        var ticket = stream.Aggregate ?? throw new DomainException("Nie ma takiego zgłoszenia.");

        var @event = await decide(ticket, time.GetUtcNow(), session);
        if (@event is null)
            return null;

        stream.AppendOne(@event);
        try
        {
            await session.SaveChangesAsync(ct);
        }
        catch (Exception exception) when (IsConcurrency(exception))
        {
            throw new DomainException(await ConflictMessageAsync(ticketId, ct), exception);
        }
        return @event;
    }

    public Task<TEvent?> ExecuteAsync<TEvent>(
        Guid ticketId,
        Func<Ticket, DateTimeOffset, TEvent?> decide,
        CancellationToken ct = default
    )
        where TEvent : class => ExecuteAsync(ticketId, (t, now, _) => Task.FromResult(decide(t, now)), ct);

    internal static bool IsConcurrency(Exception exception) =>
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
