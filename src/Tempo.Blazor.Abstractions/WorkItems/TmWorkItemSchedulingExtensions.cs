namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// Date-coercion helpers for components that require a scheduled item (e.g. the Gantt chart),
/// where <see cref="TmWorkItem.Start"/>/<see cref="TmWorkItem.End"/> are conceptually required
/// even though the unified model keeps them nullable for unscheduled sources (Notion tasks).
/// </summary>
public static class TmWorkItemSchedulingExtensions
{
    /// <summary>Scheduled start (passthrough for <see cref="TmWorkItem.Start"/>).</summary>
    public static DateTime ScheduledStart(this TmWorkItem item) => item.Start;

    /// <summary>Scheduled end (passthrough for <see cref="TmWorkItem.End"/>).</summary>
    public static DateTime ScheduledEnd(this TmWorkItem item) => item.End;

    /// <summary>Duration between scheduled start and end.</summary>
    public static TimeSpan ScheduledDuration(this TmWorkItem item) => item.End - item.Start;
}
