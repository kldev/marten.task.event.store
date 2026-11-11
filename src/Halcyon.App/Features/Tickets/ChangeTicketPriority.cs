using Halcyon.App.Notifications;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Wolverine;

namespace Halcyon.App.Features.Tickets;

/// <summary>
/// Change the priority; the deadline is recomputed from the report date with the configured SLA (spec 004, ADR-0013)
/// and its warnings are scheduled again (spec 013). Null = no change.
/// </summary>
public sealed class ChangeTicketPriority(TicketCommands commands)
{
    public Task<TicketPriorityChanged?> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        Priority to,
        string? reason,
        CancellationToken ct = default
    ) =>
        commands.SendAsync<TicketPriorityChanged>(
            ticketId,
            new ChangeTicketPriorityCommand(ticketId, agent, to, reason),
            ct
        );
}

public sealed record ChangeTicketPriorityCommand(Guid TicketId, AgentRef Agent, Priority To, string? Reason);

public static class ChangeTicketPriorityHandler
{
    public static async Task<(TicketPriorityChanged?, OutgoingMessages)> Handle(
        ChangeTicketPriorityCommand command,
        IDocumentSession session,
        SlaPolicy sla,
        TimeProvider time,
        CancellationToken ct
    )
    {
        var (stream, ticket) = await TicketStreams.LockAsync(session, command.TicketId, ct);
        var now = time.GetUtcNow();
        if (ticket.ChangePriority(command.Agent, command.To, command.Reason, now, sla) is not { } changed)
            return (null, []);
        stream.AppendOne(changed);
        return (changed, TicketMailPlan.PriorityChanged(changed, ticket.ReportedAt, now));
    }
}
