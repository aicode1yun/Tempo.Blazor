namespace Tempo.Blazor.NotionEditor.Models;

public class ListViewConfig : IListViewConfig
{
    public IReadOnlyList<Guid> PreviewFieldIds { get; set; } = new List<Guid>();
}
