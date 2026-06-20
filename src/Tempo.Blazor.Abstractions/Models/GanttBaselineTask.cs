namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Immutable snapshot of a single task's schedule at baseline creation time.
/// </summary>
public record GanttBaselineTask(string TaskId, DateTime Start, DateTime End);
