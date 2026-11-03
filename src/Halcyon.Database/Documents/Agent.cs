using Halcyon.Domain.People;

namespace Halcyon.Database.Documents;

/// <summary>
/// An agent: works tickets, signs in. No status of any kind (owner's decision) - an agent simply exists.
/// </summary>
public sealed class Agent
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";

    /// <summary>Lower-cased e-mail with a unique index: sign-in and the "one address, one agent" rule.</summary>
    public string EmailNormalized { get; set; } = "";

    public string FullName => $"{FirstName} {LastName}";

    public AgentRef ToRef() => new(Id, FullName);
}

/// <summary>
/// A person who reports tickets (an employee, not an agent). Never signs in; picks themselves from the list
/// on the public page (ADR-0005).
/// </summary>
public sealed class Person
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Department { get; set; } = "";
    public string Email { get; set; } = "";
    public string EmailNormalized { get; set; } = "";
    public string? Phone { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    public PersonRef ToRef() => new(Id, FirstName, LastName);
}
