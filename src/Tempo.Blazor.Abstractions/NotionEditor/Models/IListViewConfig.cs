namespace Tempo.Blazor.NotionEditor.Models;

public interface IListViewConfig : IDatabaseViewConfig
{
    IReadOnlyList<Guid> PreviewFieldIds { get; }
}
