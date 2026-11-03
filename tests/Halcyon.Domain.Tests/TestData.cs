using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;

namespace Halcyon.Domain.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    public static readonly AgentRef Anna = new(Guid.Parse("00000000-0000-0000-0000-00000000000a"), "Anna Nowak");
    public static readonly AgentRef Piotr = new(Guid.Parse("00000000-0000-0000-0000-00000000000b"), "Piotr Kowalski");
    public static readonly PersonRef Adam = new(Guid.Parse("00000000-0000-0000-0000-0000000000c1"), "Adam", "Mazur");

    public static TicketDraft Draft(
        Priority? priority = Priority.Medium,
        DateTimeOffset? reportedAt = null,
        string? title = "Laptop nie ładuje",
        string? description = "Dioda nie świeci.",
        string? category = "it",
        string? subcategory = "it.hardware",
        Requester? requester = null,
        ReportChannel channel = ReportChannel.Agent,
        AgentRef? registeredBy = null
    ) =>
        new(
            title,
            description,
            category,
            subcategory,
            priority,
            reportedAt ?? Now,
            requester ?? Requester.For(Adam),
            channel,
            channel == ReportChannel.Agent ? registeredBy ?? Anna : registeredBy
        );

    /// <summary>A ticket built from its events, as Marten folds it.</summary>
    public static Ticket Reported(Priority priority = Priority.Medium, DateTimeOffset? reportedAt = null)
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        return Ticket.FromEvents([Ticket.Report(id, Draft(priority, reportedAt), Now)]);
    }

    public static Ticket TakenBy(AgentRef agent, Priority priority = Priority.Medium, DateTimeOffset? reportedAt = null)
    {
        var ticket = Reported(priority, reportedAt);
        return Ticket.Evolve(ticket, ticket.Take(agent, Now));
    }

    public static Ticket Then(this Ticket ticket, object? @event) =>
        @event is null ? ticket : Ticket.Evolve(ticket, @event);
}
