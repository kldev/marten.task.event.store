using System.Net;
using Halcyon.App.Monitoring;
using Halcyon.Database.Monitoring;
using Halcyon.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Halcyon.IntegrationTests;

/// <summary>ADR-0011: HTTP answers and the log in the database, the statistics, the retention and its cleanup.</summary>
public class MonitoringTests(PostgresContainer postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);
    private TestApp app = null!;

    public async ValueTask InitializeAsync() => app = await TestApp.StartAsync(postgres, now: Now);

    public async ValueTask DisposeAsync() => await app.DisposeAsync();

    private MonitoringQueues Queues => app.Get<MonitoringQueues>();
    private MonitoringQueries Queries => app.Get<MonitoringQueries>();

    private async Task FlushAsync()
    {
        // The writer runs on its own every 2 s too; FlushAsync waits for one already running, so after it nothing waits.
        await app.Get<MonitoringWriter>().FlushAsync();
        Queues.Requests.IsEmpty.ShouldBeTrue();
    }

    private static RequestRecord Request(int status, DateTimeOffset at, string path = "/x") =>
        new(at, "GET", path, status, 12.5, null, null);

    private async Task<long> CountAsync(string table, string where = "true")
    {
        await using var command = app.Get<NpgsqlDataSource>()
            .CreateCommand($"SELECT count(*) FROM {table} WHERE {where}");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task EveryAnswer_SuccessOrError_GoesToTheDatabase_ButNotTheProbes()
    {
        using var client = app.Client();
        (await client.GetAsync(new Uri("/logowanie", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(new Uri("/nie-ma-takiej-strony", UriKind.Relative))).StatusCode.ShouldBe(
            HttpStatusCode.NotFound
        );
        (await client.GetAsync(new Uri("/health", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);

        await FlushAsync();

        var all = await Queries.RequestsAsync(new RequestFilter());
        all.Rows.ShouldContain(r => r.Path == "/logowanie" && r.Status == 200 && r.Method == "GET");
        all.Rows.ShouldContain(r => r.Path == "/nie-ma-takiej-strony" && r.Status == 404);
        all.Rows.ShouldNotContain(r => r.Path.StartsWith("/health"));

        var notFound = await Queries.RequestsAsync(new RequestFilter(RequestOutcome.NotFound));
        notFound.Rows.ShouldAllBe(r => r.Status == 404);
        notFound.Rows.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task TheQueryString_IsNotRecorded()
    {
        using var client = app.Client();
        await client.GetAsync(new Uri("/logowanie?returnUrl=%2Fmoje&email=someone%40company.io", UriKind.Relative));
        await FlushAsync();

        await using var command = app.Get<NpgsqlDataSource>()
            .CreateCommand($"SELECT count(*) FROM {MonitoringSchema.Requests} WHERE path LIKE '%company%'");
        ((long)(await command.ExecuteScalarAsync())!).ShouldBe(0);
    }

    [Fact]
    public async Task Statistics_Count200_403_404_And500_InTheRange()
    {
        foreach (var status in new[] { 200, 200, 200, 302, 403, 404, 400, 500, 503 })
            Queues.Requests.Enqueue(Request(status, Now.AddMinutes(-5)));
        // Outside the 24 hours, inside 7 days.
        Queues.Requests.Enqueue(Request(500, Now.AddDays(-3)));
        await FlushAsync();

        var day = await Queries.StatisticsAsync(StatusRange.Hours24);
        day.Ok.ShouldBe(3);
        day.Redirects.ShouldBe(1);
        day.Forbidden.ShouldBe(1);
        day.NotFound.ShouldBe(1);
        day.OtherClientErrors.ShouldBe(1);
        day.InternalServerErrors.ShouldBe(1);
        day.ServerErrors.ShouldBe(2);
        day.Total.ShouldBe(9);
        day.Buckets.Count.ShouldBe(24);
        day.Buckets[^1].Success.ShouldBe(4);
        day.Buckets[^1].ClientErrors.ShouldBe(3);
        day.Buckets[^1].ServerErrors.ShouldBe(2);
        day.FailingPaths.ShouldContain(f => f.Path == "/x" && f.Status == 500 && f.Count == 1);

        var week = await Queries.StatisticsAsync(StatusRange.Days7);
        week.InternalServerErrors.ShouldBe(2);
        week.Buckets.Count.ShouldBe(7);
        week.Buckets.Sum(b => b.ServerErrors).ShouldBe(3);
    }

    [Fact]
    public async Task TheLog_FromInformationUp_GoesToTheDatabase_WithTheException()
    {
        var logger = app.Get<ILoggerFactory>().CreateLogger("Halcyon.Tests.Probe");
        logger.LogDebug("debug {Marker}", "below-the-level");
        logger.LogInformation("info {Marker}", "monitoring-info");
        logger.LogError(new InvalidOperationException("boom-in-test"), "error {Marker}", "monitoring-error");
        await app.Get<MonitoringWriter>().FlushAsync();

        var everything = await Queries.LogsAsync(new LogFilter(MinLevel: 2, Search: "monitoring-"));
        everything.Rows.Select(r => r.Message).ShouldBe(["error \"monitoring-error\"", "info \"monitoring-info\""]);

        var errors = await Queries.LogsAsync(new LogFilter(MinLevel: 4, Search: "monitoring-"));
        var error = errors.Rows.ShouldHaveSingleItem();
        error.Source.ShouldBe("Halcyon.Tests.Probe");
        error.Exception!.ShouldContain("boom-in-test");

        (await Queries.LogsAsync(new LogFilter(Search: "below-the-level"))).Rows.ShouldBeEmpty();
        // The request line has its own table; it is not repeated in the log.
        (await CountAsync(MonitoringSchema.Logs, "source = 'Serilog.AspNetCore.RequestLoggingMiddleware'")).ShouldBe(0);
    }

    [Fact]
    public async Task Cleanup_RemovesOnlyWhatIsOlderThan14Days()
    {
        Queues.Requests.Enqueue(Request(200, Now.AddDays(-15), "/old"));
        Queues.Requests.Enqueue(Request(500, Now.AddDays(-14).AddMinutes(-1), "/old"));
        Queues.Requests.Enqueue(Request(200, Now.AddDays(-13), "/recent"));
        Queues.Logs.Enqueue(new LogRecord(Now.AddDays(-20), 4, "Old", "old entry", null, null, null));
        Queues.Logs.Enqueue(new LogRecord(Now.AddDays(-1), 4, "Recent", "recent entry", null, null, null));
        await FlushAsync();

        var result = await app.Get<MonitoringCleanup>().RunOnceAsync();

        result.Cutoff.ShouldBe(Now.AddDays(-14));
        result.Requests.ShouldBe(2);
        result.Logs.ShouldBe(1);
        (await CountAsync(MonitoringSchema.Requests, "path = '/old'")).ShouldBe(0);
        (await CountAsync(MonitoringSchema.Requests, "path = '/recent'")).ShouldBe(1);
        (await CountAsync(MonitoringSchema.Logs, "source = 'Recent'")).ShouldBe(1);

        var job = app.Get<BackgroundJobs>().Cleanup.State();
        job.Runs.ShouldBeGreaterThanOrEqualTo(1);
        job.LastResult!.ShouldContain("Usunięto: 2 odpowiedzi HTTP, 1 wpisów logu");

        (await app.Get<MonitoringCleanup>().RunOnceAsync()).Requests.ShouldBe(0);
    }

    [Fact]
    public async Task Cleanup_WorksInBatches()
    {
        await using var small = await TestApp.StartAsync(
            postgres,
            settings: new() { ["Monitoring:CleanupBatchSize"] = "100" },
            now: Now
        );
        for (var i = 0; i < 250; i++)
            small.Get<MonitoringQueues>().Requests.Enqueue(Request(200, Now.AddDays(-30), "/batch"));
        await small.Get<MonitoringWriter>().FlushAsync();

        (await small.Get<MonitoringCleanup>().RunOnceAsync()).Requests.ShouldBe(250);
    }

    [Fact]
    public void AFullQueue_DropsNewEntries_AndCountsThem()
    {
        var queue = new MonitoringQueue<int>("test", "test", capacity: 3);
        for (var i = 0; i < 5; i++)
            queue.Enqueue(i);

        var state = queue.State();
        state.Pending.ShouldBe(3);
        state.Dropped.ShouldBe(2);
        queue.Take(10).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void TheDefaults_AreTheProductionOnes()
    {
        var options = app.Get<IOptions<MonitoringOptions>>().Value;
        options.Retention.ShouldBe(TimeSpan.FromDays(14));
        options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void IgnoredPaths_FromTheEnvironment_AreAddedToTheDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Monitoring:IgnoredPaths:0"] = "/metrics" })
            .Build();
        var options = new MonitoringOptions();
        configuration.GetSection("Monitoring").Bind(options);

        options.IgnoredPaths.ShouldContain("/metrics");
        options.IgnoredPaths.ShouldContain("/health");
    }

    [Theory]
    [InlineData("Monitoring:Retention", "00:10:00", "Retention")]
    [InlineData("Monitoring:CleanupInterval", "20.00:00:00", "CleanupInterval")]
    [InlineData("Monitoring:QueueCapacity", "5", "QueueCapacity")]
    public async Task AWrongSetting_StopsTheStart_WithTheReason(string key, string value, string field)
    {
        var refusal = await Should.ThrowAsync<Exception>(async () =>
        {
            await using var _ = await TestApp.StartAsync(postgres, settings: new() { [key] = value });
        });
        refusal.ToString().ShouldContain(field);
    }

    [Fact]
    public async Task DatabasePage_ReadsTheServer_TheTables_AndTheEventStore()
    {
        var snapshot = await app.Get<DatabaseActivity>().ReadAsync();

        snapshot.Server.Version.ShouldStartWith("PostgreSQL 18");
        snapshot.Server.DatabaseSizeBytes.ShouldBeGreaterThan(0);
        snapshot.Tables.ShouldContain(t => t.Schema == "halcyon" && t.Name == "http_requests");
        snapshot.Tables.ShouldContain(t => t.Schema == "events" && t.Name == "mt_events");
        snapshot.Events.LastSequence.ShouldBeGreaterThanOrEqualTo(0);

        await Should.NotThrowAsync(() => app.Get<DatabaseActivity>().EventProgressAsync());
    }
}
