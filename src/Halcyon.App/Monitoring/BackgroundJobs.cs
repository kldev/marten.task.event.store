namespace Halcyon.App.Monitoring;

/// <summary>What the queues page shows about a job running in the background of this process.</summary>
public sealed record BackgroundJobState(
    string Name,
    string Description,
    TimeSpan Interval,
    long Runs,
    long Failures,
    DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastFinishedAt,
    string? LastResult,
    string? LastError,
    DateTimeOffset? NextRunAt
);

/// <summary>One job's record of its runs; written by the job, read by the page.</summary>
public sealed class BackgroundJob(string name, string description, TimeSpan interval)
{
    private readonly Lock gate = new();
    private long runs;
    private long failures;
    private DateTimeOffset? lastStartedAt;
    private DateTimeOffset? lastFinishedAt;
    private string? lastResult;
    private string? lastError;
    private DateTimeOffset? nextRunAt;

    public string Name => name;

    public void Started(DateTimeOffset at)
    {
        lock (gate)
            lastStartedAt = at;
    }

    public void Succeeded(DateTimeOffset at, string result, DateTimeOffset? next)
    {
        lock (gate)
        {
            runs++;
            lastFinishedAt = at;
            lastResult = result;
            lastError = null;
            nextRunAt = next;
        }
    }

    public void Failed(DateTimeOffset at, string error, DateTimeOffset? next)
    {
        lock (gate)
        {
            runs++;
            failures++;
            lastFinishedAt = at;
            lastError = error;
            nextRunAt = next;
        }
    }

    public void Scheduled(DateTimeOffset next)
    {
        lock (gate)
            nextRunAt = next;
    }

    public BackgroundJobState State()
    {
        lock (gate)
            return new BackgroundJobState(
                name,
                description,
                interval,
                runs,
                failures,
                lastStartedAt,
                lastFinishedAt,
                lastResult,
                lastError,
                nextRunAt
            );
    }
}

/// <summary>The background jobs of the process (ADR-0011), for the queues page.</summary>
public sealed class BackgroundJobs(Microsoft.Extensions.Options.IOptions<MonitoringOptions> options)
{
    public BackgroundJob Writer { get; } =
        new(
            "monitoring-writer",
            "Przenosi kolejki odpowiedzi HTTP i logów do bazy (COPY, partiami).",
            options.Value.FlushInterval
        );

    public BackgroundJob Cleanup { get; } =
        new(
            "monitoring-cleanup",
            $"Usuwa odpowiedzi HTTP i wpisy logu starsze niż {options.Value.Retention.TotalDays:0.##} dni.",
            options.Value.CleanupInterval
        );

    public IReadOnlyList<BackgroundJobState> States() => [Writer.State(), Cleanup.State()];
}
