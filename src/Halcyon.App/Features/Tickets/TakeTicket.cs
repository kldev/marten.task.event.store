using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Wolverine;

namespace Halcyon.App.Features.Tickets;

/// <summary>Take a ticket from the pool (spec 003).</summary>
public sealed class TakeTicket(TicketCommands commands)
{
    public Task<TicketTaken?> ExecuteAsync(Guid ticketId, AgentRef agent, CancellationToken ct = default) =>
        commands.SendAsync<TicketTaken>(ticketId, new TakeTicketCommand(ticketId, agent), ct);
}

public sealed record TakeTicketCommand(Guid TicketId, AgentRef Agent);

public static class TakeTicketHandler
{
    public static async Task<(TicketTaken, OutgoingMessages)> Handle(
        TakeTicketCommand command,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct
    )
    {
        var (stream, ticket) = await TicketStreams.LockAsync(session, command.TicketId, ct);
        var taken = ticket.Take(command.Agent, time.GetUtcNow());
        stream.AppendOne(taken);
        return (taken, []);
    }
}
