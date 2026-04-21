namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

public interface INotionCommentProvider
{
    Task<IEnumerable<IBlockComment>> GetBlockCommentsAsync(string blockId);
    Task<IBlockComment> AddBlockCommentAsync(string blockId, string htmlContent);
    Task<INotionCommentEntry> ReplyToCommentAsync(string commentId, string htmlContent);
    Task<INotionCommentEntry> EditCommentAsync(string commentId, string htmlContent);
    Task DeleteCommentAsync(string commentId);
    Task<IBlockComment> ResolveCommentAsync(string commentId);
    Task<IBlockComment> UnresolveCommentAsync(string commentId);

    Task<IBlockComment> AddTextAnchorCommentAsync(string blockId, int startOffset, int endOffset, string highlightedText, string htmlContent);

    Task<IEnumerable<IBlockComment>> GetPageCommentsAsync(string pageId);
    Task<IBlockComment> AddPageCommentAsync(string pageId, string htmlContent);

    Task<int> GetUnresolvedCommentsCountAsync(string pageId);
}
