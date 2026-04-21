namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class Mention
{
    public Guid Id { get; set; }
    public InlineMentionType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime? DateValue { get; set; }
    public DateTime? DateRangeStart { get; set; }
    public DateTime? DateRangeEnd { get; set; }
    public string? ReminderTiming { get; set; }
}
