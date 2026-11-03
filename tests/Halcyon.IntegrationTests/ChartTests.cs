using Halcyon.App.Features.Reports;
using Halcyon.App.Features.Tickets;
using Halcyon.Database.Documents;
using Halcyon.Domain.Tickets;
using Halcyon.Domain.Time;
using Halcyon.IntegrationTests.Infrastructure;

namespace Halcyon.IntegrationTests;

/// <summary>Spec 009: the numbers behind the charts, counted from tickets built in memory (no database).</summary>
public class ChartNumbersTests
{
    // Tuesday 03.11.2026 10:00 UTC = 11:00 in Warsaw. Seven days: 28.10 … 03.11.
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    private static TicketView View(
        DateTimeOffset reported,
        DateTimeOffset? resolved = null,
        bool late = false,
        Priority priority = Priority.Medium,
        string subcategory = "it.hardware",
        TimeSpan? sla = null,
        string? agent = "Anna Nowak",
        Guid? agentId = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            CategoryKey = subcategory.Split('.')[0],
            SubcategoryKey = subcategory,
            Priority = priority,
            ReportedAt = reported,
            Deadline = reported + (sla ?? Sla.For(priority)),
            Status = resolved is null ? TicketStatus.InPool : TicketStatus.Done,
            ResolvedAt = resolved,
            ResolvedLate = late,
            AssigneeId = resolved is null ? null : agentId ?? Guid.Empty,
            AssigneeName = resolved is null ? null : agent,
        };

    private static ChartData Build(IReadOnlyList<TicketView> tickets, ChartFilter? filter = null)
    {
        filter ??= new ChartFilter(ChartRange.Days7);
        return ChartQueries.Build(filter, Now, ChartBuckets.For(filter.Range, ReportPeriods.Today(Now)), tickets);
    }

    private static readonly TicketView[] Week =
    [
        View(At(11, 1, 9), resolved: At(11, 2, 8)), // reported Sunday, resolved Monday on time
        View(At(10, 20, 9)), // reported before the range, still open and overdue
        View(At(11, 2, 10), resolved: At(11, 3, 9), late: true),
        View(At(10, 15, 9), resolved: At(10, 29, 12), late: true), // open at the end of the first day only
    ];

    [Fact]
    public void Flow_CountsPerBucket_AndTheBacklogAtTheEndOfEach()
    {
        var flow = Build(Week).Flow;

        flow.Select(p => p.Bucket.Label).ShouldBe(["28.10", "29.10", "30.10", "31.10", "01.11", "02.11", "03.11"]);
        flow.Select(p => p.Reported).ShouldBe([0, 0, 0, 0, 1, 1, 0]);
        flow.Select(p => p.Resolved).ShouldBe([0, 1, 0, 0, 0, 1, 1]);
        flow.Select(p => p.ResolvedLate).ShouldBe([0, 1, 0, 0, 0, 0, 1]);
        flow.Select(p => p.ResolvedOnTime).ShouldBe([0, 0, 0, 0, 0, 1, 0]);
        // Today ends now: the one resolved at 09:00 is no longer open.
        flow.Select(p => p.OpenAtEnd).ShouldBe([2, 1, 1, 1, 2, 2, 1]);
    }

    [Fact]
    public void KeyFigures()
    {
        var f = Build([.. Week, View(Now - TimeSpan.FromHours(7.5), priority: Priority.High)]).Figures;

        f.Reported.ShouldBe(3);
        f.Resolved.ShouldBe(3);
        f.ResolvedLate.ShouldBe(2);
        f.OnTimeShare!.Value.ShouldBe(1 / 3.0, 1e-9);
        f.MedianResolution.ShouldBe(TimeSpan.FromHours(23)); // 23 h, 23 h and 14 days 3 h
        f.OpenNow.ShouldBe(2);
        f.DueSoonNow.ShouldBe(1); // high, 30 min left
        f.OverdueNow.ShouldBe(1);
    }

    [Fact]
    public void KeyFigures_WithNothingResolved_HaveNoShareAndNoMedian()
    {
        var f = Build([View(At(11, 2, 9))]).Figures;
        f.OnTimeShare.ShouldBeNull();
        f.MedianResolution.ShouldBeNull();
        Build([]).Flow.ShouldAllBe(p => p.OpenAtEnd == 0);
    }

    [Fact]
    public void SlaUse_IsAShareOfTheTicketsOwnSla()
    {
        var start = At(11, 2, 0);
        TicketView Critical(double hours) =>
            View(start, resolved: start + TimeSpan.FromHours(hours), late: hours > 3, priority: Priority.Critical);

        var data = Build([
            Critical(1),
            Critical(2),
            Critical(3),
            Critical(4),
            Critical(6),
            View(start, resolved: start + TimeSpan.FromHours(2), priority: Priority.High),
            // Raised to Low after a change: the deadline moved, the share is against the new SLA.
            View(At(10, 30, 0), resolved: At(11, 2, 12), priority: Priority.Low, sla: TimeSpan.FromDays(7)),
        ]);

        var critical = data.Sla.Single(s => s.Priority == Priority.Critical);
        critical.Count.ShouldBe(5);
        critical.Median!.Value.ShouldBe(1, 1e-9);
        critical.P90!.Value.ShouldBe(2, 1e-9);
        critical.MedianTime.ShouldBe(TimeSpan.FromHours(3));

        var high = data.Sla.Single(s => s.Priority == Priority.High);
        high.Median!.Value.ShouldBe(0.25, 1e-9);
        high.P90.ShouldBeNull(); // fewer than five

        data.Sla.Single(s => s.Priority == Priority.Low).Median!.Value.ShouldBe(0.5, 1e-9);
        data.Sla.Single(s => s.Priority == Priority.Medium).Count.ShouldBe(0);
    }

    [Fact]
    public void ByCategory_BiggestFirst_SplitByOutcome()
    {
        var data = Build([
            View(At(11, 2, 9), resolved: At(11, 2, 12), subcategory: "legal.contracts"),
            View(At(11, 2, 9), resolved: At(11, 3, 9), late: true, subcategory: "it.hardware"),
            View(At(11, 2, 9), subcategory: "it.application"),
            View(At(10, 20, 9), subcategory: "it.application"), // reported before the range: not here
        ]);

        data.BySubcategory.ShouldBeFalse();
        data.ByCategory.Select(b => b.Label).ShouldBe(["IT", "Dział Prawny"]);
        data.ByCategory[0].Values.ShouldBe([0, 1, 1]);
        data.ByCategory[1].Values.ShouldBe([1, 0, 0]);
    }

    [Fact]
    public void QueueNow_ByPriority_WithDeadlineStates()
    {
        var data = Build([
            View(Now - TimeSpan.FromHours(4), priority: Priority.Critical), // overdue
            View(Now - TimeSpan.FromHours(2.5), priority: Priority.Critical), // 30 min left
            View(Now - TimeSpan.FromHours(1), priority: Priority.Critical),
            View(Now - TimeSpan.FromHours(1), priority: Priority.Low),
            View(At(11, 2, 9), resolved: At(11, 2, 12), priority: Priority.Low), // done: not in the queue
        ]);

        data.QueueByPriority.Select(b => b.Label).ShouldBe(["Krytyczny", "Wysoki", "Średni", "Niski"]);
        data.QueueByPriority[0].Values.ShouldBe([1, 1, 1]);
        data.QueueByPriority[1].Total.ShouldBe(0);
        data.QueueByPriority[3].Values.ShouldBe([1, 0, 0]);
    }

    [Fact]
    public void Agents_TopEight_TheRestFolded()
    {
        var tickets = Enumerable
            .Range(1, 10)
            .SelectMany(n =>
                Enumerable
                    .Range(0, n)
                    .Select(i =>
                        View(
                            At(11, 2, 8),
                            resolved: At(11, 2, 9),
                            late: i == 0,
                            agent: $"Agent {n:00}",
                            agentId: new Guid(n, 0, 0, new byte[8])
                        )
                    )
            )
            .ToList();

        var agents = Build(tickets).Agents;

        agents.Count.ShouldBe(9);
        agents[0].Label.ShouldBe("Agent 10");
        agents[0].Values.ShouldBe([9, 1]);
        agents[^1].Label.ShouldBe("Pozostali (2)");
        agents[^1].Values.ShouldBe([1, 2]); // agents 1 and 2: three tickets, two of them late
    }

    [Fact]
    public void Heatmap_UsesWarsawWeekdayAndHour()
    {
        // Sunday 01.11 23:30 UTC is Monday 00:30 in Warsaw; Tuesday 03.11 08:15 UTC is 09:15.
        var heat = Build([View(At(11, 1, 23, 30)), View(At(11, 3, 8, 15)), View(At(11, 3, 8, 45))]).Heat;

        heat[0][0].ShouldBe(1);
        heat[1][9].ShouldBe(2);
        heat.Sum(row => row.Sum()).ShouldBe(3);
    }

    [Fact]
    public void Weeks_CountWholeCalendarWeeks()
    {
        var data = Build([View(At(10, 26, 9)), View(At(11, 2, 9), resolved: At(11, 3, 9))], new(ChartRange.Weeks12));

        data.Flow.Count.ShouldBe(12);
        data.Flow[^2].Reported.ShouldBe(1); // 26.10–01.11
        data.Flow[^1].Reported.ShouldBe(1); // 02.11–08.11
        data.Flow[^1].Resolved.ShouldBe(1);
        data.RangeText.ShouldBe("17.08.2026 – 08.11.2026");
    }

    [Theory]
    [InlineData(new double[] { }, null, null)]
    [InlineData(new double[] { 5 }, 5.0, 5.0)]
    [InlineData(new double[] { 4, 1, 3, 2 }, 2.5, 4.0)]
    [InlineData(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5.5, 9.0)]
    public void MedianAndNearestRankP90(double[] values, double? median, double? p90)
    {
        ChartQueries.Median(values).ShouldBe(median);
        ChartQueries.Percentile(values, 0.9).ShouldBe(p90);
    }
}

/// <summary>Spec 009: the charts' query on a real database.</summary>
public class ChartTests(PostgresContainer postgres) : TicketTestBase(postgres)
{
    [Fact]
    public async Task TheQuery_LoadsTicketsOpenDuringTheRange_AndResolvedInIt()
    {
        var old = await ReportAsync(ago: TimeSpan.FromDays(20)); // before the range, still open
        var resolved = await ReportAsync(Priority.High, TimeSpan.FromDays(10));
        await App.Get<TakeTicket>().ExecuteAsync(resolved, Anna);
        await App.Get<ResolveTicket>().ExecuteAsync(resolved, Anna); // today, late
        await ReportAsync(ago: TimeSpan.FromHours(2));

        var data = await App.Get<ChartQueries>().RunAsync(new ChartFilter(ChartRange.Days7));

        data.Figures.Reported.ShouldBe(1);
        data.Figures.Resolved.ShouldBe(1);
        data.Figures.ResolvedLate.ShouldBe(1);
        data.Figures.OpenNow.ShouldBe(2);
        data.Flow[0].OpenAtEnd.ShouldBe(2); // the old one and the one resolved today
        data.Flow[^1].OpenAtEnd.ShouldBe(2);
        data.Agents.Single().Label.ShouldBe(Anna.FullName);
        (await ViewAsync(old)).Status.ShouldBe(TicketStatus.InPool);
    }

    [Fact]
    public async Task CategoryFilter_SplitsBySubcategory_AndFiltersEveryChart()
    {
        await ReportAsync(subcategory: "it.hardware");
        await ReportAsync(subcategory: "it.hardware");
        await ReportAsync(subcategory: "it.application");
        await ReportAsync(subcategory: "legal.contracts");

        var all = await App.Get<ChartQueries>().RunAsync(new ChartFilter());
        all.Figures.Reported.ShouldBe(4);
        all.ByCategory.Select(b => b.Label).ShouldBe(["IT", "Dział Prawny"]);

        var it = await App.Get<ChartQueries>().RunAsync(new ChartFilter(CategoryKey: "it"));
        it.BySubcategory.ShouldBeTrue();
        it.Figures.Reported.ShouldBe(3);
        it.Figures.OpenNow.ShouldBe(3);
        it.ByCategory.Select(b => (b.Key, b.Total)).ShouldBe([("it.hardware", 2), ("it.application", 1)]);
        it.QueueByPriority.Sum(b => b.Total).ShouldBe(3);
        it.Heat.Sum(row => row.Sum()).ShouldBe(3);
    }
}
