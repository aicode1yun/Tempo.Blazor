namespace Tempo.Blazor.NotionEditor.Models;

public interface ICalendarViewConfig : IDatabaseViewConfig
{
    Guid DateFieldId { get; }
}
