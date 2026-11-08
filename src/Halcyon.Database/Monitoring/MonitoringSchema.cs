using Marten;
using Weasel.Postgresql.Tables;

namespace Halcyon.Database.Monitoring;

/// <summary>
/// Two plain tables next to the documents (ADR-0011): every HTTP answer and the application's log, kept for the retention
/// (14 days by default) and removed by the app's cleanup. Not documents and not events: they are counted, filtered by
/// time and deleted in bulk. Created by the migrator like the rest of the store (ADR-0002).
/// </summary>
public static class MonitoringSchema
{
    public const string Requests = HalcyonStore.DocumentSchema + ".http_requests";
    public const string Logs = HalcyonStore.DocumentSchema + ".app_logs";

    public static void Register(StoreOptions options)
    {
        options.Storage.ExtendedSchemaObjects.Add(RequestsTable());
        options.Storage.ExtendedSchemaObjects.Add(LogsTable());
    }

    private static Table RequestsTable()
    {
        var table = new Table(Requests);
        table.AddColumn<long>("id").AsPrimaryKey().BigAutoIncrement();
        table.AddColumn("at", "timestamptz").NotNull();
        table.AddColumn("method", "varchar(16)").NotNull();
        table.AddColumn("path", "text").NotNull();
        table.AddColumn("status", "smallint").NotNull();
        table.AddColumn("elapsed_ms", "double precision").NotNull();
        table.AddColumn("agent", "text");
        table.AddColumn("trace_id", "varchar(32)");
        // The time range of every page and the cleanup; the status for "only the errors".
        table.Indexes.Add(new IndexDefinition("ix_http_requests_at") { Columns = ["at"] });
        table.Indexes.Add(new IndexDefinition("ix_http_requests_status_at") { Columns = ["status", "at"] });
        return table;
    }

    private static Table LogsTable()
    {
        var table = new Table(Logs);
        table.AddColumn<long>("id").AsPrimaryKey().BigAutoIncrement();
        table.AddColumn("at", "timestamptz").NotNull();
        // Serilog's LogEventLevel: 0 Verbose ... 5 Fatal, so "at least Warning" is a comparison.
        table.AddColumn("level", "smallint").NotNull();
        table.AddColumn("source", "text");
        table.AddColumn("message", "text").NotNull();
        table.AddColumn("exception", "text");
        table.AddColumn("request_path", "text");
        table.AddColumn("trace_id", "varchar(32)");
        table.Indexes.Add(new IndexDefinition("ix_app_logs_at") { Columns = ["at"] });
        table.Indexes.Add(new IndexDefinition("ix_app_logs_level_at") { Columns = ["level", "at"] });
        return table;
    }
}
