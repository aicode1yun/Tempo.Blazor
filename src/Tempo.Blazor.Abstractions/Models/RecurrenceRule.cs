namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Represents a recurrence rule that can be serialized to/from iCal RRULE format.
/// </summary>
public class RecurrenceRule
{
    /// <summary>The recurrence pattern type.</summary>
    public RecurrencePattern Pattern { get; set; } = RecurrencePattern.Daily;

    /// <summary>Interval (e.g. every 2 days/weeks/months/years). Default is 1.</summary>
    public int Interval { get; set; } = 1;

    /// <summary>Days of the week selected for Weekly pattern (0=Sunday, 6=Saturday).</summary>
    public IReadOnlyList<int> DaysOfWeek { get; set; } = [];

    /// <summary>Day of the month for Monthly/Yearly patterns (1-31).</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>Month of the year for Yearly pattern (1-12).</summary>
    public int? MonthOfYear { get; set; }

    /// <summary>Position of the day in the month for Monthly (e.g. first Monday).</summary>
    public int? Position { get; set; }

    /// <summary>How the recurrence ends: null = never, int = after N occurrences, DateTime = until date.</summary>
    public object? EndAfter { get; set; }

    /// <summary>The start date of the recurrence.</summary>
    public DateTime StartDate { get; set; } = DateTime.Today;
}
