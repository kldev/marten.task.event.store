using Halcyon.Domain.People;
using Halcyon.Domain.Tickets;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 008: the agent who took a ticket names the person (from the department) who does the work.</summary>
public class HandlerTests
{
    private static readonly PersonRef Halina = new(
        Guid.Parse("00000000-0000-0000-0000-0000000000d1"),
        "Halina",
        "Nowicka"
    );

    private static Ticket TakenHr(AgentRef agent)
    {
        var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var ticket = Ticket.FromEvents([
            Ticket.Report(id, TestData.Draft(category: "hr-poland", subcategory: "hr-poland.pl.leave"), TestData.Now),
        ]);
        return ticket.Then(ticket.Take(agent, TestData.Now));
    }

    [Fact]
    public void TheAgent_NamesSomebodyFromKadry_WithAReason()
    {
        var ticket = TakenHr(TestData.Anna);

        var e = ticket.AssignHandler(
            TestData.Anna,
            Halina,
            "Kadry",
            " Brak uprawnień w systemie kadrowym ",
            TestData.Now
        )!;

        e.Person.ShouldBe(Halina);
        e.Reason.ShouldBe("Brak uprawnień w systemie kadrowym");
        ticket.Then(e).Handler.ShouldBe(Halina);
        ticket.Then(e).Assignee.ShouldBe(TestData.Anna); // the ticket stays with the agent
    }

    [Fact]
    public void SomebodyFromAnotherDepartment_IsRefused_ForHr() =>
        Should
            .Throw<DomainException>(() =>
                TakenHr(TestData.Anna).AssignHandler(TestData.Anna, Halina, "Logistyka", null, TestData.Now)
            )
            .Message.ShouldContain("Kadry");

    [Fact]
    public void WithoutADepartment_TheCategoryTakesAnybody()
    {
        var ticket = TestData.TakenBy(TestData.Anna); // IT
        ticket.AssignHandler(TestData.Anna, Halina, "Logistyka", null, TestData.Now).ShouldNotBeNull();
    }

    [Fact]
    public void OnlyTheAgentTheTicketIsWith_AndNotInThePool()
    {
        Should.Throw<DomainException>(() =>
            TakenHr(TestData.Anna).AssignHandler(TestData.Piotr, Halina, "Kadry", null, TestData.Now)
        );
        Should.Throw<DomainException>(() =>
            TestData.Reported().AssignHandler(TestData.Anna, Halina, "Kadry", null, TestData.Now)
        );
    }

    [Fact]
    public void TheSamePerson_IsNothing_AndNullTakesThemOff()
    {
        var ticket = TakenHr(TestData.Anna);
        ticket = ticket.Then(ticket.AssignHandler(TestData.Anna, Halina, "Kadry", null, TestData.Now));

        ticket.AssignHandler(TestData.Anna, Halina, "Kadry", "znowu", TestData.Now).ShouldBeNull();
        var off = ticket.AssignHandler(TestData.Anna, null, null, null, TestData.Now)!;
        off.Person.ShouldBeNull();
        ticket.Then(off).Handler.ShouldBeNull();
    }

    [Fact]
    public void HrRecruitmentFinanceAndLegalisation_HaveTheirDepartments()
    {
        var catalog = Categories.CategoryCatalog.Default;
        catalog.FindCategory("hr-poland")!.Department.ShouldBe("Kadry");
        catalog.FindCategory("hr-abroad")!.Department.ShouldBe("Kadry");
        catalog.FindCategory("legalization")!.Department.ShouldBe("Kadry");
        catalog.FindCategory("recruitment")!.Department.ShouldBe("Rekrutacja");
        catalog.FindCategory("finance")!.Department.ShouldBe("Finanse");
        catalog.FindCategory("it")!.Department.ShouldBeNull();
    }
}
