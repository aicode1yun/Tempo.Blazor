namespace Tempo.Blazor.Abstractions.Models;

public class GanttTimeLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TaskId { get; set; } = string.Empty;
    public string AssigneeId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public string? Notes { get; set; }
}
