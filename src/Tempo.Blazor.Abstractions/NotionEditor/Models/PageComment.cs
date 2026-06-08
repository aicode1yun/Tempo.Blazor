namespace Tempo.Blazor.NotionEditor.Models;

public class PageComment : BlockComment, IPageComment
{
    public string PageId { get; set; } = string.Empty;
}
