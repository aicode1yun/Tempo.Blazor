namespace Tempo.Blazor.NotionEditor.Models;

public sealed class PagePropertiesBlockContent : IPagePropertiesBlockContent
{
    private IReadOnlyList<PagePropertyRow> _rows = [];

    public IReadOnlyList<PagePropertyRow> Rows
    {
        get => _rows;
        set => _rows = value ?? [];
    }
}
