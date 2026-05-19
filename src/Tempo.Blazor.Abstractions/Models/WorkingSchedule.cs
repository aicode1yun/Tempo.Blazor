namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Defines the working calendar for a Gantt chart:
/// which days are non-working, public holidays, and working hours per day.
/// </summary>
public class WorkingSchedule
{
    /// <summary>Days of the week treated as non-working. Default: Saturday + Sunday.</summary>
    public DayOfWeek[] NonWorkingDaysOfWeek { get; set; } = [DayOfWeek.Saturday, DayOfWeek.Sunday];

    /// <summary>Specific dates treated as public holidays (non-working).</summary>
    public DateTime[] Holidays { get; set; } = [];

    /// <summary>Hour at which the working day starts (0–23). Default: 8.</summary>
    public int WorkDayStartHour { get; set; } = 8;

    /// <summary>Hour at which the working day ends (0–23). Default: 17.</summary>
    public int WorkDayEndHour { get; set; } = 17;
}
