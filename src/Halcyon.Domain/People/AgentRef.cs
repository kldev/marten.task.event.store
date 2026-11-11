namespace Halcyon.Domain.People;

/// <summary>An agent as an event remembers them: the id and the name at that moment.</summary>
public sealed record AgentRef(Guid Id, string FullName);

/// <summary>A requester (a person, not an agent) as an event remembers them.</summary>
public sealed record PersonRef(Guid Id, string FirstName, string LastName)
{
    public string FullName => $"{FirstName} {LastName}";
}
