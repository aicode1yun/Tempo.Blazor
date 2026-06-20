namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class NotionSearchResult
{
    public Guid PageId { get; set; }
    public string PageTitle { get; set; } = string.Empty;
    public string? PageIconEmoji { get; set; }
    public Guid? BlockId { get; set; }
    public BlockType? BlockType { get; set; }
    public string MatchSnippet { get; set; } = string.Empty;
    public IReadOnlyList<NotionSearchHighlightRange> HighlightRanges { get; set; } = [];
}
