namespace Tempo.Blazor.Abstractions.Models;

public class GanttResourceCalendar
{
    public string AssigneeId { get; set; } = string.Empty;
    public IReadOnlyList<DateRange> VacationDays { get; set; } = [];
    public IReadOnlyList<DateTime> DaysOff { get; set; } = [];
}
