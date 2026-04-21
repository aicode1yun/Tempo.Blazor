namespace Tempo.Blazor.NotionEditor.Models;

public interface ITimelineViewConfig : IDatabaseViewConfig
{
    Guid StartDateFieldId { get; }
    Guid? EndDateFieldId { get; }
    bool ShowTableArea { get; }
}
