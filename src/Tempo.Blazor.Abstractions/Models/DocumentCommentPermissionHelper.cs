namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Helper methods for document comment permissions.</summary>
public static class DocumentCommentPermissionHelper
{
    /// <summary>Returns true when the current user may edit a comment.</summary>
    /// <param name="comment">Comment to inspect.</param>
    /// <param name="currentUserId">Current user identifier.</param>
    public static bool CanEdit(DocumentComment? comment, string? currentUserId)
    {
        return comment?.CanEdit == true
            || (!string.IsNullOrWhiteSpace(currentUserId)
                && string.Equals(comment?.AuthorId, currentUserId, StringComparison.Ordinal));
    }

    /// <summary>Returns true when the current user may delete a comment.</summary>
    /// <param name="comment">Comment to inspect.</param>
    /// <param name="currentUserId">Current user identifier.</param>
    public static bool CanDelete(DocumentComment? comment, string? currentUserId)
    {
        return comment?.CanDelete == true
            || (!string.IsNullOrWhiteSpace(currentUserId)
                && string.Equals(comment?.AuthorId, currentUserId, StringComparison.Ordinal));
    }
}
