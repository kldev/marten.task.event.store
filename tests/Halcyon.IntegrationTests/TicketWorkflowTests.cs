using Halcyon.App.Features.Tickets;
using Halcyon.Domain;
using Halcyon.Domain.Tickets;
using Halcyon.IntegrationTests.Infrastructure;
using Marten;

namespace Halcyon.IntegrationTests;

/// <summary>Specs 002, 003, 004, 005 through the handlers, on the real store.</summary>
public class TicketWorkflowTests(PostgresContainer postgres) : TicketTestBase(postgres)
{
    [Fact]
    public async Task AgentReport_LandsInThePool_WithRequesterAndDeadline()
    {
        var id = await ReportAsync(Priority.High, TimeSpan.FromHours(1));

        var view = await ViewAsync(id);
        view.Status.ShouldBe(TicketStatus.InPool);
        view.AssigneeId.ShouldBeNull();
        view.RequesterName.ShouldBe("Adam Mazur");
        view.RequesterId.ShouldBe(Adam.Id);
        view.Channel.ShouldBe(ReportChannel.Agent);
        view.RegisteredByName.ShouldBe("Anna Nowak");
        view.Deadline.ShouldBe(Now.AddHours(-1).AddHours(8));
        view.Number.ShouldStartWith("HAL-");
    }

    [Fact]
    public async Task PublicReport_Incognito_UsesTheMomentOfSending()
    {
        var reported = await App.Get<ReportTicket>()
            .SelfServiceAsync(Command(Priority.Low, reportedAt: Now.AddDays(-3), incognito: true));

        var view = await ViewAsync(reported.TicketId);
        view.RequesterIncognito.ShouldBeTrue();
        view.RequesterName.ShouldBe("Incognito");
        view.Channel.ShouldBe(ReportChannel.SelfService);
        view.RegisteredByName.ShouldBeNull();
        view.ReportedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task PublicReport_AsAPersonFromTheList()
    {
        var reported = await App.Get<ReportTicket>().SelfServiceAsync(Command(Priority.Critical));
        (await ViewAsync(reported.TicketId)).RequesterName.ShouldBe("Adam Mazur");
    }

    [Fact]
    public async Task Report_ForAnUnknownPerson_IsRefused() =>
        (
            await Should.ThrowAsync<DomainException>(() =>
                App.Get<ReportTicket>().ByAgentAsync(Anna, Command(personId: Guid.NewGuid()))
            )
        ).Message.ShouldContain("Nie ma takiej osoby");

    [Fact]
    public async Task Report_WithAnAgentAsTheRequester_IsRefused() =>
        (
            await Should.ThrowAsync<DomainException>(() =>
                App.Get<ReportTicket>().ByAgentAsync(Anna, Command(personId: Piotr.Id))
            )
        ).Message.ShouldContain("Agent nie może być zgłaszającym");

    [Fact]
    public async Task Report_PersonAndIncognitoAtOnce_IsRefused() =>
        await Should.ThrowAsync<DomainException>(() =>
            App.Get<ReportTicket>().ByAgentAsync(Anna, Command(personId: Adam.Id) with { Incognito = true })
        );

    [Fact]
    public async Task Report_ByAgentWithoutDate_IsRefused() =>
        await Should.ThrowAsync<DomainException>(() =>
            App.Get<ReportTicket>().ByAgentAsync(Anna, Command() with { ReportedAt = null })
        );

    [Fact]
    public async Task Report_InTheFuture_IsRefused_AndNothingIsStored()
    {
        await Should.ThrowAsync<DomainException>(() =>
            App.Get<ReportTicket>().ByAgentAsync(Anna, Command(reportedAt: Now.AddHours(1)))
        );
        await using var session = App.Store.QuerySession();
        (await session.Query<Database.Documents.TicketView>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task TakeHandOverReturnResolve_UpdateTheView_AndKeepTheDeadline()
    {
        var id = await ReportAsync(Priority.High);
        var deadline = (await ViewAsync(id)).Deadline;

        await App.Get<TakeTicket>().ExecuteAsync(id, Anna);
        (await ViewAsync(id)).AssigneeName.ShouldBe("Anna Nowak");

        await App.Get<HandOverTicket>().ExecuteAsync(id, Anna, Piotr.Id, "urlop");
        var handed = await ViewAsync(id);
        handed.AssigneeId.ShouldBe(Piotr.Id);
        handed.Deadline.ShouldBe(deadline);

        await App.Get<ReturnTicketToPool>().ExecuteAsync(id, Piotr, null);
        var returned = await ViewAsync(id);
        returned.Status.ShouldBe(TicketStatus.InPool);
        returned.AssigneeId.ShouldBeNull();
        returned.Deadline.ShouldBe(deadline);

        await App.Get<TakeTicket>().ExecuteAsync(id, Kasia);
        App.Clock.Advance(TimeSpan.FromHours(9));
        var resolved = await App.Get<ResolveTicket>().ExecuteAsync(id, Kasia);

        resolved!.Late.ShouldBeTrue();
        var done = await ViewAsync(id);
        done.Status.ShouldBe(TicketStatus.Done);
        done.ResolvedLate.ShouldBeTrue();
    }

    [Fact]
    public async Task HandOver_ToAnUnknownAgent_IsRefused()
    {
        var id = await ReportAsync();
        await App.Get<TakeTicket>().ExecuteAsync(id, Anna);

        (
            await Should.ThrowAsync<DomainException>(() =>
                App.Get<HandOverTicket>().ExecuteAsync(id, Anna, Adam.Id, null)
            )
        ).Message.ShouldContain("Nie ma takiego agenta");
    }

    [Fact]
    public async Task Action_OnAnUnknownTicket_IsRefused() =>
        (
            await Should.ThrowAsync<DomainException>(() => App.Get<TakeTicket>().ExecuteAsync(Guid.NewGuid(), Anna))
        ).Message.ShouldContain("Nie ma takiego zgłoszenia");

    [Fact]
    public async Task PriorityChange_RecomputesFromTheReportDate_AndSamePriorityStoresNothing()
    {
        var id = await ReportAsync(Priority.Low, TimeSpan.FromHours(4));

        (await App.Get<ChangeTicketPriority>().ExecuteAsync(id, Piotr, Priority.Low, null)).ShouldBeNull();
        await App.Get<ChangeTicketPriority>().ExecuteAsync(id, Piotr, Priority.Critical, "awaria");

        var view = await ViewAsync(id);
        view.Priority.ShouldBe(Priority.Critical);
        view.OriginalPriority.ShouldBe(Priority.Low);
        view.Deadline.ShouldBe(Now.AddHours(-4).AddHours(3));
        view.StateAt(Now).ShouldBe(DeadlineState.Overdue);

        await using var session = App.Store.QuerySession();
        (await session.Events.FetchStreamAsync(id)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Comments_AreStored_AlsoOnAResolvedTicket_AndShownInOrder()
    {
        var id = await ReportAsync();
        await App.Get<TakeTicket>().ExecuteAsync(id, Anna);
        App.Clock.Advance(TimeSpan.FromMinutes(5));
        await App.Get<CommentOnTicket>().ExecuteAsync(id, Piotr, "  Sprawdzam zasilacz.  ");
        App.Clock.Advance(TimeSpan.FromMinutes(5));
        await App.Get<ResolveTicket>().ExecuteAsync(id, Anna);
        App.Clock.Advance(TimeSpan.FromMinutes(5));
        await App.Get<CommentOnTicket>().ExecuteAsync(id, Kasia, "<b>Dzięki</b>");

        var details = (await App.Get<TicketQueries>().DetailsAsync(id))!;
        details.Ticket.CommentCount.ShouldBe(2);
        details
            .Timeline.Select(e => e.Kind)
            .ShouldBe([
                TimelineKind.Reported,
                TimelineKind.Taken,
                TimelineKind.Commented,
                TimelineKind.Resolved,
                TimelineKind.Commented,
            ]);
        var comments = details.Timeline.Where(e => e.Kind == TimelineKind.Commented).ToList();
        comments[0].Note.ShouldBe("Sprawdzam zasilacz.");
        comments[0].Actor.ShouldBe("Piotr Kowalski");
        comments[1].Note.ShouldBe("<b>Dzięki</b>");
    }

    [Fact]
    public async Task EmptyComment_IsRefused()
    {
        var id = await ReportAsync();
        await Should.ThrowAsync<DomainException>(() => App.Get<CommentOnTicket>().ExecuteAsync(id, Anna, "   "));
    }

    [Fact]
    public async Task Timeline_TellsTheWholeStory()
    {
        var id = await ReportAsync(Priority.Medium);
        await App.Get<TakeTicket>().ExecuteAsync(id, Anna);
        await App.Get<HandOverTicket>().ExecuteAsync(id, Anna, Piotr.Id, "urlop");
        await App.Get<ChangeTicketPriority>().ExecuteAsync(id, Piotr, Priority.High, null);
        await App.Get<ReturnTicketToPool>().ExecuteAsync(id, Piotr, "nie mój obszar");

        var timeline = (await App.Get<TicketQueries>().DetailsAsync(id))!.Timeline;

        timeline
            .Select(e => e.Kind)
            .ShouldBe([
                TimelineKind.Reported,
                TimelineKind.Taken,
                TimelineKind.HandedOver,
                TimelineKind.PriorityChanged,
                TimelineKind.Returned,
            ]);
        timeline[2].Text.ShouldContain("Piotr Kowalski");
        timeline[2].Note.ShouldBe("urlop");
        timeline[3].Text.ShouldContain("Średni → Wysoki");
    }
}
