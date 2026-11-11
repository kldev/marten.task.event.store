using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Wolverine;

namespace Halcyon.App.Features.Tickets;

/// <summary>Fill in or correct the report data, in any state (spec 008). Null = nothing changed.</summary>
public sealed class UpdateTicketFields(TicketCommands commands)
{
    public Task<TicketFieldsUpdated?> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken ct = default
    ) => commands.SendAsync<TicketFieldsUpdated>(ticketId, new UpdateTicketFieldsCommand(ticketId, agent, values), ct);
}

public sealed record UpdateTicketFieldsCommand(
    Guid TicketId,
    AgentRef Agent,
    IReadOnlyDictionary<string, string?> Values
);

public static class UpdateTicketFieldsHandler
{
    public static async Task<(TicketFieldsUpdated?, OutgoingMessages)> Handle(
        UpdateTicketFieldsCommand command,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct
    )
    {
        var (stream, ticket) = await TicketStreams.LockAsync(session, command.TicketId, ct);
        if (ticket.UpdateFields(command.Agent, command.Values, time.GetUtcNow()) is not { } updated)
            return (null, []);
        stream.AppendOne(updated);
        return (updated, []);
    }
}
