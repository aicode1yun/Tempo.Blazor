namespace Tempo.Blazor.NotionEditor.Models;

public class TimelineViewConfig : ITimelineViewConfig
{
    public Guid StartDateFieldId { get; set; }
    public Guid? EndDateFieldId { get; set; }
    public bool ShowTableArea { get; set; }
}
