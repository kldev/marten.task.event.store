using Halcyon.Database.Documents;
using Halcyon.Domain;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Wolverine;

namespace Halcyon.App.Features.Tickets;

/// <summary>
/// The agent the ticket is with names a person from the category's department to do the work, or takes them off
/// (<c>personId</c> null) - spec 008. Null = no change. ("Handler" here is the person who does the work; the Wolverine
/// handler is <see cref="AssignTicketPersonHandler"/>.)
/// </summary>
public sealed class AssignTicketHandler(TicketCommands commands)
{
    public Task<TicketHandlerAssigned?> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        Guid? personId,
        string? reason,
        CancellationToken ct = default
    ) =>
        commands.SendAsync<TicketHandlerAssigned>(
            ticketId,
            new AssignTicketPersonCommand(ticketId, agent, personId, reason),
            ct
        );
}

public sealed record AssignTicketPersonCommand(Guid TicketId, AgentRef Agent, Guid? PersonId, string? Reason);

public static class AssignTicketPersonHandler
{
    private const string NoSuchPerson = "Nie ma takiej osoby na liście.";

    public static async Task<(TicketHandlerAssigned?, OutgoingMessages)> Handle(
        AssignTicketPersonCommand command,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct
    )
    {
        var (stream, ticket) = await TicketStreams.LockAsync(session, command.TicketId, ct);
        var now = time.GetUtcNow();
        TicketHandlerAssigned? assigned;
        if (command.PersonId is not { } id)
            assigned = ticket.AssignHandler(command.Agent, null, null, command.Reason, now);
        else
        {
            var person = await session.LoadAsync<Person>(id, ct) ?? throw new DomainException(NoSuchPerson);
            assigned = ticket.AssignHandler(command.Agent, person.ToRef(), person.Department, command.Reason, now);
        }
        if (assigned is null)
            return (null, []);
        stream.AppendOne(assigned);
        return (assigned, []);
    }
}
