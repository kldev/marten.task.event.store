using static Halcyon.App.Localization.Tr;

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

/// <summary>
/// One job's record of its runs; written by the job, read by the page. The description and the last result are
/// functions: a job runs with no reader's language, the page asks for the text in its own (ADR-0012).
/// </summary>
public sealed class BackgroundJob(string name, Func<string> description, TimeSpan interval)
{
    private readonly Lock gate = new();
    private long runs;
    private long failures;
    private DateTimeOffset? lastStartedAt;
    private DateTimeOffset? lastFinishedAt;
    private Func<string>? lastResult;
    private string? lastError;
    private DateTimeOffset? nextRunAt;

    public string Name => name;

    public void Started(DateTimeOffset at)
    {
        lock (gate)
            lastStartedAt = at;
    }

    public void Succeeded(DateTimeOffset at, Func<string> result, DateTimeOffset? next)
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

    public BackgroundJobState State()
    {
        lock (gate)
            return new BackgroundJobState(
                name,
                description(),
                interval,
                runs,
                failures,
                lastStartedAt,
                lastFinishedAt,
                lastResult?.Invoke(),
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
            () =>
                T(
                    "Przenosi kolejki odpowiedzi HTTP i logów do bazy (COPY, partiami).",
                    "Moves the queues of HTTP responses and log entries to the database (COPY, in batches)."
                ),
            options.Value.FlushInterval
        );

    public BackgroundJob Cleanup { get; } =
        new(
            "monitoring-cleanup",
            () =>
                T(
                    $"Usuwa odpowiedzi HTTP i wpisy logu starsze niż {options.Value.Retention.TotalDays:0.##} dni.",
                    $"Removes HTTP responses and log entries older than {options.Value.Retention.TotalDays:0.##} days."
                ),
            options.Value.CleanupInterval
        );

    public IReadOnlyList<BackgroundJobState> States() => [Writer.State(), Cleanup.State()];
}
