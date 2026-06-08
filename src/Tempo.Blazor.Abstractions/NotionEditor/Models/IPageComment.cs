namespace Tempo.Blazor.NotionEditor.Models;

public interface IPageComment : IBlockComment
{
    string PageId { get; }
}
