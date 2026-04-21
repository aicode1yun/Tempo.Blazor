namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class ReminderMention : IReminderMention
{
    public InlineMentionType Type { get; set; } = InlineMentionType.Reminder;
    public int TextOffset { get; set; }
    public DateTime Date { get; set; }
    public DateTime? EndDate { get; set; }
    public string? TimeZone { get; set; }
    public bool IncludeTime { get; set; }
    public ReminderTiming Timing { get; set; }
}
