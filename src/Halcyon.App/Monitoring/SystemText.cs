using System.Globalization;
using Halcyon.Domain.Time;

namespace Halcyon.App.Monitoring;

/// <summary>How the system pages write numbers, sizes, times, levels and statuses.</summary>
public static class SystemText
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static string N(long value) => value.ToString("N0", Polish);

    public static string Bytes(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:0.#} kB",
            < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.#} MB",
            _ => $"{bytes / 1024.0 / 1024 / 1024:0.##} GB",
        };

    public static string Ms(double? ms) =>
        ms switch
        {
            null => "-",
            < 1 => "<1 ms",
            < 1000 => $"{ms:0} ms",
            _ => $"{ms / 1000:0.0#} s",
        };

    /// <summary>"3 d 4 h", "2 h 5 min", "40 s".</summary>
    public static string Span(TimeSpan span) =>
        span switch
        {
            { TotalDays: >= 1 } => $"{(int)span.TotalDays} d {span.Hours} h",
            { TotalHours: >= 1 } => $"{span.Hours} h {span.Minutes} min",
            { TotalMinutes: >= 1 } => $"{span.Minutes} min {span.Seconds} s",
            _ => $"{Math.Max(0, (int)span.TotalSeconds)} s",
        };

    public static string Time(DateTimeOffset? at) =>
        at is { } value ? LocalTime.ToLocal(value).ToString("dd.MM.yyyy HH:mm:ss", Polish) : "-";

    public static string Share(long part, long total) =>
        total == 0 ? "-" : (part * 100.0 / total).ToString("0.#", Polish) + " %";

    public static string LevelName(int level) =>
        level switch
        {
            0 => "Verbose",
            1 => "Debug",
            2 => "Info",
            3 => "Warning",
            4 => "Error",
            _ => "Fatal",
        };

    public static string LevelClass(int level) =>
        level switch
        {
            >= 4 => "bg-red-100 text-red-800",
            3 => "bg-amber-100 text-amber-900",
            2 => "bg-halcyon-100 text-halcyon-800",
            _ => "bg-ink-100 text-ink-700",
        };

    public static string StatusClass(int status) =>
        status switch
        {
            >= 500 => "bg-red-700 text-white",
            403 => "bg-amber-100 text-amber-900 ring-1 ring-amber-300",
            >= 400 => "bg-amber-50 text-amber-900",
            >= 300 => "bg-ink-100 text-ink-700",
            _ => "bg-emerald-100 text-emerald-800",
        };

    /// <summary>The source's class name without the namespace: "Halcyon.App.Auth.SignInService" → "SignInService".</summary>
    public static string ShortSource(string? source) =>
        source is null ? "-"
        : source.LastIndexOf('.') is var dot and >= 0 ? source[(dot + 1)..]
        : source;
}
