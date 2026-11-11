using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Wolverine;

namespace Halcyon.App.Features.Tickets;

/// <summary>Give a ticket back to the pool (spec 003). The deadline does not move.</summary>
public sealed class ReturnTicketToPool(TicketCommands commands)
{
    public Task<TicketReturnedToPool?> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        string? reason,
        CancellationToken ct = default
    ) => commands.SendAsync<TicketReturnedToPool>(ticketId, new ReturnTicketToPoolCommand(ticketId, agent, reason), ct);
}

public sealed record ReturnTicketToPoolCommand(Guid TicketId, AgentRef Agent, string? Reason);

public static class ReturnTicketToPoolHandler
{
    public static async Task<(TicketReturnedToPool, OutgoingMessages)> Handle(
        ReturnTicketToPoolCommand command,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct
    )
    {
        var (stream, ticket) = await TicketStreams.LockAsync(session, command.TicketId, ct);
        var returned = ticket.ReturnToPool(command.Agent, command.Reason, time.GetUtcNow());
        stream.AppendOne(returned);
        return (returned, []);
    }
}
