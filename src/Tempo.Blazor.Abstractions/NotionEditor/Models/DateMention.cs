namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class DateMention : IDateMention
{
    public InlineMentionType Type { get; set; } = InlineMentionType.Date;
    public int TextOffset { get; set; }
    public DateTime Date { get; set; }
    public DateTime? EndDate { get; set; }
    public string? TimeZone { get; set; }
    public bool IncludeTime { get; set; }
}
