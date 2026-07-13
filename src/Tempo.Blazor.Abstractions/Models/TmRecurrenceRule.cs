namespace Tempo.Blazor.Models;

/// <summary>
/// Frequency of recurrence for scheduled events.
/// </summary>
public enum TmRecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}

/// <summary>
/// Represents a recurrence rule for scheduled events (subset of RFC 5545 RRULE).
/// </summary>
public class TmRecurrenceRule
{
    /// <summary>Frequency of recurrence.</summary>
    public TmRecurrenceFrequency Frequency { get; set; }

    /// <summary>Interval between occurrences (e.g., every 2 weeks).</summary>
    public int Interval { get; set; } = 1;

    /// <summary>Maximum number of occurrences. Null = unlimited.</summary>
    public int? Count { get; set; }

    /// <summary>End date for recurrence. Null = unlimited (or limited by Count).</summary>
    public DateTime? Until { get; set; }

    /// <summary>Days of the week for WEEKLY frequency (non-positional BYDAY entries).</summary>
    public DayOfWeek[]? ByDay { get; set; }

    /// <summary>
    /// The day the workweek starts (RFC 5545 <c>WKST</c>). For <c>WEEKLY</c> rules with
    /// <see cref="Interval"/> &gt; 1 and multiple <see cref="ByDay"/> entries, this determines which
    /// week an occurrence falls into and therefore the resulting dates. Defaults to
    /// <see cref="DayOfWeek.Monday"/>, matching the RFC 5545 default (behavior is unchanged when
    /// <c>WKST</c> is absent).
    /// </summary>
    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Monday;

    /// <summary>
    /// Positional BYDAY entries for MONTHLY/YEARLY frequency, e.g. <c>3TH</c> (3rd Thursday) or
    /// <c>-1FR</c> (last Friday). Ordinal is 1-based from the start, or negative from the end.
    /// </summary>
    public (int Ordinal, DayOfWeek Day)[]? ByDayPositional { get; set; }

    /// <summary>Days of the month for MONTHLY frequency (1–31).</summary>
    public int[]? ByMonthDay { get; set; }

    /// <summary>Months for YEARLY frequency (1–12).</summary>
    public int[]? ByMonth { get; set; }

    /// <summary>
    /// BYSETPOS selectors applied to the candidate set of each period (1-based, or negative from the
    /// end), for example <c>-1</c> with <c>BYDAY=MO,TU,WE,TH,FR</c> = the last weekday of the month.
    /// </summary>
    public int[]? BySetPos { get; set; }
}
