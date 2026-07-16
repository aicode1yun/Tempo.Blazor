using System.Text.Json;
using Microsoft.JSInterop;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Demo.SharedUI.Services;

/// <summary>
/// Demo <see cref="IPdfAnnotationProvider"/> that persists annotation threads to
/// browser localStorage, so annotations survive a page reload. Raises
/// <see cref="Changed"/> after every mutation so multiple annotator instances
/// sharing the provider can refresh (two-user demo).
/// </summary>
public sealed class LocalStoragePdfAnnotationProvider : IPdfAnnotationProvider
{
    private const string KeyPrefix = "tm-demo-pdf-annotations:";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IJSRuntime _js;
    private readonly Dictionary<string, List<DocumentCommentThread>> _cache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loaded = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Raised after any mutation, so shared consumers can reload.</summary>
    public event Action? Changed;

    /// <summary>Creates the provider.</summary>
    /// <param name="js">JS runtime used to reach localStorage.</param>
    public LocalStoragePdfAnnotationProvider(IJSRuntime js)
    {
        _js = js;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentCommentThread>> GetThreadsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var list = await EnsureLoadedAsync(documentId);
            return [.. list];
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DocumentCommentThread> CreateThreadAsync(
        string documentId,
        DocumentCommentThreadCreateRequest request,
        DocumentCommentUser author,
        CancellationToken cancellationToken = default)
    {
        var thread = DocumentCommentFactory.CreateThread(request, author);
        await MutateAsync(documentId, list => list.Add(thread), cancellationToken);
        return thread;
    }

    /// <inheritdoc />
    public async Task<DocumentCommentThread> ReplyAsync(
        string documentId,
        DocumentCommentReplyRequest request,
        DocumentCommentUser author,
        CancellationToken cancellationToken = default)
    {
        var comment = DocumentCommentFactory.CreateReply(request, author);
        DocumentCommentThread? result = null;
        await MutateAsync(documentId, list =>
        {
            var thread = Find(list, request.ThreadId);
            thread.Comments.Add(comment);
            result = thread;
        }, cancellationToken);
        return result!;
    }

    /// <inheritdoc />
    public async Task<DocumentCommentThread> EditAsync(
        string documentId,
        DocumentCommentEditRequest request,
        CancellationToken cancellationToken = default)
    {
        DocumentCommentThread? result = null;
        await MutateAsync(documentId, list =>
        {
            var thread = Find(list, request.ThreadId);
            var comment = thread.Comments.FirstOrDefault(c => string.Equals(c.Id, request.CommentId, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException($"Comment '{request.CommentId}' was not found.");
            comment.Body = request.Body ?? string.Empty;
            comment.Mentions = request.Mentions is null ? [] : [.. request.Mentions];
            comment.EditedAt = DateTimeOffset.UtcNow;
            result = thread;
        }, cancellationToken);
        return result!;
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        string documentId,
        DocumentCommentDeleteRequest request,
        CancellationToken cancellationToken = default)
        => MutateAsync(documentId, list =>
        {
            var thread = list.FirstOrDefault(t => string.Equals(t.Id, request.ThreadId, StringComparison.Ordinal));
            if (thread is null)
            {
                return;
            }

            thread.Comments.RemoveAll(c => string.Equals(c.Id, request.CommentId, StringComparison.Ordinal));
            if (thread.Comments.Count == 0)
            {
                list.Remove(thread);
            }
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<DocumentCommentThread> ResolveAsync(
        string documentId,
        string threadId,
        DocumentCommentUser? resolvedBy = null,
        CancellationToken cancellationToken = default)
    {
        DocumentCommentThread? result = null;
        await MutateAsync(documentId, list =>
        {
            var thread = Find(list, threadId);
            thread.Status = DocumentCommentThreadStatus.Resolved;
            thread.ResolvedByUserId = resolvedBy?.UserId;
            thread.ResolvedByName = resolvedBy?.DisplayName;
            thread.ResolvedAt = DateTimeOffset.UtcNow;
            result = thread;
        }, cancellationToken);
        return result!;
    }

    /// <inheritdoc />
    public async Task<DocumentCommentThread> ReopenAsync(
        string documentId,
        string threadId,
        DocumentCommentUser? reopenedBy = null,
        CancellationToken cancellationToken = default)
    {
        DocumentCommentThread? result = null;
        await MutateAsync(documentId, list =>
        {
            var thread = Find(list, threadId);
            thread.Status = DocumentCommentThreadStatus.Open;
            thread.ResolvedByUserId = null;
            thread.ResolvedByName = null;
            thread.ResolvedAt = null;
            result = thread;
        }, cancellationToken);
        return result!;
    }

    private async Task MutateAsync(string documentId, Action<List<DocumentCommentThread>> mutation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var list = await EnsureLoadedAsync(documentId);
            mutation(list);
            await SaveAsync(documentId, list);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke();
    }

    private async Task<List<DocumentCommentThread>> EnsureLoadedAsync(string documentId)
    {
        if (_loaded.Contains(documentId))
        {
            return _cache.TryGetValue(documentId, out var cached) ? cached : (_cache[documentId] = []);
        }

        var list = _cache.TryGetValue(documentId, out var existing) ? existing : (_cache[documentId] = []);
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", KeyPrefix + documentId);
            if (!string.IsNullOrEmpty(json))
            {
                list = JsonSerializer.Deserialize<List<DocumentCommentThread>>(json, _jsonOptions) ?? [];
                _cache[documentId] = list;
            }

            // Only a successful read marks the document loaded; a transient JS failure
            // (e.g. prerender) must not cache an empty list that a later save would
            // write back over the persisted annotations.
            _loaded.Add(documentId);
        }
        catch
        {
            // Storage unreachable: keep the in-memory list and retry the read next call.
        }

        return list;
    }

    private async Task SaveAsync(string documentId, List<DocumentCommentThread> list)
    {
        try
        {
            var json = JsonSerializer.Serialize(list, _jsonOptions);
            await _js.InvokeVoidAsync("localStorage.setItem", KeyPrefix + documentId, json);
        }
        catch
        {
            // Storage unavailable: annotations stay in memory for this session.
        }
    }

    private static DocumentCommentThread Find(List<DocumentCommentThread> list, string threadId)
        => list.FirstOrDefault(t => string.Equals(t.Id, threadId, StringComparison.Ordinal))
           ?? throw new KeyNotFoundException($"Thread '{threadId}' was not found.");
}
