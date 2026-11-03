namespace Halcyon.Domain.Calendar;

public sealed record CalendarDay(DateOnly Date, bool InMonth, bool Disabled);

/// <summary>
/// The grid of the date picker (spec 006): six weeks from Monday, so the layout never jumps between months, with days
/// outside [min, max] disabled. Kept here so the math is tested without a browser.
/// </summary>
public sealed class CalendarMonth
{
    private CalendarMonth(int year, int month, IReadOnlyList<CalendarDay> days)
    {
        Year = year;
        Month = month;
        Days = days;
    }

    public int Year { get; }
    public int Month { get; }
    public IReadOnlyList<CalendarDay> Days { get; }

    public IEnumerable<IReadOnlyList<CalendarDay>> Weeks => Days.Chunk(7);

    public static string[] WeekdayShortNames { get; } = ["Pn", "Wt", "Śr", "Cz", "Pt", "So", "Nd"];

    public static string[] MonthNames { get; } =
    [
        "Styczeń",
        "Luty",
        "Marzec",
        "Kwiecień",
        "Maj",
        "Czerwiec",
        "Lipiec",
        "Sierpień",
        "Wrzesień",
        "Październik",
        "Listopad",
        "Grudzień",
    ];

    public string Title => $"{MonthNames[Month - 1]} {Year}";

    public static CalendarMonth Build(int year, int month, DateOnly? min = null, DateOnly? max = null)
    {
        var first = new DateOnly(year, month, 1);
        // Monday = 0 ... Sunday = 6.
        var offset = ((int)first.DayOfWeek + 6) % 7;
        var start = first.AddDays(-offset);

        var days = new List<CalendarDay>(42);
        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            var disabled = (min is { } lo && date < lo) || (max is { } hi && date > hi);
            days.Add(new CalendarDay(date, date.Month == month && date.Year == year, disabled));
        }
        return new CalendarMonth(year, month, days);
    }

    public (int Year, int Month) Previous => Month == 1 ? (Year - 1, 12) : (Year, Month - 1);

    public (int Year, int Month) Next => Month == 12 ? (Year + 1, 1) : (Year, Month + 1);
}
