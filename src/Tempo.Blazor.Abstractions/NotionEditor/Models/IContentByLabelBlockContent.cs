using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.NotionEditor.Models;

public interface IContentByLabelBlockContent : IBlockContent
{
    IReadOnlyList<string> Labels { get; }
    int MaxItems { get; }
    ContentByLabelSortBy SortBy { get; }
}
