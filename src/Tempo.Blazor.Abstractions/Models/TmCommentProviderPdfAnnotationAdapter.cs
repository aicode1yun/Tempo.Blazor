using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Adapts the shared <see cref="ITmCommentProvider"/> to the PDF viewer
/// <see cref="IPdfAnnotationProvider"/> contract, translating between the document-viewer
/// comment models and the shared comment models via <see cref="DocumentViewerCommentBridge"/>.
/// </summary>
public sealed class TmCommentProviderPdfAnnotationAdapter : IPdfAnnotationProvider
{
    private readonly ITmCommentProvider _inner;

    /// <summary>Creates an adapter over a shared comment provider.</summary>
    /// <param name="inner">Shared comment provider to delegate to.</param>
    public TmCommentProviderPdfAnnotationAdapter(ITmCommentProvider inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentCommentThread>> GetThreadsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var threads = await _inner.GetForEntityAsync(DocumentViewerCommentBridge.Entity(documentId), cancellationToken)
            .ConfigureAwait(false);
        return threads.Select(DocumentViewerCommentBridge.ToDocumentCommentThread).ToList();
    }

    /// <inheritdoc />
    public async Task<DocumentCommentThread> CreateThreadAsync(
        string documentId,
        DocumentCommentThreadCreateRequest request,
        DocumentCommentUser author,
        CancellationToken cancellationToken = default)
    {
        var thread = DocumentCommentFactory.CreateThread(request, author);
        var tmThread = DocumentViewerCommentBridge.ToTmCommentThread(thread, documentId);
        var created = await _inner.CreateThreadAsync(tmThread, cancellationToken).ConfigureAwait(false);
        return DocumentViewerCommentBridge.ToDocumentCommentThread(created);
    }

    /// <inheritdoc />
    public async Task<DocumentCommentThread> ReplyAsync(
        string documentId,
        DocumentCommentReplyRequest request,
        DocumentCommentUser author,
        CancellationToken cancellationToken = default)
    {
        var comment = DocumentCommentFactory.CreateReply(request, author);
        var entry = DocumentViewerCommentBridge.ToTmCommentEntry(comment, request.ThreadId);
        await _inner.ReplyAsync(request.ThreadId, entry, cancellationToken).ConfigureAwait(false);
        return await ReloadThreadAsync(documentId, request.ThreadId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DocumentCommentThread> EditAsync(
        string documentId,
        DocumentCommentEditRequest request,
        CancellationToken cancellationToken = default)
    {
        // Load the existing comment so author, creation time, and reactions are preserved
        // when the inner provider replaces the entry.
        var current = await ReloadThreadAsync(documentId, request.ThreadId, cancellationToken).ConfigureAwait(false);
        var comment = current.Comments.FirstOrDefault(c => string.Equals(c.Id, request.CommentId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Comment '{request.CommentId}' was not found in thread '{request.ThreadId}'.");

        comment.Body = request.Body ?? string.Empty;
        comment.Mentions = request.Mentions is null ? [] : [.. request.Mentions];
        comment.EditedAt = DateTimeOffset.UtcNow;

        var entry = DocumentViewerCommentBridge.ToTmCommentEntry(comment, request.ThreadId);
        await _inner.UpdateEntryAsync(request.ThreadId, request.CommentId, entry, cancellationToken).ConfigureAwait(false);
        return await ReloadThreadAsync(documentId, request.ThreadId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        string documentId,
        DocumentCommentDeleteRequest request,
        CancellationToken cancellationToken = default)
        => _inner.DeleteEntryAsync(request.ThreadId, request.CommentId, cancellationToken);

    /// <inheritdoc />
    public async Task<DocumentCommentThread> ResolveAsync(
        string documentId,
        string threadId,
        DocumentCommentUser? resolvedBy = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await _inner.ResolveAsync(threadId, ToTmUserRef(resolvedBy), cancellationToken).ConfigureAwait(false);
        return DocumentViewerCommentBridge.ToDocumentCommentThread(resolved);
    }

    /// <inheritdoc />
    public async Task<DocumentCommentThread> ReopenAsync(
        string documentId,
        string threadId,
        DocumentCommentUser? reopenedBy = null,
        CancellationToken cancellationToken = default)
    {
        var reopened = await _inner.ReopenAsync(threadId, ToTmUserRef(reopenedBy), cancellationToken).ConfigureAwait(false);
        return DocumentViewerCommentBridge.ToDocumentCommentThread(reopened);
    }

    private async Task<DocumentCommentThread> ReloadThreadAsync(
        string documentId,
        string threadId,
        CancellationToken cancellationToken)
    {
        var threads = await GetThreadsAsync(documentId, cancellationToken).ConfigureAwait(false);
        return threads.FirstOrDefault(thread => string.Equals(thread.Id, threadId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Thread '{threadId}' was not found for document '{documentId}'.");
    }

    private static TmUserRef? ToTmUserRef(DocumentCommentUser? user)
        => user is null
            ? null
            : new TmUserRef
            {
                Id = user.UserId,
                DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserId : user.DisplayName,
                AvatarUrl = user.AvatarUrl
            };
}
