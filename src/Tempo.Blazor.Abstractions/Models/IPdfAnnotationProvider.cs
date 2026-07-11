namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Persistence contract for PDF viewer annotations (comment threads anchored to page
/// points, areas, or text ranges). Implementations load, create, reply to, edit,
/// delete, resolve, and reopen threads for a given document.
/// </summary>
public interface IPdfAnnotationProvider
{
    /// <summary>Loads all annotation threads for a document.</summary>
    /// <param name="documentId">Stable document identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<DocumentCommentThread>> GetThreadsAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a new annotation thread with an initial comment.</summary>
    /// <param name="documentId">Stable document identifier.</param>
    /// <param name="request">Anchor and body for the new thread.</param>
    /// <param name="author">Author of the initial comment.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<DocumentCommentThread> CreateThreadAsync(
        string documentId,
        DocumentCommentThreadCreateRequest request,
        DocumentCommentUser author,
        CancellationToken cancellationToken = default);

    /// <summary>Appends a reply to an existing thread.</summary>
    /// <param name="documentId">Stable document identifier.</param>
    /// <param name="request">Target thread and reply body.</param>
    /// <param name="author">Author of the reply.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<DocumentCommentThread> ReplyAsync(
        string documentId,
        DocumentCommentReplyRequest request,
        DocumentCommentUser author,
        CancellationToken cancellationToken = default);

    /// <summary>Edits the body of an existing comment.</summary>
    /// <param name="documentId">Stable document identifier.</param>
    /// <param name="request">Target thread, comment, and updated body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<DocumentCommentThread> EditAsync(
        string documentId,
        DocumentCommentEditRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a comment. Deleting the last comment removes the whole thread.</summary>
    /// <param name="documentId">Stable document identifier.</param>
    /// <param name="request">Target thread and comment.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task DeleteAsync(
        string documentId,
        DocumentCommentDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Marks a thread as resolved.</summary>
    /// <param name="documentId">Stable document identifier.</param>
    /// <param name="threadId">Target thread identifier.</param>
    /// <param name="resolvedBy">User that resolved the thread, when known.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<DocumentCommentThread> ResolveAsync(
        string documentId,
        string threadId,
        DocumentCommentUser? resolvedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reopens a resolved thread.</summary>
    /// <param name="documentId">Stable document identifier.</param>
    /// <param name="threadId">Target thread identifier.</param>
    /// <param name="reopenedBy">User that reopened the thread, when known.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<DocumentCommentThread> ReopenAsync(
        string documentId,
        string threadId,
        DocumentCommentUser? reopenedBy = null,
        CancellationToken cancellationToken = default);
}
