using Halcyon.App.Features.Tickets;
using Halcyon.Domain;
using Halcyon.Domain.Tickets;
using Halcyon.IntegrationTests.Infrastructure;

namespace Halcyon.IntegrationTests;

/// <summary>Spec 003: two agents on one ticket at the same moment - exactly one wins (ADR-0003).</summary>
public class ConcurrencyTests(PostgresContainer postgres) : TicketTestBase(postgres)
{
    private static async Task<(int Won, List<string> Refusals)> RaceAsync(params Func<Task>[] actions)
    {
        using var start = new ManualResetEventSlim();
        var tasks = actions
            .Select(action =>
                Task.Run(async () =>
                {
                    start.Wait();
                    try
                    {
                        await action();
                        return (string?)null;
                    }
                    catch (DomainException exception)
                    {
                        return exception.Message;
                    }
                })
            )
            .ToList();
        start.Set();
        var results = await Task.WhenAll(tasks);
        return (results.Count(r => r is null), results.OfType<string>().ToList());
    }

    [Fact]
    public async Task TwoAgentsTakingTheSameTicket_ExactlyOneGetsIt()
    {
        for (var round = 0; round < 10; round++)
        {
            var id = await ReportAsync(title: $"Wyścig {round}");
            var take = App.Get<TakeTicket>();

            var (won, refusals) = await RaceAsync(
                () => take.ExecuteAsync(id, Anna),
                () => take.ExecuteAsync(id, Piotr)
            );

            won.ShouldBe(1);
            refusals.Count.ShouldBe(1);
            var view = await ViewAsync(id);
            view.Status.ShouldBe(TicketStatus.InProgress);
            refusals[0].ShouldContain(view.AssigneeName!);
        }
    }

    [Fact]
    public async Task HandOverAndResolveAtOnce_OnlyOneHappens()
    {
        for (var round = 0; round < 10; round++)
        {
            var id = await ReportAsync(title: $"Wyścig {round}");
            await App.Get<TakeTicket>().ExecuteAsync(id, Anna);

            var (won, refusals) = await RaceAsync(
                () => App.Get<HandOverTicket>().ExecuteAsync(id, Anna, Piotr.Id, null),
                () => App.Get<ResolveTicket>().ExecuteAsync(id, Anna)
            );

            won.ShouldBe(1);
            refusals.Count.ShouldBe(1);
            var view = await ViewAsync(id);
            (view.Status == TicketStatus.Done || view.AssigneeId == Piotr.Id).ShouldBeTrue();
            (view.Status == TicketStatus.Done && view.AssigneeId == Piotr.Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task ReturnAndHandOverAtOnce_OnlyOneHappens()
    {
        var id = await ReportAsync();
        await App.Get<TakeTicket>().ExecuteAsync(id, Anna);

        var (won, _) = await RaceAsync(
            () => App.Get<ReturnTicketToPool>().ExecuteAsync(id, Anna, null),
            () => App.Get<HandOverTicket>().ExecuteAsync(id, Anna, Kasia.Id, null)
        );

        won.ShouldBe(1);
    }
}
