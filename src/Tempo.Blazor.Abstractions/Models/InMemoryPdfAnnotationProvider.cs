using System.Collections.Concurrent;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// In-memory <see cref="IPdfAnnotationProvider"/> that keeps annotation threads per
/// document for the lifetime of the instance. Suitable for demos, tests, and prototypes.
/// </summary>
public sealed class InMemoryPdfAnnotationProvider : IPdfAnnotationProvider
{
    private readonly ConcurrentDictionary<string, List<DocumentCommentThread>> _store = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>Creates an empty provider.</summary>
    public InMemoryPdfAnnotationProvider()
    {
    }

    /// <summary>Creates a provider seeded with existing threads per document.</summary>
    /// <param name="seed">Initial threads keyed by document identifier.</param>
    public InMemoryPdfAnnotationProvider(IReadOnlyDictionary<string, IReadOnlyList<DocumentCommentThread>> seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        foreach (var pair in seed)
        {
            _store[pair.Key] = [.. pair.Value];
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentCommentThread>> GetThreadsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<DocumentCommentThread> result = _store.TryGetValue(documentId, out var threads)
                ? [.. threads]
                : [];
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<DocumentCommentThread> CreateThreadAsync(
        string documentId,
        DocumentCommentThreadCreateRequest request,
        DocumentCommentUser author,
        CancellationToken cancellationToken = default)
    {
        var thread = DocumentCommentFactory.CreateThread(request, author);
        lock (_gate)
        {
            var list = _store.GetOrAdd(documentId, static _ => []);
            list.Add(thread);
        }

        return Task.FromResult(thread);
    }

    /// <inheritdoc />
    public Task<DocumentCommentThread> ReplyAsync(
        string documentId,
        DocumentCommentReplyRequest request,
        DocumentCommentUser author,
        CancellationToken cancellationToken = default)
    {
        var comment = DocumentCommentFactory.CreateReply(request, author);
        lock (_gate)
        {
            var thread = Find(documentId, request.ThreadId);
            thread.Comments.Add(comment);
            return Task.FromResult(thread);
        }
    }

    /// <inheritdoc />
    public Task<DocumentCommentThread> EditAsync(
        string documentId,
        DocumentCommentEditRequest request,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var thread = Find(documentId, request.ThreadId);
            var comment = thread.Comments.FirstOrDefault(c => string.Equals(c.Id, request.CommentId, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException($"Comment '{request.CommentId}' was not found.");
            comment.Body = request.Body ?? string.Empty;
            comment.Mentions = request.Mentions is null ? [] : [.. request.Mentions];
            comment.EditedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(thread);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        string documentId,
        DocumentCommentDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_store.TryGetValue(documentId, out var list))
            {
                return Task.CompletedTask;
            }

            var thread = list.FirstOrDefault(t => string.Equals(t.Id, request.ThreadId, StringComparison.Ordinal));
            if (thread is null)
            {
                return Task.CompletedTask;
            }

            thread.Comments.RemoveAll(c => string.Equals(c.Id, request.CommentId, StringComparison.Ordinal));
            if (thread.Comments.Count == 0)
            {
                list.Remove(thread);
            }

            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task<DocumentCommentThread> ResolveAsync(
        string documentId,
        string threadId,
        DocumentCommentUser? resolvedBy = null,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var thread = Find(documentId, threadId);
            thread.Status = DocumentCommentThreadStatus.Resolved;
            thread.ResolvedByUserId = resolvedBy?.UserId;
            thread.ResolvedByName = resolvedBy?.DisplayName;
            thread.ResolvedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(thread);
        }
    }

    /// <inheritdoc />
    public Task<DocumentCommentThread> ReopenAsync(
        string documentId,
        string threadId,
        DocumentCommentUser? reopenedBy = null,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var thread = Find(documentId, threadId);
            thread.Status = DocumentCommentThreadStatus.Open;
            thread.ResolvedByUserId = null;
            thread.ResolvedByName = null;
            thread.ResolvedAt = null;
            return Task.FromResult(thread);
        }
    }

    private DocumentCommentThread Find(string documentId, string threadId)
    {
        if (_store.TryGetValue(documentId, out var list))
        {
            var thread = list.FirstOrDefault(t => string.Equals(t.Id, threadId, StringComparison.Ordinal));
            if (thread is not null)
            {
                return thread;
            }
        }

        throw new KeyNotFoundException($"Thread '{threadId}' was not found for document '{documentId}'.");
    }
}
