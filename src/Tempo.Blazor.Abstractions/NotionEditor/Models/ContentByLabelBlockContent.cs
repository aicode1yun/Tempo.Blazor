using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.NotionEditor.Models;

public sealed class ContentByLabelBlockContent : IContentByLabelBlockContent
{
    private IReadOnlyList<string> _labels = [];
    private int _maxItems = 10;

    public IReadOnlyList<string> Labels
    {
        get => _labels;
        set => _labels = value ?? [];
    }

    public int MaxItems
    {
        get => _maxItems;
        set => _maxItems = value is >= 1 and <= 100 ? value : 10;
    }

    public ContentByLabelSortBy SortBy { get; set; } = ContentByLabelSortBy.LastEditedDescending;
}
