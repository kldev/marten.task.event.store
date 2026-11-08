using Halcyon.Database.Monitoring;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using static Halcyon.App.Localization.Tr;

namespace Halcyon.App.Monitoring;

/// <summary>
/// Moves both queues to the database every <see cref="MonitoringOptions.FlushInterval"/> with binary COPY (ADR-0011).
/// A batch the database refuses is dropped and counted, not retried: the record of the app must never become the
/// reason it stops. What is waiting at shutdown is written once more.
/// </summary>
public sealed class MonitoringWriter(
    NpgsqlDataSource dataSource,
    MonitoringQueues queues,
    BackgroundJobs jobs,
    IOptions<MonitoringOptions> options,
    TimeProvider time,
    ILogger<MonitoringWriter> logger
) : BackgroundService
{
    private const string CopyRequests =
        $"COPY {MonitoringSchema.Requests} (at, method, path, status, elapsed_ms, agent, trace_id) FROM STDIN (FORMAT BINARY)";

    private const string CopyLogs =
        $"COPY {MonitoringSchema.Logs} (at, level, source, message, exception, request_path, trace_id) FROM STDIN (FORMAT BINARY)";

    private readonly SemaphoreSlim flushing = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.FlushInterval;
        // The real clock: a test's fake clock must not stop the writer; tests call FlushAsync themselves.
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await FlushAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown.
        }

        // What came in during the last interval; the host's shutdown timeout bounds it.
        await FlushAsync(CancellationToken.None);
    }

    /// <summary>Writes everything waiting now. Returns how many rows were written.</summary>
    public async Task<int> FlushAsync(CancellationToken ct = default)
    {
        await flushing.WaitAsync(ct);
        try
        {
            if (queues.Requests.IsEmpty && queues.Logs.IsEmpty)
                return 0;

            jobs.Writer.Started(time.GetUtcNow());
            var batch = options.Value.WriteBatchSize;
            var requests = 0;
            var logs = 0;
            string? error = null;

            for (var rows = queues.Requests.Take(batch); rows.Count > 0; rows = queues.Requests.Take(batch))
            {
                if (await TryWriteAsync(queues.Requests, rows, WriteRequestsAsync, ct) is { } failure)
                    error = failure;
                else
                    requests += rows.Count;
            }

            for (var rows = queues.Logs.Take(batch); rows.Count > 0; rows = queues.Logs.Take(batch))
            {
                if (await TryWriteAsync(queues.Logs, rows, WriteLogsAsync, ct) is { } failure)
                    error = failure;
                else
                    logs += rows.Count;
            }

            var now = time.GetUtcNow();
            if (error is null)
                jobs.Writer.Succeeded(
                    now,
                    () =>
                        T(
                            $"Zapisano: {requests} odpowiedzi HTTP, {logs} wpisów logu",
                            $"Saved: {requests} HTTP responses, {logs} log entries"
                        ),
                    null
                );
            else
                jobs.Writer.Failed(now, error, null);
            return requests + logs;
        }
        finally
        {
            flushing.Release();
        }
    }

    private async Task<string?> TryWriteAsync<T>(
        MonitoringQueue<T> queue,
        List<T> rows,
        Func<NpgsqlConnection, List<T>, CancellationToken, Task> write,
        CancellationToken ct
    )
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await write(connection, rows, ct);
            queue.Written(rows.Count, time.GetUtcNow());
            return null;
        }
        catch (Exception e) when (e is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            queue.Failed(rows.Count);
            // This class's own entries are not stored (AppLogSink), so a failing database does not feed itself.
            logger.LogWarning(e, "Monitoring: {Count} entries of {Queue} not written", rows.Count, queue.Name);
            return $"{queue.Name}: {e.Message}";
        }
    }

    private static async Task WriteRequestsAsync(
        NpgsqlConnection connection,
        List<RequestRecord> rows,
        CancellationToken ct
    )
    {
        await using var importer = await connection.BeginBinaryImportAsync(CopyRequests, ct);
        foreach (var row in rows)
        {
            await importer.StartRowAsync(ct);
            await importer.WriteAsync(row.At, NpgsqlDbType.TimestampTz, ct);
            await importer.WriteAsync(row.Method, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(row.Path, NpgsqlDbType.Text, ct);
            await importer.WriteAsync((short)row.Status, NpgsqlDbType.Smallint, ct);
            await importer.WriteAsync(row.ElapsedMs, NpgsqlDbType.Double, ct);
            await WriteTextAsync(importer, row.Agent, NpgsqlDbType.Text, ct);
            await WriteTextAsync(importer, row.TraceId, NpgsqlDbType.Varchar, ct);
        }
        await importer.CompleteAsync(ct);
    }

    private static async Task WriteLogsAsync(NpgsqlConnection connection, List<LogRecord> rows, CancellationToken ct)
    {
        await using var importer = await connection.BeginBinaryImportAsync(CopyLogs, ct);
        foreach (var row in rows)
        {
            await importer.StartRowAsync(ct);
            await importer.WriteAsync(row.At, NpgsqlDbType.TimestampTz, ct);
            await importer.WriteAsync((short)row.Level, NpgsqlDbType.Smallint, ct);
            await WriteTextAsync(importer, row.Source, NpgsqlDbType.Text, ct);
            await importer.WriteAsync(row.Message, NpgsqlDbType.Text, ct);
            await WriteTextAsync(importer, row.Exception, NpgsqlDbType.Text, ct);
            await WriteTextAsync(importer, row.RequestPath, NpgsqlDbType.Text, ct);
            await WriteTextAsync(importer, row.TraceId, NpgsqlDbType.Varchar, ct);
        }
        await importer.CompleteAsync(ct);
    }

    private static Task WriteTextAsync(
        NpgsqlBinaryImporter importer,
        string? value,
        NpgsqlDbType type,
        CancellationToken ct
    ) => value is null ? importer.WriteNullAsync(ct) : importer.WriteAsync(value, type, ct);

    public override void Dispose()
    {
        flushing.Dispose();
        base.Dispose();
    }
}
