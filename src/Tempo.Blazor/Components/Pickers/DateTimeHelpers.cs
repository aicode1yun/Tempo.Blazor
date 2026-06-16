using System.Globalization;

namespace Tempo.Blazor.Components.Pickers;

/// <summary>
/// Internal helpers for calendar rendering and date arithmetic.
/// </summary>
internal static class DateTimeHelpers
{
    /// <summary>
    /// Returns <see cref="DateOnly"/> values that fill the calendar grid for the given year/month,
    /// starting on Monday. The grid always contains six weeks so the calendar
    /// keeps a stable shape while navigating between months.
    /// Leading and trailing cells contain days from adjacent months.
    /// </summary>
    public static IReadOnlyList<DateOnly> GetCalendarDays(int year, int month)
    {
        var first  = new DateOnly(year, month, 1);
        // Mon=0 offset: (DayOfWeek + 6) % 7
        var startOffset = ((int)first.DayOfWeek + 6) % 7;
        var start = first.AddDays(-startOffset);
        const int count = 42;
        var days = new DateOnly[count];
        for (var i = 0; i < count; i++)
            days[i] = start.AddDays(i);

        return days;
    }

    /// <summary>Returns the full month name for the given month and culture.</summary>
    public static string GetMonthName(int month, CultureInfo culture)
        => culture.DateTimeFormat.GetMonthName(month);

    /// <summary>
    /// Returns 7 abbreviated day-header strings starting on Monday,
    /// honouring the culture's <see cref="DateTimeFormatInfo.FirstDayOfWeek"/>.
    /// </summary>
    public static IReadOnlyList<string> GetDayHeaders(CultureInfo culture)
    {
        var abbr = culture.DateTimeFormat.AbbreviatedDayNames; // Sun=0 … Sat=6
        // Reorder: start from Monday
        var result = new string[7];
        for (var i = 0; i < 7; i++)
        {
            // Mon=1 in DayOfWeek; map slot i → DayOfWeek (Mon=1,…,Sun=0)
            var dow = (i + 1) % 7; // 0=Mon→1, 1=Tue→2, … 6=Sun→0
            result[i] = abbr[dow];
        }
        return result;
    }

    /// <summary>Returns <see langword="true"/> if <paramref name="date"/> is today.</summary>
    public static bool IsToday(DateOnly date)
        => date == DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Returns <see langword="true"/> if <paramref name="date"/> falls within an inclusive range.</summary>
    public static bool IsInRange(DateOnly date, DateOnly? start, DateOnly? end)
        => start.HasValue && end.HasValue && date >= start.Value && date <= end.Value;
}
