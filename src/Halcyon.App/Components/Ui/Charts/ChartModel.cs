using System.Globalization;

namespace Halcyon.App.Components.Ui.Charts;

/// <summary>A line or a stacked part of a column chart: one value per bucket.</summary>
public sealed record ChartSeries(string Name, string Color, IReadOnlyList<int> Values);

/// <summary>A part of a horizontal stacked bar.</summary>
public sealed record ChartSegment(string Name, string Color);

/// <summary>A legend entry; <paramref name="Line"/> draws a short line instead of a square.</summary>
public sealed record LegendItem(string Name, string Color, bool Line = false);

/// <summary>
/// The colors of the charts (spec 009). Series: the brand teal and the rufous accent, checked for colour-blind
/// separation. Status: the meaning the lists already give (on time, due soon, overdue), never used for a series, and
/// always with a word in the legend.
/// </summary>
public static class ChartColors
{
    public const string Reported = "#0b98b4"; // halcyon-500
    public const string Resolved = "#e2662a"; // rufous-500
    public const string Backlog = "#10627a"; // halcyon-700, a single series
    public const string OnTime = "#059669"; // emerald-600
    public const string DueSoon = "#f59e0b"; // amber-500
    public const string Late = "#b91c1c"; // red-700
    public const string Open = "#a3b8be"; // ink-300: not decided yet
    public const string Median = "#10627a"; // halcyon-700
    public const string P90 = "#a0e6f1"; // halcyon-200
    public const string Grid = "#e6edef"; // ink-100
    public const string Axis = "#cbd8dc"; // ink-200
    public const string Muted = "#6f8d95"; // ink-400
    public const string Ink = "#132226"; // ink-900
    public const string Surface = "#ffffff";
}

public static class ChartScale
{
    /// <summary>The reader's number format (ADR-0012): "1 234" in Polish, "1,234" in English.</summary>
    private static CultureInfo Reader => Localization.Language.Culture;
    private static readonly int[] Multiples = [1, 2, 5, 10];

    /// <summary>A round top for the value axis with four even steps (1, 2 or 5 times a power of ten).</summary>
    public static (int Max, int Step) Nice(int max)
    {
        if (max <= 4)
            return (4, 1);
        var raw = max / 4.0;
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = (int)(Multiples.Select(m => m * power).First(s => s >= raw));
        return ((int)Math.Ceiling(max / (double)step) * step, step);
    }

    /// <summary>Which labels to show under an axis of <paramref name="count"/> buckets: at most 8, always the last.</summary>
    public static bool ShowLabel(int index, int count) => (count - 1 - index) % (int)Math.Ceiling(count / 8.0) == 0;

    public static string N(int value) => value.ToString("N0", Reader);

    public static string N(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    public static string Percent(double share) => (share * 100).ToString("0", Reader) + " %";

    /// <summary>"1 zgłoszenie", "3 zgłoszenia", "5 zgłoszeń".</summary>
    public static string Tickets(int n) =>
        Localization.Language.IsEnglish
            ? $"{N(n)} {(n == 1 ? "ticket" : "tickets")}"
            : $"{N(n)} {Plural(n, "zgłoszenie", "zgłoszenia", "zgłoszeń")}";

    public static string Plural(int n, string one, string few, string many)
    {
        if (n == 1)
            return one;
        var tens = n % 100;
        var units = n % 10;
        return units is >= 2 and <= 4 && tens is < 12 or > 14 ? few : many;
    }

    /// <summary>The SVG path of a column rising from <paramref name="bottom"/> with the top corners rounded.</summary>
    public static string RoundedTop(double x, double top, double width, double bottom, double radius = 4)
    {
        var r = Math.Min(radius, Math.Min(width / 2, bottom - top));
        return FormattableString.Invariant(
            $"M{x:0.##},{bottom:0.##}V{top + r:0.##}Q{x:0.##},{top:0.##} {x + r:0.##},{top:0.##}H{x + width - r:0.##}Q{x + width:0.##},{top:0.##} {x + width:0.##},{top + r:0.##}V{bottom:0.##}Z"
        );
    }

    /// <summary>A number for an SVG attribute: invariant, two decimals at most.</summary>
    public static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>About how wide a text of 12 px Inter is, for sizing a tooltip in SVG.</summary>
    public static double TextWidth(string text) => text.Length * 6.6;
}
