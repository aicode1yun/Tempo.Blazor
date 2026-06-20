namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Controls which events trigger email or push notifications for the current user.</summary>
public class GanttNotificationSettings
{
    public bool EmailOnAssign  { get; set; }
    public bool EmailOnMention { get; set; }
    public bool PushOnAssign   { get; set; }
    public bool PushOnMention  { get; set; }
    public bool PushOnDeadline { get; set; }
}
