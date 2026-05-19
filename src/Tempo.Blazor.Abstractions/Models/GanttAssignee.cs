namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Represents a person assigned to a Gantt task.</summary>
public class GanttAssignee
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    /// <summary>Hourly billing rate for cost tracking. Null = not specified.</summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>When true, this is a generic placeholder resource (not a real user account).</summary>
    public bool IsVirtual { get; set; }
}
