using Halcyon.App.Features.Tickets;
using Halcyon.Database.Documents;
using Halcyon.Database.Seed;
using Halcyon.Domain;
using Halcyon.Domain.Tickets;
using Halcyon.Domain.ValueObjects;
using Halcyon.IntegrationTests.Infrastructure;
using Marten;

namespace Halcyon.IntegrationTests;

/// <summary>
/// ADR-0015 on PostgreSQL 18 (Testcontainers): the value objects decide what is stored - in the seed, in a report's
/// fields, in the lookups by e-mail and by number.
/// </summary>
public class ValueObjectTests(PostgresContainer postgres) : TicketTestBase(postgres)
{
    [Fact]
    public async Task ASeedAgentWithAWrongPhone_StopsTheSeed_AndNothingIsStored()
    {
        var definition = new SeedDefinition([new SeedAgent("Jan", "Zły", "600-100-10x", "jan.zly@company.io")], [], []);

        var refusal = await Should.ThrowAsync<DomainException>(() => App.Get<HalcyonSeeder>().RunAsync(definition));

        refusal.Message.ShouldBe(PhoneNumber.InvalidMessage);
        await using var session = App.Store.QuerySession();
        (await session.Query<Agent>().AnyAsync(a => a.EmailNormalized == "jan.zly@company.io")).ShouldBeFalse();
    }

    [Fact]
    public async Task ASeedPerson_IsStoredAsTyped_AndFoundByTheNormalizedAddress()
    {
        var definition = new SeedDefinition(
            [],
            [new SeedPerson("Ewa", "Duża", "Kadry", "  Ewa.Duza@Company.IO ", "  +48  601 200 300 ")],
            []
        );

        await App.Get<HalcyonSeeder>().RunAsync(definition);

        var person = await App.PersonAsync("EWA.DUZA@company.io");
        person.Email.ShouldBe("Ewa.Duza@Company.IO");
        person.EmailNormalized.ShouldBe("ewa.duza@company.io");
        person.Phone.ShouldBe("+48 601 200 300");
    }

    [Fact]
    public async Task AReport_StoresItsFieldsInTheValueObjectsForm()
    {
        var reported = await App.Get<ReportTicket>()
            .ByAgentAsync(
                Anna,
                Command(subcategory: "finance.payments", title: "Przelew dla dostawcy") with
                {
                    Fields = new Dictionary<string, string?>
                    {
                        ["contractor"] = "Biuro Rachunkowe",
                        ["amount"] = "1 234,5",
                        ["currency"] = "pln",
                        ["bank-account"] = "pl61 1090 1014 0000 0712 1981 2874",
                    },
                }
            );

        var view = await ViewAsync(reported.TicketId);
        view.Fields["amount"].ShouldBe("1234.50");
        view.Fields["currency"].ShouldBe("PLN");
        view.Fields["bank-account"].ShouldBe("PL61109010140000071219812874");
    }

    [Fact]
    public async Task AWrongIban_IsRefused_NamingTheField_AndNoTicketIsStored()
    {
        var command = Command(subcategory: "finance.payments", title: "Przelew") with
        {
            Fields = new Dictionary<string, string?> { ["bank-account"] = "PL62 1090 1014 0000 0712 1981 2874" },
        };

        var refusal = await Should.ThrowAsync<DomainException>(() =>
            App.Get<ReportTicket>().ByAgentAsync(Anna, command)
        );

        refusal.Message.ShouldBe(Iban.InvalidMessage("Numer rachunku (IBAN)"));
        await using var session = App.Store.QuerySession();
        (await session.Query<TicketView>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ATicketNumber_TypedInLowerCase_FindsTheTicket()
    {
        var id = await ReportAsync();
        var number = (await ViewAsync(id)).Number;

        (await App.Get<TicketQueries>().FindByNumberAsync(number.ToLowerInvariant())).ShouldBe(id);
        number.ShouldBe(TicketNumber.For(id).Value);
    }

    [Fact]
    public async Task ATitleWithSpaces_IsStoredTrimmed_AndTheAggregateHoldsTheValueObject()
    {
        var reported = await App.Get<ReportTicket>().ByAgentAsync(Anna, Command(title: "   Monitor miga   "));

        (await ViewAsync(reported.TicketId)).Title.ShouldBe("Monitor miga");
        await using var session = App.Store.LightweightSession();
        var ticket = (await session.Events.FetchForWriting<Ticket>(reported.TicketId)).Aggregate!;
        ticket.Title.ShouldBe(TicketTitle.Create("Monitor miga"));
    }
}
