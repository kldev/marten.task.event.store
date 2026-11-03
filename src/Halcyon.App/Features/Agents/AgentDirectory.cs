using Halcyon.Database.Documents;
using Halcyon.Domain.Tickets;
using Marten;

namespace Halcyon.App.Features.Agents;

public sealed record AgentRow(Guid Id, string FirstName, string LastName, string Phone, string Email, int InProgress)
{
    public string FullName => $"{FirstName} {LastName}";
}

public sealed record PersonRow(
    Guid Id,
    string FirstName,
    string LastName,
    string Department,
    string Email,
    string? Phone
)
{
    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>Agents see each other (spec 001) and the people who report tickets.</summary>
public sealed class AgentDirectory(IDocumentStore store)
{
    public async Task<IReadOnlyList<AgentRow>> AgentsAsync(CancellationToken ct = default)
    {
        await using var session = store.QuerySession();
        var agents = await session.Query<Agent>().ToListAsync(ct);
        var inProgress = await session
            .Query<TicketView>()
            .Where(t => t.Status == TicketStatus.InProgress)
            .ToListAsync(ct);
        var counts = inProgress
            .Where(t => t.AssigneeId.HasValue)
            .GroupBy(t => t.AssigneeId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return agents
            .OrderBy(a => a.LastName, StringComparer.Create(PolishCulture, false))
            .ThenBy(a => a.FirstName, StringComparer.Create(PolishCulture, false))
            .Select(a => new AgentRow(a.Id, a.FirstName, a.LastName, a.Phone, a.Email, counts.GetValueOrDefault(a.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<PersonRow>> PeopleAsync(CancellationToken ct = default)
    {
        await using var session = store.QuerySession();
        var people = await session.Query<Person>().ToListAsync(ct);
        return people
            .OrderBy(p => p.LastName, StringComparer.Create(PolishCulture, false))
            .ThenBy(p => p.FirstName, StringComparer.Create(PolishCulture, false))
            .Select(p => new PersonRow(p.Id, p.FirstName, p.LastName, p.Department, p.Email, p.Phone))
            .ToList();
    }

    private static readonly System.Globalization.CultureInfo PolishCulture = new("pl-PL");
}
