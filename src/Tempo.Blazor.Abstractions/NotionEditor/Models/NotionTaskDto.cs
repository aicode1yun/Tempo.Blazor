namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Aggregated task item resolved from a Notion todo block or an external task source.</summary>
public sealed class NotionTaskDto
{
    /// <summary>Stable task identifier used by the task provider.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Source page identifier.</summary>
    public string PageId { get; set; } = string.Empty;

    /// <summary>Source page title.</summary>
    public string PageTitle { get; set; } = string.Empty;

    /// <summary>Source block identifier.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Plain-text task body.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Assigned user id, when present.</summary>
    public string? AssigneeId { get; set; }

    /// <summary>Assigned user display name, when present.</summary>
    public string? AssigneeDisplayName { get; set; }

    /// <summary>Task due date, when present.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Whether the task is completed.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Task creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }
}
