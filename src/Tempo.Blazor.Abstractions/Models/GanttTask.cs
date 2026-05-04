namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Represents a single task in a Gantt chart.
/// </summary>
public class GanttTask
{
    /// <summary>Unique identifier of the task.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display title of the task.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Start date and time of the task.</summary>
    public DateTime Start { get; set; }

    /// <summary>End date and time of the task.</summary>
    public DateTime End { get; set; }

    /// <summary>Completion percentage (0–100).</summary>
    public int PercentComplete { get; set; }

    /// <summary>Parent task identifier. Null for root-level tasks.</summary>
    public string? ParentId { get; set; }

    /// <summary>When true, the task is rendered as a milestone (diamond).</summary>
    public bool IsMilestone { get; set; }

    /// <summary>Computed duration of the task.</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>Whether this task has a parent.</summary>
    public bool HasParent => !string.IsNullOrEmpty(ParentId);

    /// <summary>Whether this task is expanded (relevant for parent tasks).</summary>
    public bool IsExpanded { get; set; } = true;
}
