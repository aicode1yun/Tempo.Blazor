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

    /// <summary>Bar color as a CSS color string (e.g. "#3b82f6"). Null uses the default primary color.</summary>
    public string? Color { get; set; }

    /// <summary>Workflow status of the task. Default: Open.</summary>
    public GanttTaskStatus Status { get; set; } = GanttTaskStatus.Open;

    /// <summary>Priority level. Default: Medium.</summary>
    public GanttTaskPriority Priority { get; set; } = GanttTaskPriority.Medium;

    /// <summary>Optional deadline. When set and End exceeds Deadline, a warning marker is shown.</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>People assigned to this task.</summary>
    public List<GanttAssignee> Assignees { get; set; } = [];

    /// <summary>Estimated effort in hours.</summary>
    public double? EstimationHours { get; set; }

    /// <summary>Actually logged hours.</summary>
    public double? LoggedHours { get; set; }

    /// <summary>User-defined custom field values keyed by field ID.</summary>
    public Dictionary<string, string?> CustomValues { get; set; } = [];

    /// <summary>Rich-text description of the task. May contain plain text or markdown.</summary>
    public string? Description { get; set; }

    /// <summary>Files attached to this task.</summary>
    public List<GanttAttachment> Attachments { get; set; } = [];

    /// <summary>User comments on this task.</summary>
    public List<GanttComment> Comments { get; set; } = [];

    /// <summary>Planned effort in hours (for budget tracking).</summary>
    public double? BudgetHours { get; set; }

    /// <summary>Actual monetary cost incurred so far.</summary>
    public decimal? ActualCost { get; set; }

    /// <summary>Time log entries recorded via the built-in stopwatch.</summary>
    public List<GanttTimeLogEntry> TimeLog { get; set; } = [];

    /// <summary>
    /// When false (default), a group task bar spans min(Start)→max(End) of its direct children.
    /// Set to true to use this task's own Start/End regardless of children.
    /// </summary>
    public bool UseManualDates { get; set; }
}
