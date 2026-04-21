namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class PageMention : IPageMention
{
    public InlineMentionType Type { get; set; } = InlineMentionType.Page;
    public int TextOffset { get; set; }
    public Guid PageId { get; set; }
    public string PageTitle { get; set; } = string.Empty;
    public string? PageIconEmoji { get; set; }
}
