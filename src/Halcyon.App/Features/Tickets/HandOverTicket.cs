using Halcyon.App.Notifications;
using Halcyon.Database.Documents;
using Halcyon.Domain;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Wolverine;

namespace Halcyon.App.Features.Tickets;

/// <summary>Hand a ticket over to another agent (spec 003); the agent hears it by mail (spec 013). The deadline does not move.</summary>
public sealed class HandOverTicket(TicketCommands commands)
{
    public Task<TicketHandedOver?> ExecuteAsync(
        Guid ticketId,
        AgentRef from,
        Guid toAgentId,
        string? reason,
        CancellationToken ct = default
    ) =>
        commands.SendAsync<TicketHandedOver>(
            ticketId,
            new HandOverTicketCommand(ticketId, from, toAgentId, reason),
            ct
        );
}

public sealed record HandOverTicketCommand(Guid TicketId, AgentRef From, Guid ToAgentId, string? Reason);

public static class HandOverTicketHandler
{
    private const string NoSuchAgent = "Nie ma takiego agenta.";

    public static async Task<(TicketHandedOver, OutgoingMessages)> Handle(
        HandOverTicketCommand command,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct
    )
    {
        var (stream, ticket) = await TicketStreams.LockAsync(session, command.TicketId, ct);
        var to = await session.LoadAsync<Agent>(command.ToAgentId, ct) ?? throw new DomainException(NoSuchAgent);
        var handed = ticket.HandOver(command.From, to.ToRef(), command.Reason, time.GetUtcNow());
        stream.AppendOne(handed);
        return (handed, TicketMailPlan.HandedOver(handed));
    }
}
