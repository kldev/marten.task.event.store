using Halcyon.Database.Documents;
using Halcyon.Domain;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;

namespace Halcyon.App.Features.Tickets;

/// <summary>Take a ticket from the pool (spec 003).</summary>
public sealed class TakeTicket(TicketWriter writer)
{
    public Task<TicketTaken?> ExecuteAsync(Guid ticketId, AgentRef agent, CancellationToken ct = default) =>
        writer.ExecuteAsync(ticketId, (ticket, now) => ticket.Take(agent, now), ct);
}

/// <summary>Hand a ticket over to another agent (spec 003). The deadline does not move.</summary>
public sealed class HandOverTicket(TicketWriter writer)
{
    public Task<TicketHandedOver?> ExecuteAsync(
        Guid ticketId,
        AgentRef from,
        Guid toAgentId,
        string? reason,
        CancellationToken ct = default
    ) =>
        writer.ExecuteAsync(
            ticketId,
            async (ticket, now, session) =>
            {
                var to =
                    await session.LoadAsync<Agent>(toAgentId, ct)
                    ?? throw new DomainException("Nie ma takiego agenta.");
                return ticket.HandOver(from, to.ToRef(), reason, now);
            },
            ct
        );
}

/// <summary>Give a ticket back to the pool (spec 003). The deadline does not move.</summary>
public sealed class ReturnTicketToPool(TicketWriter writer)
{
    public Task<TicketReturnedToPool?> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        string? reason,
        CancellationToken ct = default
    ) => writer.ExecuteAsync(ticketId, (ticket, now) => ticket.ReturnToPool(agent, reason, now), ct);
}

/// <summary>Change the priority; the deadline is recomputed from the report date (spec 004). Null = no change.</summary>
public sealed class ChangeTicketPriority(TicketWriter writer)
{
    public Task<TicketPriorityChanged?> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        Priority to,
        string? reason,
        CancellationToken ct = default
    ) => writer.ExecuteAsync(ticketId, (ticket, now) => ticket.ChangePriority(agent, to, reason, now), ct);
}

/// <summary>Mark the ticket resolved; only its assignee can (spec 003).</summary>
public sealed class ResolveTicket(TicketWriter writer)
{
    public Task<TicketResolved?> ExecuteAsync(Guid ticketId, AgentRef agent, CancellationToken ct = default) =>
        writer.ExecuteAsync(ticketId, (ticket, now) => ticket.Resolve(agent, now), ct);
}

/// <summary>Add a comment, in any state (spec 005).</summary>
public sealed class CommentOnTicket(TicketWriter writer)
{
    public Task<TicketCommented?> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        string? text,
        CancellationToken ct = default
    ) => writer.ExecuteAsync(ticketId, (ticket, now) => ticket.Comment(Guid.CreateVersion7(), agent, text, now), ct);
}

/// <summary>Fill in or correct the report data, in any state (spec 008). Null = nothing changed.</summary>
public sealed class UpdateTicketFields(TicketWriter writer)
{
    public Task<TicketFieldsUpdated?> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken ct = default
    ) => writer.ExecuteAsync(ticketId, (ticket, now) => ticket.UpdateFields(agent, values, now), ct);
}

/// <summary>
/// The agent the ticket is with names a person from the category's department to do the work, or takes them off
/// (<c>personId</c> null) - spec 008. Null = no change.
/// </summary>
public sealed class AssignTicketHandler(TicketWriter writer)
{
    public Task<TicketHandlerAssigned?> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        Guid? personId,
        string? reason,
        CancellationToken ct = default
    ) =>
        writer.ExecuteAsync(
            ticketId,
            async (ticket, now, session) =>
            {
                if (personId is not { } id)
                    return ticket.AssignHandler(agent, null, null, reason, now);
                var person =
                    await session.LoadAsync<Person>(id, ct)
                    ?? throw new DomainException("Nie ma takiej osoby na liście.");
                return ticket.AssignHandler(agent, person.ToRef(), person.Department, reason, now);
            },
            ct
        );
}
