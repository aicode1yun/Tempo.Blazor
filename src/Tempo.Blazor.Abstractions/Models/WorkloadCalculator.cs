namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Calculates per-assignee per-day workload from a list of tasks.</summary>
public static class WorkloadCalculator
{
    /// <summary>
    /// Calculates workload entries for each assignee × working day combination covered by the given tasks.
    /// AllocatedHours = sum of each task's daily share (task duration / working days in task span).
    /// CapacityHours = WorkDayEndHour − WorkDayStartHour.
    /// </summary>
    public static IReadOnlyList<WorkloadEntry> Calculate(
        IEnumerable<GanttTask> tasks,
        WorkingSchedule schedule)
    {
        var capacityPerDay = schedule.WorkDayEndHour - schedule.WorkDayStartHour;
        var holidaySet = new HashSet<DateTime>(schedule.Holidays.Select(h => h.Date));

        bool IsWorkingDay(DateTime d) =>
            !schedule.NonWorkingDaysOfWeek.Contains(d.DayOfWeek) &&
            !holidaySet.Contains(d.Date);

        // group allocations by (assigneeId, date)
        var alloc = new Dictionary<(string AssigneeId, DateTime Date), double>();

        foreach (var task in tasks)
        {
            foreach (var assignee in task.Assignees)
            {
                var workingDays = EnumerateDays(task.Start.Date, task.End.Date)
                    .Where(IsWorkingDay)
                    .ToList();

                if (workingDays.Count == 0)
                    continue;

                var hoursPerDay = (double)capacityPerDay / workingDays.Count;

                foreach (var day in workingDays)
                {
                    var key = (assignee.Id, day);
                    alloc[key] = alloc.TryGetValue(key, out var existing)
                        ? existing + hoursPerDay
                        : hoursPerDay;
                }
            }
        }

        return alloc
            .Select(kv => new WorkloadEntry(kv.Key.AssigneeId, kv.Key.Date, kv.Value, capacityPerDay))
            .OrderBy(e => e.AssigneeId)
            .ThenBy(e => e.Date)
            .ToList();
    }

    private static IEnumerable<DateTime> EnumerateDays(DateTime start, DateTime end)
    {
        for (var d = start; d <= end; d = d.AddDays(1))
            yield return d;
    }
}
