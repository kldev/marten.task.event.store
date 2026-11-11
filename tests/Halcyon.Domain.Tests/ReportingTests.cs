using Halcyon.Domain.Categories;
using Halcyon.Domain.Tickets;
using Halcyon.Domain.ValueObjects;
using static Halcyon.Domain.Tests.TestData;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 002.</summary>
public class ReportingTests
{
    private static readonly Guid Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Report_PutsTicketInThePool_WithDeadlineFromPriority()
    {
        var reported = Ticket.Report(Id, Draft(Priority.Critical, Now.AddMinutes(-30)), Now);
        var ticket = Ticket.FromEvents([reported]);

        ticket.Status.ShouldBe(TicketStatus.InPool);
        ticket.Assignee.ShouldBeNull();
        ticket.Deadline.ShouldBe(Now.AddMinutes(-30).AddHours(3));
        ticket.OriginalPriority.ShouldBe(Priority.Critical);
    }

    [Fact]
    public void Report_TrimsTitleAndDescription()
    {
        var reported = Ticket.Report(Id, Draft(title: "  Drukarka  ", description: "\n opis \t"), Now);

        reported.Title.ShouldBe("Drukarka");
        reported.Description.ShouldBe("opis");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Report_WithoutTitle_IsRefused(string? title) =>
        Should.Throw<DomainException>(() => Ticket.Report(Id, Draft(title: title), Now)).Message.ShouldContain("Tytuł");

    [Fact]
    public void Report_WithoutDescription_IsRefused() =>
        Should
            .Throw<DomainException>(() => Ticket.Report(Id, Draft(description: " "), Now))
            .Message.ShouldContain("Opis");

    [Fact]
    public void Report_TooLongTitle_IsRefused() =>
        Should.Throw<DomainException>(() =>
            Ticket.Report(Id, Draft(title: new string('x', TicketTitle.MaxLength + 1)), Now)
        );

    [Fact]
    public void Report_TitleOfMaxLength_IsAccepted() =>
        Ticket
            .Report(Id, Draft(title: new string('x', TicketTitle.MaxLength)), Now)
            .Title.Length.ShouldBe(TicketTitle.MaxLength);

    [Fact]
    public void Report_TooLongDescription_IsRefused() =>
        Should.Throw<DomainException>(() =>
            Ticket.Report(Id, Draft(description: new string('x', TicketDescription.MaxLength + 1)), Now)
        );

    [Fact]
    public void Report_WithoutPriority_IsRefused() =>
        Should
            .Throw<DomainException>(() => Ticket.Report(Id, Draft(priority: null), Now))
            .Message.ShouldContain("priorytet");

    [Fact]
    public void Report_DatedMoreThanFiveMinutesAhead_IsRefused() =>
        Should
            .Throw<DomainException>(() => Ticket.Report(Id, Draft(reportedAt: Now.AddMinutes(5).AddSeconds(1)), Now))
            .Message.ShouldContain("przyszłości");

    [Fact]
    public void Report_DatedWithinClockTolerance_IsAccepted() =>
        Ticket.Report(Id, Draft(reportedAt: Now.AddMinutes(5)), Now).ReportedAt.ShouldBe(Now.AddMinutes(5));

    [Fact]
    public void Report_BackdatedThirtyDays_IsAccepted() =>
        Ticket.Report(Id, Draft(reportedAt: Now.AddDays(-30)), Now).ReportedAt.ShouldBe(Now.AddDays(-30));

    [Fact]
    public void Report_BackdatedMoreThanThirtyDays_IsRefused() =>
        Should.Throw<DomainException>(() => Ticket.Report(Id, Draft(reportedAt: Now.AddDays(-30).AddMinutes(-1)), Now));

    [Fact]
    public void Report_OldEnoughToBeOverdue_IsOverdueAtOnce()
    {
        var ticket = Ticket.FromEvents([Ticket.Report(Id, Draft(Priority.Critical, Now.AddHours(-4)), Now)]);
        ticket.StateAt(Now).ShouldBe(DeadlineState.Overdue);
    }

    [Fact]
    public void Report_StoresTheInstantInUtc()
    {
        var warsaw = new DateTimeOffset(2026, 11, 3, 13, 0, 0, TimeSpan.FromHours(1));
        Ticket.Report(Id, Draft(reportedAt: warsaw), Now).ReportedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("it", "legal.contracts")]
    [InlineData("legal", "it.hardware")]
    [InlineData("it", "it.nothing")]
    [InlineData("it", null)]
    [InlineData(null, "it.hardware")]
    [InlineData("hr", "hr.payroll")]
    public void Report_SubcategoryOutsideTheCategory_IsRefused(string? category, string? subcategory) =>
        Should.Throw<DomainException>(() =>
            Ticket.Report(Id, Draft(category: category, subcategory: subcategory), Now)
        );

    [Fact]
    public void Report_RetiredSubcategory_IsRefused()
    {
        var catalog = new CategoryCatalog([
            new Category("it", "IT", [new Subcategory("it.fax", "Fax", Active: false)]),
        ]);

        Should
            .Throw<DomainException>(() => Ticket.Report(Id, Draft(category: "it", subcategory: "it.fax"), Now, catalog))
            .Message.ShouldContain("nie przyjmuje");
    }

    [Fact]
    public void Report_Incognito_HasNoPerson()
    {
        var reported = Ticket.Report(Id, Draft(requester: Requester.Anonymous), Now);
        reported.Requester.Incognito.ShouldBeTrue();
        reported.Requester.DisplayName.ShouldBe("Incognito");
    }

    [Fact]
    public void Report_PersonAndIncognitoAtOnce_IsRefused() =>
        Should.Throw<DomainException>(() => Ticket.Report(Id, Draft(requester: new Requester(true, Adam)), Now));

    [Fact]
    public void Report_NeitherPersonNorIncognito_IsRefused() =>
        Should.Throw<DomainException>(() => Ticket.Report(Id, Draft(requester: new Requester(false, null)), Now));

    [Fact]
    public void Report_PersonWithoutName_IsRefused() =>
        Should.Throw<DomainException>(() =>
            Ticket.Report(Id, Draft(requester: Requester.For(new People.PersonRef(Guid.NewGuid(), " ", "Mazur"))), Now)
        );

    [Fact]
    public void Report_ByAgentWithoutAgent_IsRefused() =>
        Should.Throw<DomainException>(() =>
            Ticket.Report(Id, Draft() with { Channel = ReportChannel.Agent, RegisteredBy = null }, Now)
        );

    [Fact]
    public void Report_SelfServiceWithAgent_IsRefused() =>
        Should.Throw<DomainException>(() =>
            Ticket.Report(Id, Draft() with { Channel = ReportChannel.SelfService, RegisteredBy = Anna }, Now)
        );

    [Fact]
    public void Report_SelfService_HasNoAgent()
    {
        var reported = Ticket.Report(Id, Draft(channel: ReportChannel.SelfService), Now);
        reported.Channel.ShouldBe(ReportChannel.SelfService);
        reported.RegisteredBy.ShouldBeNull();
    }

    [Fact]
    public void Number_IsShortAndStable() =>
        TicketNumber.For(Guid.Parse("0a1b2c3d-0000-0000-0000-000000000000")).Value.ShouldBe("HAL-0A1B2C3D");

    /// <summary>Regression: version 7 ids made within the same ~65 s share their first 8 digits (the timestamp).</summary>
    [Fact]
    public void Number_OfTicketsReportedTheSameMinute_Differs()
    {
        var at = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        var numbers = Enumerable
            .Range(0, 200)
            .Select(i => TicketNumber.For(Guid.CreateVersion7(at.AddSeconds(i % 60))).Value)
            .ToList();
        numbers.Distinct().Count().ShouldBe(200);
        numbers.ShouldAllBe(n => n.Length == 12 && n.StartsWith("HAL-"));
    }
}
