using Halcyon.App.Notifications;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Wolverine;

namespace Halcyon.App.Features.Tickets;

/// <summary>Mark the ticket resolved; only its assignee can (spec 003). The requester hears it by mail (spec 013).</summary>
public sealed class ResolveTicket(TicketCommands commands)
{
    public Task<TicketResolved?> ExecuteAsync(Guid ticketId, AgentRef agent, CancellationToken ct = default) =>
        commands.SendAsync<TicketResolved>(ticketId, new ResolveTicketCommand(ticketId, agent), ct);
}

public sealed record ResolveTicketCommand(Guid TicketId, AgentRef Agent);

public static class ResolveTicketHandler
{
    public static async Task<(TicketResolved, OutgoingMessages)> Handle(
        ResolveTicketCommand command,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct
    )
    {
        var (stream, ticket) = await TicketStreams.LockAsync(session, command.TicketId, ct);
        var resolved = ticket.Resolve(command.Agent, time.GetUtcNow());
        stream.AppendOne(resolved);
        return (resolved, TicketMailPlan.Resolved(resolved, ticket));
    }
}
