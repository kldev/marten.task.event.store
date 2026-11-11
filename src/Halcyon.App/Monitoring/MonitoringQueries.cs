using Halcyon.Database.Monitoring;
using Halcyon.Domain.Time;
using Npgsql;

namespace Halcyon.App.Monitoring;

/// <summary>The period of the status page: the last 24 hours by the hour, or the last 7 or 14 days by the day.</summary>
public enum StatusRange
{
    Hours24,
    Days7,
    Days14,
}

/// <summary>Which HTTP answers a list shows.</summary>
public enum RequestOutcome
{
    All,
    Success,
    ClientError,
    Forbidden,
    NotFound,
    ServerError,
}

public sealed record StatusCount(int Status, long Count);

/// <summary>The figures of the status page for a period.</summary>
public sealed record HttpStatistics(
    DateTimeOffset From,
    long Total,
    long Ok,
    long Success,
    long Redirects,
    long Forbidden,
    long NotFound,
    long OtherClientErrors,
    long ServerErrors,
    long InternalServerErrors,
    double? AverageMs,
    double? P95Ms,
    IReadOnlyList<StatusCount> ByStatus,
    IReadOnlyList<HttpBucket> Buckets,
    IReadOnlyList<FailingPath> FailingPaths
);

/// <summary>One column of the chart: answers below 400, 4xx and 5xx.</summary>
public sealed record HttpBucket(
    DateTimeOffset From,
    string Label,
    string LongLabel,
    int Success,
    int ClientErrors,
    int ServerErrors
);

public sealed record FailingPath(string Method, string Path, int Status, long Count, DateTimeOffset LastAt);

public sealed record RequestRow(
    long Id,
    DateTimeOffset At,
    string Method,
    string Path,
    int Status,
    double ElapsedMs,
    string? Agent,
    string? TraceId
);

public sealed record LogRow(
    long Id,
    DateTimeOffset At,
    int Level,
    string? Source,
    string Message,
    string? Exception,
    string? RequestPath,
    string? TraceId
);

public sealed record RequestFilter(RequestOutcome Outcome = RequestOutcome.All, string? Path = null, int Page = 0);

/// <param name="MinLevel">Serilog's level: 2 Information, 3 Warning, 4 Error.</param>
public sealed record LogFilter(int MinLevel = 2, string? Search = null, int Page = 0);

/// <summary>A page of a list: the rows and whether there is a next one (no count over 14 days of rows).</summary>
public sealed record Slice<T>(IReadOnlyList<T> Rows, bool HasMore);

/// <summary>Reads the HTTP answers and the log the app keeps for the retention (ADR-0011).</summary>
public sealed class MonitoringQueries(NpgsqlDataSource dataSource, TimeProvider time)
{
    private const int PageSize = 50;
    private const string Zone = "Europe/Warsaw";

    public async Task<HttpStatistics> StatisticsAsync(StatusRange range, CancellationToken ct = default)
    {
        var buckets = BucketsFor(range, time.GetUtcNow());
        var from = buckets[0].From;
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var byStatus = new List<StatusCount>();
        await using (
            var command = new NpgsqlCommand(
                $"SELECT status, count(*) FROM {MonitoringSchema.Requests} WHERE at >= @from GROUP BY status ORDER BY status",
                connection
            )
        )
        {
            command.Parameters.AddWithValue("from", from);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                byStatus.Add(new StatusCount(reader.GetInt16(0), reader.GetInt64(1)));
        }

        double? average = null;
        double? p95 = null;
        await using (
            var command = new NpgsqlCommand(
                $"""
                SELECT avg(elapsed_ms), percentile_cont(0.95) WITHIN GROUP (ORDER BY elapsed_ms)
                FROM {MonitoringSchema.Requests} WHERE at >= @from
                """,
                connection
            )
        )
        {
            command.Parameters.AddWithValue("from", from);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                average = reader.IsDBNull(0) ? null : reader.GetDouble(0);
                p95 = reader.IsDBNull(1) ? null : reader.GetDouble(1);
            }
        }

        var unit = range == StatusRange.Hours24 ? "hour" : "day";
        var counted = new Dictionary<DateTimeOffset, (int Success, int Client, int Server)>();
        await using (
            var command = new NpgsqlCommand(
                $"""
                SELECT date_trunc('{unit}', at, '{Zone}') AS bucket,
                       count(*) FILTER (WHERE status < 400),
                       count(*) FILTER (WHERE status >= 400 AND status < 500),
                       count(*) FILTER (WHERE status >= 500)
                FROM {MonitoringSchema.Requests}
                WHERE at >= @from
                GROUP BY 1
                """,
                connection
            )
        )
        {
            command.Parameters.AddWithValue("from", from);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                counted[reader.GetFieldValue<DateTimeOffset>(0)] = (
                    (int)reader.GetInt64(1),
                    (int)reader.GetInt64(2),
                    (int)reader.GetInt64(3)
                );
        }

        var failing = new List<FailingPath>();
        await using (
            var command = new NpgsqlCommand(
                $"""
                SELECT method, path, status, count(*), max(at)
                FROM {MonitoringSchema.Requests}
                WHERE at >= @from AND status >= 400
                GROUP BY method, path, status
                ORDER BY count(*) DESC, max(at) DESC
                LIMIT 10
                """,
                connection
            )
        )
        {
            command.Parameters.AddWithValue("from", from);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                failing.Add(
                    new FailingPath(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetInt16(2),
                        reader.GetInt64(3),
                        reader.GetFieldValue<DateTimeOffset>(4)
                    )
                );
        }

        long Sum(Func<int, bool> match) => byStatus.Where(s => match(s.Status)).Sum(s => s.Count);

        return new HttpStatistics(
            from,
            Sum(_ => true),
            Sum(s => s == 200),
            Sum(s => s < 300),
            Sum(s => s is >= 300 and < 400),
            Sum(s => s == 403),
            Sum(s => s == 404),
            Sum(s => s is >= 400 and < 500 and not 403 and not 404),
            Sum(s => s >= 500),
            Sum(s => s == 500),
            average,
            p95,
            byStatus,
            buckets
                .Select(b =>
                    counted.TryGetValue(b.From, out var c)
                        ? b with
                        {
                            Success = c.Success,
                            ClientErrors = c.Client,
                            ServerErrors = c.Server,
                        }
                        : b
                )
                .ToList(),
            failing
        );
    }

    public async Task<Slice<RequestRow>> RequestsAsync(RequestFilter filter, CancellationToken ct = default)
    {
        var where = filter.Outcome switch
        {
            RequestOutcome.Success => "status < 400",
            RequestOutcome.ClientError => "status >= 400 AND status < 500",
            RequestOutcome.Forbidden => "status = 403",
            RequestOutcome.NotFound => "status = 404",
            RequestOutcome.ServerError => "status >= 500",
            _ => "true",
        };
        var search = string.IsNullOrWhiteSpace(filter.Path) ? null : filter.Path.Trim();
        if (search is not null)
            where += " AND path ILIKE @path";

        await using var command = dataSource.CreateCommand(
            $"""
            SELECT id, at, method, path, status, elapsed_ms, agent, trace_id
            FROM {MonitoringSchema.Requests}
            WHERE {where}
            ORDER BY id DESC
            LIMIT @limit OFFSET @offset
            """
        );
        if (search is not null)
            command.Parameters.AddWithValue("path", $"%{EscapeLike(search)}%");
        command.Parameters.AddWithValue("limit", PageSize + 1);
        command.Parameters.AddWithValue("offset", Math.Max(0, filter.Page) * PageSize);

        var rows = new List<RequestRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(
                new RequestRow(
                    reader.GetInt64(0),
                    reader.GetFieldValue<DateTimeOffset>(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt16(4),
                    reader.GetDouble(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7)
                )
            );
        return ToSlice(rows);
    }

    public async Task<Slice<LogRow>> LogsAsync(LogFilter filter, CancellationToken ct = default)
    {
        var where = "level >= @level";
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        if (search is not null)
            where += " AND (message ILIKE @search OR source ILIKE @search OR exception ILIKE @search)";

        await using var command = dataSource.CreateCommand(
            $"""
            SELECT id, at, level, source, message, exception, request_path, trace_id
            FROM {MonitoringSchema.Logs}
            WHERE {where}
            ORDER BY id DESC
            LIMIT @limit OFFSET @offset
            """
        );
        command.Parameters.AddWithValue("level", (short)filter.MinLevel);
        if (search is not null)
            command.Parameters.AddWithValue("search", $"%{EscapeLike(search)}%");
        command.Parameters.AddWithValue("limit", PageSize + 1);
        command.Parameters.AddWithValue("offset", Math.Max(0, filter.Page) * PageSize);

        var rows = new List<LogRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(
                new LogRow(
                    reader.GetInt64(0),
                    reader.GetFieldValue<DateTimeOffset>(1),
                    reader.GetInt16(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7)
                )
            );
        return ToSlice(rows);
    }

    /// <summary>Errors and warnings of the log in the period - the status page's tile.</summary>
    public async Task<(long Errors, long Warnings)> LogCountsAsync(DateTimeOffset from, CancellationToken ct = default)
    {
        await using var command = dataSource.CreateCommand(
            $"""
            SELECT count(*) FILTER (WHERE level >= 4), count(*) FILTER (WHERE level = 3)
            FROM {MonitoringSchema.Logs} WHERE at >= @from
            """
        );
        command.Parameters.AddWithValue("from", from);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    /// <summary>
    /// The columns of a range, oldest first, the last holding now: whole Warsaw hours or calendar days (a day that
    /// changes the clock is 23 or 25 hours long).
    /// </summary>
    private static List<HttpBucket> BucketsFor(StatusRange range, DateTimeOffset now)
    {
        var local = LocalTime.ToLocal(now);
        if (range == StatusRange.Hours24)
        {
            var hour = new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified);
            return Enumerable
                .Range(0, 24)
                .Select(i => hour.AddHours(i - 23))
                .Select(h =>
                {
                    var from = LocalTime.FromLocal(h).ToUniversalTime();
                    return new HttpBucket(
                        from,
                        h.ToString("HH:00"),
                        $"{h:dd.MM.yyyy HH:00}–{h.AddHours(1):HH:00}",
                        0,
                        0,
                        0
                    );
                })
                .DistinctBy(b => b.From)
                .ToList();
        }

        var days = range == StatusRange.Days7 ? 7 : 14;
        var today = DateOnly.FromDateTime(local.DateTime);
        return Enumerable
            .Range(0, days)
            .Select(i => today.AddDays(i - days + 1))
            .Select(d => new HttpBucket(
                LocalTime.FromLocal(d.ToDateTime(TimeOnly.MinValue)).ToUniversalTime(),
                d.ToString("dd.MM"),
                d.ToString("ddd dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("pl-PL")),
                0,
                0,
                0
            ))
            .ToList();
    }

    private static Slice<T> ToSlice<T>(List<T> rows) =>
        rows.Count > PageSize ? new Slice<T>(rows[..PageSize], true) : new Slice<T>(rows, false);

    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
