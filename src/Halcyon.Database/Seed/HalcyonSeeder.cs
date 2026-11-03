using Halcyon.Database.Documents;
using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;
using Marten;
using Microsoft.Extensions.Logging;

namespace Halcyon.Database.Seed;

public sealed record SeedDefinition(
    IReadOnlyList<SeedAgent> Agents,
    IReadOnlyList<SeedPerson> People,
    IReadOnlyList<SeedTicket> Tickets
)
{
    public static SeedDefinition Default { get; } = new(SeedData.Agents, SeedData.People, SeedData.Tickets);
}

public sealed record SeedResult(int AgentsAdded, int PeopleAdded, int TicketsAdded)
{
    public bool NothingAdded => AgentsAdded == 0 && PeopleAdded == 0 && TicketsAdded == 0;
}

/// <summary>
/// Inserts what is missing by a stable key and never overwrites (ADR-0006). Tickets are built with the domain's own
/// decisions, so seeded streams obey the same rules as real ones.
/// </summary>
public sealed class HalcyonSeeder(IDocumentStore store, TimeProvider time, ILogger<HalcyonSeeder> logger)
{
    public async Task<SeedResult> RunAsync(SeedDefinition? definition = null, CancellationToken ct = default)
    {
        definition ??= SeedDefinition.Default;
        var now = time.GetUtcNow();

        await using var session = store.LightweightSession();

        var agentsAdded = await InsertMissingAsync(
            session,
            definition.Agents.Select(a => new Agent
            {
                Id = StableId.Agent(a.Email),
                FirstName = a.FirstName,
                LastName = a.LastName,
                Phone = a.Phone,
                Email = a.Email,
                EmailNormalized = Emails.Normalize(a.Email),
            }),
            x => x.Id,
            ct
        );

        var peopleAdded = await InsertMissingAsync(
            session,
            definition.People.Select(p => new Person
            {
                Id = StableId.Person(p.Email),
                FirstName = p.FirstName,
                LastName = p.LastName,
                Department = p.Department,
                Email = p.Email,
                EmailNormalized = Emails.Normalize(p.Email),
                Phone = p.Phone,
            }),
            x => x.Id,
            ct
        );

        // People and agents first: tickets refer to them by the names stored in the database (a renamed seed entry
        // does not rename an existing record, so the events must not either).
        await session.SaveChangesAsync(ct);

        var agents = (await session.Query<Agent>().ToListAsync(ct)).ToDictionary(a => a.EmailNormalized);
        var people = (await session.Query<Person>().ToListAsync(ct)).ToDictionary(p => p.EmailNormalized);

        var ticketsAdded = 0;
        foreach (var seed in definition.Tickets)
        {
            var id = StableId.Ticket(seed.Key);
            if (await session.Events.FetchStreamStateAsync(id, ct) is not null)
                continue;

            session.Events.StartStream<Ticket>(id, [.. BuildEvents(id, seed, now, agents, people)]);
            ticketsAdded++;
        }
        await session.SaveChangesAsync(ct);

        var result = new SeedResult(agentsAdded, peopleAdded, ticketsAdded);
        logger.LogInformation(
            "Seed: {Agents} agent(s), {People} person(s), {Tickets} ticket(s) added",
            result.AgentsAdded,
            result.PeopleAdded,
            result.TicketsAdded
        );
        return result;
    }

    private static async Task<int> InsertMissingAsync<T>(
        IDocumentSession session,
        IEnumerable<T> candidates,
        Func<T, Guid> id,
        CancellationToken ct
    )
        where T : notnull
    {
        var list = candidates.ToList();
        var ids = list.Select(id).ToArray();
        var existing = (await session.LoadManyAsync<T>(ct, ids)).Select(id).ToHashSet();
        var missing = list.Where(x => !existing.Contains(id(x))).ToList();
        foreach (var doc in missing)
            session.Insert(doc);
        return missing.Count;
    }

    private static List<object> BuildEvents(
        Guid id,
        SeedTicket seed,
        DateTimeOffset now,
        Dictionary<string, Agent> agents,
        Dictionary<string, Person> people
    )
    {
        AgentRef Agent(string email) =>
            agents.TryGetValue(Emails.Normalize(email), out var a)
                ? a.ToRef()
                : throw new InvalidOperationException($"Seed ticket {seed.Key}: unknown agent {email}.");

        Person Person(string email) =>
            people.TryGetValue(Emails.Normalize(email), out var p)
                ? p
                : throw new InvalidOperationException($"Seed ticket {seed.Key}: unknown person {email}.");

        var requester =
            seed.RequesterEmail is null ? Requester.Anonymous
            : people.TryGetValue(Emails.Normalize(seed.RequesterEmail), out var person) ? Requester.For(person.ToRef())
            : throw new InvalidOperationException($"Seed ticket {seed.Key}: unknown person {seed.RequesterEmail}.");

        var category =
            Domain.Categories.CategoryCatalog.Default.FindSubcategory(seed.SubcategoryKey)
            ?? throw new InvalidOperationException(
                $"Seed ticket {seed.Key}: unknown subcategory {seed.SubcategoryKey}."
            );

        var reportedAt = TruncateToMinute(now - seed.ReportedAgo);
        var draft = new TicketDraft(
            seed.Title,
            seed.Description,
            category.Category.Key,
            seed.SubcategoryKey,
            seed.Priority,
            reportedAt,
            requester,
            seed.RegisteredByEmail is null ? ReportChannel.SelfService : ReportChannel.Agent,
            seed.RegisteredByEmail is null ? null : Agent(seed.RegisteredByEmail),
            seed.Fields
        );

        var events = new List<object> { Ticket.Report(id, draft, reportedAt) };
        var ticket = Ticket.FromEvents(events);

        foreach (var step in seed.Steps)
        {
            var at = now - step.Ago;
            object? e = step switch
            {
                Take t => ticket.Take(Agent(t.AgentEmail), at),
                HandOver h => ticket.HandOver(Agent(h.FromEmail), Agent(h.ToEmail), h.Reason, at),
                Return r => ticket.ReturnToPool(Agent(r.AgentEmail), r.Reason, at),
                ChangePriority p => ticket.ChangePriority(Agent(p.AgentEmail), p.To, p.Reason, at),
                Resolve r => ticket.Resolve(Agent(r.AgentEmail), at),
                Comment c => ticket.Comment(
                    StableId.For("comment", $"{seed.Key}:{c.Ago}"),
                    Agent(c.AgentEmail),
                    c.Text,
                    at
                ),
                FillFields f => ticket.UpdateFields(Agent(f.AgentEmail), f.Values, at),
                AssignHandler h => ticket.AssignHandler(
                    Agent(h.AgentEmail),
                    Person(h.PersonEmail).ToRef(),
                    Person(h.PersonEmail).Department,
                    h.Reason,
                    at
                ),
                _ => throw new InvalidOperationException($"Unknown seed step {step.GetType().Name}."),
            };
            if (e is null)
                continue;
            events.Add(e);
            ticket = Ticket.Evolve(ticket, e);
        }

        return events;
    }

    private static DateTimeOffset TruncateToMinute(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Offset);
}
