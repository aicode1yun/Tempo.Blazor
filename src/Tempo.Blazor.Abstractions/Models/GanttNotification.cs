namespace Tempo.Blazor.Abstractions.Models;

public class GanttNotification
{
    public string TaskId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public GanttNotificationType Type { get; set; }
}

public enum GanttNotificationType { Assign, Mention, Deadline }
