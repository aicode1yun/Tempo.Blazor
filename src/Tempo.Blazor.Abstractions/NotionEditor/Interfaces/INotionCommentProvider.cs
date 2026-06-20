namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

public interface INotionCommentProvider
{
    Task<IEnumerable<IBlockComment>> GetBlockCommentsAsync(string blockId);
    Task<IBlockComment> AddBlockCommentAsync(string blockId, string htmlContent);
    Task<INotionCommentEntry> ReplyToCommentAsync(string commentId, string htmlContent, string? parentEntryId = null);
    Task<INotionCommentEntry> EditCommentAsync(string commentId, string htmlContent);
    Task DeleteCommentAsync(string commentId);
    Task DeleteCommentEntryAsync(string entryId);
    Task<IBlockComment> ResolveCommentAsync(string commentId);
    Task<IBlockComment> UnresolveCommentAsync(string commentId);

    Task<IBlockComment> AddTextAnchorCommentAsync(string blockId, int startOffset, int endOffset, string highlightedText, string htmlContent, string commentId);

    Task<IEnumerable<IPageComment>> GetPageCommentsAsync(string pageId);
    Task<IPageComment> AddPageCommentAsync(string pageId, string htmlContent);

    Task<int> GetUnresolvedCommentsCountAsync(string pageId);

    // ── Read tracking ─────────────────────────────────────────────────────────

    /// <summary>Marks the comment thread as read for the given user.</summary>
    Task MarkThreadAsReadAsync(string commentId, string userId);

    /// <summary>Removes the read mark for the given user (marks as unread).</summary>
    Task MarkThreadAsUnreadAsync(string commentId, string userId);

    /// <summary>Marks all comment threads for the given block/page as read for the user.</summary>
    Task MarkAllThreadsAsReadAsync(string ownerId, string userId);

    // ── Reactions ─────────────────────────────────────────────────────────────

    Task<IReadOnlyList<ICommentReaction>> GetReactionsAsync(string entryId);
    Task AddReactionAsync(string entryId, string emoji, string userId);
    Task RemoveReactionAsync(string entryId, string emoji, string userId);

    // ── Subscribe / Unsubscribe ───────────────────────────────────────────────

    Task SubscribeToThreadAsync(string commentId, string userId);
    Task UnsubscribeFromThreadAsync(string commentId, string userId);
}
