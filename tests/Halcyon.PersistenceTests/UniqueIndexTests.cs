using Halcyon.Database;
using Halcyon.Database.Documents;
using Halcyon.PersistenceTests.Infrastructure;
using Marten;
using Npgsql;

namespace Halcyon.PersistenceTests;

/// <summary>
/// The unique indexes <see cref="HalcyonStore"/> declares hold in the database itself, where the app's own checks cannot
/// reach two requests at once: the ticket number (ADR-0014) and one e-mail per agent and per person.
/// </summary>
public class UniqueIndexTests(PostgresContainer postgres) : StoreTestBase(postgres)
{
    private static TicketView View(string number) => new() { Id = Guid.CreateVersion7(), Number = number };

    private static Agent AgentWith(string email) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            FirstName = "Anna",
            LastName = "Nowak",
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
        };

    private static Person PersonWith(string email) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            FirstName = "Adam",
            LastName = "Mazur",
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
        };

    private static bool IsUniqueViolation(Exception exception, string constraint) =>
        exception switch
        {
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } p => p.ConstraintName == constraint,
            { InnerException: { } inner } => IsUniqueViolation(inner, constraint),
            _ => false,
        };

    [Fact]
    public async Task ASecondTicketWithAGivenNumber_IsRefused_AndRecognisedAsNumberTaken()
    {
        await SaveAsync(View("HAL-1A2B3C4D"));

        var exception = await Should.ThrowAsync<Exception>(() => SaveAsync(View("HAL-1A2B3C4D")));

        HalcyonStore.IsNumberTaken(exception).ShouldBeTrue();
    }

    [Fact]
    public async Task TicketsWithDifferentNumbers_AreBothSaved()
    {
        await SaveAsync(View("HAL-00000001"));
        await SaveAsync(View("HAL-00000002"));

        await using var session = Store.QuerySession();
        (await session.Query<TicketView>().ToListAsync(TestContext.Current.CancellationToken)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ASecondAgentWithTheSameAddress_InAnotherCase_IsRefused()
    {
        await SaveAsync(AgentWith("anna.nowak@company.io"));

        var exception = await Should.ThrowAsync<Exception>(() => SaveAsync(AgentWith("Anna.Nowak@Company.io")));

        IsUniqueViolation(exception, "uidx_agent_email").ShouldBeTrue();
        HalcyonStore.IsNumberTaken(exception).ShouldBeFalse();
    }

    [Fact]
    public async Task ASecondPersonWithTheSameAddress_IsRefused()
    {
        await SaveAsync(PersonWith("adam.mazur@company.io"));

        var exception = await Should.ThrowAsync<Exception>(() => SaveAsync(PersonWith("adam.mazur@company.io")));

        IsUniqueViolation(exception, "uidx_person_email").ShouldBeTrue();
    }

    [Fact]
    public async Task AnAgentAndAPerson_MayShareAnAddress()
    {
        // Two lists, two indexes: an agent can also report tickets as a person.
        await SaveAsync(AgentWith("anna.nowak@company.io"));
        await SaveAsync(PersonWith("anna.nowak@company.io"));

        await using var session = Store.QuerySession();
        (await session.Query<Agent>().ToListAsync(TestContext.Current.CancellationToken)).Count.ShouldBe(1);
        (await session.Query<Person>().ToListAsync(TestContext.Current.CancellationToken)).Count.ShouldBe(1);
    }
}
