using Halcyon.Database.Monitoring;
using Microsoft.Extensions.Options;
using Npgsql;
using static Halcyon.App.Localization.Tr;

namespace Halcyon.App.Monitoring;

/// <summary>What one cleanup removed.</summary>
public sealed record CleanupResult(DateTimeOffset Cutoff, long Requests, long Logs);

/// <summary>
/// Removes HTTP answers and log entries older than <see cref="MonitoringOptions.Retention"/> (14 days by default) every
/// <see cref="MonitoringOptions.CleanupInterval"/>, the first time right after the start (ADR-0011). In batches, so a
/// long-stopped app does not hold one huge transaction. Idempotent: two replicas cleaning at once remove each row once.
/// </summary>
public sealed class MonitoringCleanup(
    NpgsqlDataSource dataSource,
    BackgroundJobs jobs,
    IOptions<MonitoringOptions> options,
    TimeProvider time,
    ILogger<MonitoringCleanup> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.CleanupInterval;
        // The real clock for the schedule (a test's fake clock would never tick); the cutoff comes from TimeProvider.
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception e) when (e is NpgsqlException or TimeoutException)
                {
                    jobs.Cleanup.Failed(time.GetUtcNow(), e.Message, DateTimeOffset.UtcNow + interval);
                    logger.LogWarning(e, "Monitoring cleanup failed; next attempt in {Interval}", interval);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown.
        }
    }

    /// <summary>One cleanup: everything older than now minus the retention.</summary>
    public async Task<CleanupResult> RunOnceAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        var now = time.GetUtcNow();
        var cutoff = now - settings.Retention;
        jobs.Cleanup.Started(now);

        var requests = await DeleteOlderAsync(MonitoringSchema.Requests, cutoff, settings.CleanupBatchSize, ct);
        var logs = await DeleteOlderAsync(MonitoringSchema.Logs, cutoff, settings.CleanupBatchSize, ct);

        var result = new CleanupResult(cutoff, requests, logs);
        jobs.Cleanup.Succeeded(
            time.GetUtcNow(),
            () =>
                T(
                    $"Usunięto: {requests} odpowiedzi HTTP, {logs} wpisów logu (starsze niż {cutoff:yyyy-MM-dd HH:mm} UTC)",
                    $"Removed: {requests} HTTP responses, {logs} log entries (older than {cutoff:yyyy-MM-dd HH:mm} UTC)"
                ),
            DateTimeOffset.UtcNow + settings.CleanupInterval
        );
        if (requests + logs > 0)
            logger.LogInformation(
                "Monitoring cleanup removed {Requests} HTTP answers and {Logs} log entries older than {Cutoff}",
                requests,
                logs,
                cutoff
            );
        return result;
    }

    private async Task<long> DeleteOlderAsync(string table, DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        // Oldest first by the index on "at"; one short transaction per batch.
        var sql = $"""
            DELETE FROM {table}
            WHERE id IN (SELECT id FROM {table} WHERE at < @cutoff ORDER BY at LIMIT @batch)
            """;
        long total = 0;
        while (true)
        {
            await using var command = dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("cutoff", cutoff);
            command.Parameters.AddWithValue("batch", batchSize);
            var removed = await command.ExecuteNonQueryAsync(ct);
            total += removed;
            if (removed < batchSize)
                return total;
        }
    }
}
