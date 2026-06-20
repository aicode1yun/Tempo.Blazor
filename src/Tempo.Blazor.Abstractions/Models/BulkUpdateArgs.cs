namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Arguments for a bulk update operation on selected Gantt tasks.</summary>
public record BulkUpdateArgs
{
    public IReadOnlyList<string> TaskIds { get; init; } = Array.Empty<string>();
    public GanttTaskStatus? Status { get; init; }
    public GanttTaskPriority? Priority { get; init; }
    public string? Color { get; init; }
    public IReadOnlyList<string> AssigneeIdsToAdd { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AssigneeIdsToRemove { get; init; } = Array.Empty<string>();
}
