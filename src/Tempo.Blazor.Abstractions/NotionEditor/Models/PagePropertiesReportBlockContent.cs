namespace Tempo.Blazor.NotionEditor.Models;

public sealed class PagePropertiesReportBlockContent : IPagePropertiesReportBlockContent
{
    private IReadOnlyList<string> _labels = [];
    private IReadOnlyList<string> _columns = [];

    public IReadOnlyList<string> Labels
    {
        get => _labels;
        set => _labels = value ?? [];
    }

    public IReadOnlyList<string> Columns
    {
        get => _columns;
        set => _columns = value ?? [];
    }
}
