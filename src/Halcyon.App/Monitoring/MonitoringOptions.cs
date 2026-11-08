using System.ComponentModel.DataAnnotations;
using Serilog.Events;

namespace Halcyon.App.Monitoring;

/// <summary>
/// What the app keeps about itself in the database and for how long (ADR-0011). Bound from "Monitoring", validated at
/// start - a wrong value stops the app with the reason. The defaults are for production. TimeSpan in .NET format:
/// "14.00:00:00" = 14 days, "01:00:00" = an hour, "00:00:02" = 2 seconds.
/// </summary>
public sealed class MonitoringOptions : IValidatableObject
{
    public const string Section = "Monitoring";

    /// <summary>How long HTTP answers and log entries stay in the database (Monitoring__Retention).</summary>
    public TimeSpan Retention { get; init; } = TimeSpan.FromDays(14);

    /// <summary>How often the cleanup removes what is older than the retention (Monitoring__CleanupInterval).</summary>
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Rows one DELETE removes; the cleanup repeats until nothing is left (Monitoring__CleanupBatchSize).</summary>
    [Range(100, 100_000)]
    public int CleanupBatchSize { get; init; } = 5_000;

    /// <summary>How often the writer moves the queues to the database (Monitoring__FlushInterval).</summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Rows one COPY writes (Monitoring__WriteBatchSize).</summary>
    [Range(10, 10_000)]
    public int WriteBatchSize { get; init; } = 500;

    /// <summary>
    /// Entries a queue holds before it drops new ones (Monitoring__QueueCapacity): a database that does not answer must
    /// not take the app's memory with it. Dropped entries are counted on the queues page.
    /// </summary>
    [Range(100, 1_000_000)]
    public int QueueCapacity { get; init; } = 10_000;

    /// <summary>The lowest level of the log kept in the database (Monitoring__LogLevel); the console keeps everything.</summary>
    public LogEventLevel LogLevel { get; init; } = LogEventLevel.Information;

    /// <summary>
    /// Path prefixes not recorded as HTTP answers: probes, Blazor's circuit and static files would bury the pages and the
    /// endpoints. Monitoring__IgnoredPaths__0, __1, ... are added to these (the binder appends to an array).
    /// </summary>
    public string[] IgnoredPaths { get; init; } =
    ["/health", "/_blazor", "/_framework", "/_content", "/css", "/favicon"];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Retention < TimeSpan.FromHours(1))
            yield return new($"{nameof(Retention)} must be at least an hour, got {Retention}.", [nameof(Retention)]);
        if (CleanupInterval < TimeSpan.FromMinutes(1))
            yield return new(
                $"{nameof(CleanupInterval)} must be at least a minute, got {CleanupInterval}.",
                [nameof(CleanupInterval)]
            );
        if (CleanupInterval > Retention)
            yield return new(
                $"{nameof(CleanupInterval)} ({CleanupInterval}) cannot be longer than {nameof(Retention)} ({Retention}).",
                [nameof(CleanupInterval)]
            );
        if (FlushInterval < TimeSpan.FromMilliseconds(100) || FlushInterval > TimeSpan.FromMinutes(1))
            yield return new(
                $"{nameof(FlushInterval)} must be between 100 ms and a minute, got {FlushInterval}.",
                [nameof(FlushInterval)]
            );
        if (QueueCapacity < WriteBatchSize)
            yield return new(
                $"{nameof(QueueCapacity)} ({QueueCapacity}) cannot be smaller than {nameof(WriteBatchSize)} ({WriteBatchSize}).",
                [nameof(QueueCapacity)]
            );
        if (IgnoredPaths.Any(p => !p.StartsWith('/')))
            yield return new($"Every entry of {nameof(IgnoredPaths)} must start with '/'.", [nameof(IgnoredPaths)]);
    }
}
