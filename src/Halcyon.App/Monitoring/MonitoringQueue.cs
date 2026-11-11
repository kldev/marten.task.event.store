using System.Threading.Channels;
using static Halcyon.App.Localization.Tr;

namespace Halcyon.App.Monitoring;

/// <summary>One HTTP answer, as the request recorder saw it.</summary>
public sealed record RequestRecord(
    DateTimeOffset At,
    string Method,
    string Path,
    int Status,
    double ElapsedMs,
    string? Agent,
    string? TraceId
);

/// <summary>One entry of the application's log.</summary>
public sealed record LogRecord(
    DateTimeOffset At,
    int Level,
    string? Source,
    string Message,
    string? Exception,
    string? RequestPath,
    string? TraceId
);

/// <summary>What the queues page shows about a queue.</summary>
public sealed record QueueState(
    string Name,
    string Description,
    int Pending,
    int Capacity,
    long Enqueued,
    long Written,
    long Dropped,
    long Failed,
    DateTimeOffset? LastWriteAt
);

/// <summary>
/// A bounded queue in memory between the request (or the logger) and the database (ADR-0011): the request never waits
/// for a write, and a database that does not answer costs at most <c>capacity</c> entries, never the app's memory.
/// When full, new entries are dropped and counted.
/// </summary>
public sealed class MonitoringQueue<T>
{
    private readonly Channel<T> channel;
    private long enqueued;
    private long written;
    private long dropped;
    private long failed;
    private long lastWriteTicks;

    public MonitoringQueue(string name, Func<string> description, int capacity)
    {
        Name = name;
        Description = description;
        Capacity = capacity;
        channel = Channel.CreateBounded<T>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            },
            _ => Interlocked.Increment(ref dropped)
        );
    }

    public string Name { get; }

    /// <summary>In the reader's language (ADR-0012): asked for when the page renders.</summary>
    private Func<string> Description { get; }
    private int Capacity { get; }

    public void Enqueue(T item)
    {
        if (channel.Writer.TryWrite(item))
            Interlocked.Increment(ref enqueued);
    }

    /// <summary>Takes up to <paramref name="max"/> waiting entries.</summary>
    public List<T> Take(int max)
    {
        var batch = new List<T>(Math.Min(max, channel.Reader.Count));
        while (batch.Count < max && channel.Reader.TryRead(out var item))
            batch.Add(item);
        return batch;
    }

    public bool IsEmpty => channel.Reader.Count == 0;

    public void Written(int count, DateTimeOffset at)
    {
        Interlocked.Add(ref written, count);
        Interlocked.Exchange(ref lastWriteTicks, at.UtcTicks);
    }

    public void Failed(int count) => Interlocked.Add(ref failed, count);

    public QueueState State()
    {
        var ticks = Interlocked.Read(ref lastWriteTicks);
        return new QueueState(
            Name,
            Description(),
            channel.Reader.Count,
            Capacity,
            Interlocked.Read(ref enqueued),
            Interlocked.Read(ref written),
            Interlocked.Read(ref dropped),
            Interlocked.Read(ref failed),
            ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero)
        );
    }
}

/// <summary>The two queues of the process, one instance each.</summary>
public sealed class MonitoringQueues(Microsoft.Extensions.Options.IOptions<MonitoringOptions> options)
{
    public MonitoringQueue<RequestRecord> Requests { get; } =
        new(
            "http-requests",
            () => T("Odpowiedzi HTTP → tabela halcyon.http_requests", "HTTP responses → table halcyon.http_requests"),
            options.Value.QueueCapacity
        );

    public MonitoringQueue<LogRecord> Logs { get; } =
        new(
            "app-logs",
            () => T("Log aplikacji → tabela halcyon.app_logs", "Application log → table halcyon.app_logs"),
            options.Value.QueueCapacity
        );

    public IReadOnlyList<QueueState> States() => [Requests.State(), Logs.State()];
}
