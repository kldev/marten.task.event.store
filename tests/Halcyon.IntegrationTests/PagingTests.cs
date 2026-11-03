using Halcyon.App.Features.Tickets;
using Halcyon.Domain.Tickets;
using Halcyon.IntegrationTests.Infrastructure;

namespace Halcyon.IntegrationTests;

/// <summary>Spec 008: the lists are paged in the database; the total is counted only on request.</summary>
public class PagingTests(PostgresContainer postgres) : TicketTestBase(postgres)
{
    private TicketQueries Queries => App.Get<TicketQueries>();

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        // 13 open with different deadlines (some with the same one) and 8 resolved.
        for (var i = 0; i < 13; i++)
            await ReportAsync(i % 2 == 0 ? Priority.High : Priority.Low, TimeSpan.FromHours(i % 5), title: $"open {i}");
        for (var i = 0; i < 8; i++)
        {
            var id = await ReportAsync(Priority.Medium, TimeSpan.FromHours(i), title: $"done {i}");
            await App.Get<TakeTicket>().ExecuteAsync(id, Anna);
            App.Clock.Advance(TimeSpan.FromMinutes(1));
            await App.Get<ResolveTicket>().ExecuteAsync(id, Anna);
        }
    }

    [Theory]
    [InlineData(5)] // the border between open and resolved inside a page
    [InlineData(13)] // a page that ends exactly at the last open one
    [InlineData(7)]
    public async Task Pages_TogetherAreTheWholeList_InOrder_WithNoGapsOrDuplicates(int size)
    {
        var filter = new TicketFilter(TicketScope.All);
        var whole = await Queries.ListAsync(filter);
        whole.Count.ShouldBe(21);

        var paged = new List<Guid>();
        var page = 0;
        TicketPage result;
        do
        {
            result = await Queries.PageAsync(filter, page++, size);
            result.Items.Count.ShouldBeLessThanOrEqualTo(size);
            paged.AddRange(result.Items.Select(t => t.Id));
        } while (result.HasMore);

        paged.ShouldBe(whole.Select(t => t.Id));
        page.ShouldBe((21 + size - 1) / size);
    }

    [Fact]
    public async Task OpenFirstByDeadline_ThenResolvedLatestFirst()
    {
        var whole = await Queries.ListAsync(new TicketFilter(TicketScope.All));
        var open = whole.TakeWhile(t => t.Status != TicketStatus.Done).ToList();
        open.Count.ShouldBe(13);
        open.Select(t => t.Deadline).ShouldBeInOrder();
        whole.Skip(13).Select(t => t.ResolvedAt).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task APageAfterTheLast_IsEmpty_AndTheTotalIsCountedOnRequest()
    {
        var filter = new TicketFilter(TicketScope.All);
        var past = await Queries.PageAsync(filter, 10, 5);
        past.Items.ShouldBeEmpty();
        past.HasMore.ShouldBeFalse();

        (await Queries.CountAsync(filter)).ShouldBe(21);
        (await Queries.CountAsync(new TicketFilter(TicketScope.Pool))).ShouldBe(13);
        (await Queries.CountAsync(filter with { Completion = CompletionFilter.Done })).ShouldBe(8);
        (await Queries.PageAsync(filter with { Completion = CompletionFilter.Done }, 0, 5)).HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task Search_FindsByAPartOfTheTitle_OrTheNumber_AndWildcardsAreLetters()
    {
        var percent = await ReportAsync(title: "Rabat 50% nie działa");
        var underscore = await ReportAsync(title: "Plik raport_2026 uszkodzony");
        var all = new TicketFilter(TicketScope.All);

        (await Queries.ListAsync(all with { Search = "RABAT" })).Select(t => t.Id).ShouldBe([percent]);
        (await Queries.ListAsync(all with { Search = "%" })).Select(t => t.Id).ShouldBe([percent]);
        (await Queries.ListAsync(all with { Search = "_" })).Select(t => t.Id).ShouldBe([underscore]);
        (await Queries.ListAsync(all with { Search = "open 1" })).Count.ShouldBe(4); // open 1, 10, 11, 12

        var number = (await ViewAsync(underscore)).Number;
        (await Queries.ListAsync(all with { Search = number.ToLowerInvariant() }))
            .Select(t => t.Id)
            .ShouldBe([underscore]);
        (await Queries.CountAsync(all with { Search = "done" })).ShouldBe(8);
    }
}
