using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Wolverine;

namespace Halcyon.App.Features.Tickets;

/// <summary>Add a comment, in any state (spec 005).</summary>
public sealed class CommentOnTicket(TicketCommands commands)
{
    public Task<TicketCommented?> ExecuteAsync(
        Guid ticketId,
        AgentRef agent,
        string? text,
        CancellationToken ct = default
    ) =>
        commands.SendAsync<TicketCommented>(
            ticketId,
            new CommentOnTicketCommand(ticketId, Guid.CreateVersion7(), agent, text),
            ct
        );
}

public sealed record CommentOnTicketCommand(Guid TicketId, Guid CommentId, AgentRef Agent, string? Text);

public static class CommentOnTicketHandler
{
    public static async Task<(TicketCommented, OutgoingMessages)> Handle(
        CommentOnTicketCommand command,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct
    )
    {
        var (stream, ticket) = await TicketStreams.LockAsync(session, command.TicketId, ct);
        var commented = ticket.Comment(command.CommentId, command.Agent, command.Text, time.GetUtcNow());
        stream.AppendOne(commented);
        return (commented, []);
    }
}
