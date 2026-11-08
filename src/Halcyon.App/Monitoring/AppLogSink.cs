using Microsoft.Extensions.Options;
using Serilog.Core;
using Serilog.Events;

namespace Halcyon.App.Monitoring;

/// <summary>
/// The Serilog sink that puts the application's log into the database queue (ADR-0011), from
/// <see cref="MonitoringOptions.LogLevel"/> up. Two sources stay out: the one-line-per-request log (the HTTP answers
/// have their own table) and the monitoring itself (a failing database would otherwise feed its own queue).
/// </summary>
public sealed class AppLogSink(MonitoringQueues queues, IOptions<MonitoringOptions> options) : ILogEventSink
{
    private const int MaxMessageLength = 4_000;
    private const int MaxExceptionLength = 16_000;

    private static readonly string[] SkippedSources =
    [
        "Serilog.AspNetCore.RequestLoggingMiddleware",
        typeof(MonitoringWriter).FullName!,
    ];

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < options.Value.LogLevel)
            return;

        var source = Scalar(logEvent, "SourceContext");
        if (source is not null && SkippedSources.Contains(source, StringComparer.Ordinal))
            return;

        queues.Logs.Enqueue(
            new LogRecord(
                logEvent.Timestamp.ToUniversalTime(),
                (int)logEvent.Level,
                source,
                Cut(logEvent.RenderMessage(), MaxMessageLength)!,
                Cut(logEvent.Exception?.ToString(), MaxExceptionLength),
                Scalar(logEvent, "RequestPath"),
                logEvent.TraceId?.ToHexString()
            )
        );
    }

    private static string? Scalar(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: { } scalar }
            ? scalar.ToString()
            : null;

    private static string? Cut(string? text, int max) =>
        text is null || text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "…");
}
