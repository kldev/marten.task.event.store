using Halcyon.Database;
using Npgsql;

namespace Halcyon.App.Monitoring;

public sealed record DatabaseServer(
    string Version,
    DateTimeOffset StartedAt,
    long DatabaseSizeBytes,
    int MaxConnections,
    int ServerConnections,
    bool SeesAllSessions
);

/// <summary>A client session of Halcyon's database now (pg_stat_activity).</summary>
public sealed record DatabaseSession(
    int Pid,
    string? Role,
    string? Application,
    string? ClientAddress,
    string? State,
    string? WaitEventType,
    string? WaitEvent,
    IReadOnlyList<int> BlockedBy,
    bool Blocking,
    long? TransactionMs,
    long? QueryMs,
    string? Query,
    bool Notable
);

public sealed record TableSize(string Schema, string Name, long TotalBytes, long Rows);

public sealed record EventStoreState(long LastSequence, DateTimeOffset? LastEventAt, long Streams);

/// <summary>A projection's or the daemon's progress through the event store (events.mt_event_progression).</summary>
public sealed record EventProgress(string Name, long LastSequence, DateTimeOffset? UpdatedAt);

public sealed record DatabaseSnapshot(
    DatabaseServer Server,
    IReadOnlyList<DatabaseSession> Sessions,
    IReadOnlyList<TableSize> Tables,
    EventStoreState Events,
    DateTimeOffset ReadAt
);

/// <summary>
/// PostgreSQL's view of Halcyon's database, as on Halcyon's database page in the Ambit platform (ADR-0011): the server,
/// the client sessions of this database (not the reader's own) with whom they wait for, the tables and the event store.
/// Read with the app's own pool and role: without pg_read_all_stats the queries of other roles are hidden by PostgreSQL
/// ("insufficient privilege") and the page says so.
/// </summary>
public sealed class DatabaseActivity(NpgsqlDataSource dataSource, TimeProvider time)
{
    /// <summary>A transaction or query running this long is marked on the page.</summary>
    public static readonly TimeSpan LongRunning = TimeSpan.FromSeconds(30);

    private const string ServerSql = """
        SELECT version(),
               pg_postmaster_start_time(),
               pg_database_size(current_database()),
               current_setting('max_connections')::int,
               (SELECT count(*)::int FROM pg_stat_activity WHERE backend_type = 'client backend'),
               pg_has_role('pg_read_all_stats', 'member')
        """;

    // The query is cut to 500 characters in PostgreSQL: the whole text (parameters, personal data) never reaches the page.
    private const string SessionsSql = """
        SELECT a.pid,
               a.usename,
               nullif(a.application_name, ''),
               a.client_addr::text,
               a.state,
               a.wait_event_type,
               a.wait_event,
               pg_blocking_pids(a.pid),
               (extract(epoch FROM now() - a.xact_start) * 1000)::bigint,
               CASE WHEN a.state = 'active' THEN (extract(epoch FROM now() - a.query_start) * 1000)::bigint END,
               left(a.query, 500)
        FROM pg_stat_activity a
        WHERE a.datname = current_database()
          AND a.backend_type = 'client backend'
          AND a.pid <> pg_backend_pid()
        ORDER BY a.xact_start NULLS LAST, a.pid
        """;

    private const string TablesSql = $"""
        SELECT n.nspname, c.relname, pg_total_relation_size(c.oid),
               greatest(coalesce(s.n_live_tup, c.reltuples::bigint), 0)
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        LEFT JOIN pg_stat_user_tables s ON s.relid = c.oid
        WHERE c.relkind IN ('r', 'p') AND n.nspname IN ('{HalcyonStore.DocumentSchema}', '{HalcyonStore.EventSchema}')
        ORDER BY 3 DESC, 1, 2
        """;

    private const string EventsSql = $"""
        SELECT coalesce((SELECT max(seq_id) FROM {HalcyonStore.EventSchema}.mt_events), 0),
               (SELECT max(timestamp) FROM {HalcyonStore.EventSchema}.mt_events),
               greatest((SELECT reltuples::bigint FROM pg_class
                         WHERE oid = to_regclass('{HalcyonStore.EventSchema}.mt_streams')), 0)
        """;

    private const string ProgressSql = $"""
        SELECT name, last_seq_id, last_updated FROM {HalcyonStore.EventSchema}.mt_event_progression ORDER BY name
        """;

    public async Task<DatabaseSnapshot> ReadAsync(CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        DatabaseServer server;
        await using (var command = new NpgsqlCommand(ServerSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            server = new DatabaseServer(
                reader.GetString(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetInt64(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetBoolean(5)
            );
        }

        var rows = new List<DatabaseSession>();
        await using (var command = new NpgsqlCommand(SessionsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                rows.Add(
                    new DatabaseSession(
                        reader.GetInt32(0),
                        reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4),
                        reader.IsDBNull(5) ? null : reader.GetString(5),
                        reader.IsDBNull(6) ? null : reader.GetString(6),
                        reader.GetFieldValue<int[]>(7),
                        false,
                        reader.IsDBNull(8) ? null : reader.GetInt64(8),
                        reader.IsDBNull(9) ? null : reader.GetInt64(9),
                        reader.IsDBNull(10) ? null : reader.GetString(10),
                        false
                    )
                );
        }

        var blocking = rows.SelectMany(r => r.BlockedBy).ToHashSet();
        var longMs = (long)LongRunning.TotalMilliseconds;
        var sessions = rows.Select(r =>
            {
                var blocks = blocking.Contains(r.Pid);
                return r with
                {
                    Blocking = blocks,
                    Notable = r.WaitEventType == "Lock" || blocks || r.TransactionMs >= longMs || r.QueryMs >= longMs,
                };
            })
            .ToList();

        var tables = new List<TableSize>();
        await using (var command = new NpgsqlCommand(TablesSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                tables.Add(
                    new TableSize(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3))
                );
        }

        EventStoreState events;
        await using (var command = new NpgsqlCommand(EventsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            events = new EventStoreState(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetInt64(2)
            );
        }

        return new DatabaseSnapshot(server, sessions, tables, events, time.GetUtcNow());
    }

    /// <summary>
    /// Marten's progress table. The projections are inline (ADR-0002), so it usually holds only the high-water mark -
    /// or nothing; an empty list is not an error.
    /// </summary>
    public async Task<IReadOnlyList<EventProgress>> EventProgressAsync(CancellationToken ct = default)
    {
        var rows = new List<EventProgress>();
        try
        {
            await using var command = dataSource.CreateCommand(ProgressSql);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                rows.Add(
                    new EventProgress(
                        reader.GetString(0),
                        reader.GetInt64(1),
                        reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2)
                    )
                );
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // No progress table in this schema version.
        }
        return rows;
    }
}
