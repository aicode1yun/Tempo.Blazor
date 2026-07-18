using System.Collections.Concurrent;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Envelope fanned out between server instances by a collaboration backplane.</summary>
public sealed class DocumentCollaborationBackplaneMessage
{
    /// <summary>Document the message belongs to.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Instance that produced the message — used to suppress echo on the source.</summary>
    public string SourceInstanceId { get; set; } = string.Empty;

    /// <summary>Operation batch payload, when the message carries operations.</summary>
    public DocumentCollaborationOperationBatch? Batch { get; set; }

    /// <summary>Cursor payload, when the message carries a presence update.</summary>
    public DocumentCollaborationCursor? Cursor { get; set; }
}

/// <summary>
/// Cross-instance fan-out boundary for document collaboration: server instances publish operation
/// batches and cursors per document and subscribe to receive the other instances' traffic. The
/// in-memory implementation serves tests and single-process multi-instance setups; a distributed
/// implementation (e.g. Redis pub/sub) makes the collaboration provider multi-server.
/// </summary>
public interface IDocumentCollaborationBackplane
{
    /// <summary>Publishes a message to every subscriber of the message's document.</summary>
    Task PublishAsync(DocumentCollaborationBackplaneMessage message, CancellationToken cancellationToken = default);

    /// <summary>Subscribes to messages for a document. Dispose the returned handle to unsubscribe.</summary>
    Task<IAsyncDisposable> SubscribeAsync(
        string documentId,
        Func<DocumentCollaborationBackplaneMessage, Task> handler,
        CancellationToken cancellationToken = default);
}

/// <summary>In-process backplane: fans messages out to every subscription of the same document.</summary>
public sealed class InMemoryDocumentCollaborationBackplane : IDocumentCollaborationBackplane
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Func<DocumentCollaborationBackplaneMessage, Task>>> _subscriptions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task PublishAsync(DocumentCollaborationBackplaneMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!_subscriptions.TryGetValue(message.DocumentId, out var handlers))
        {
            return;
        }

        foreach (var handler in handlers.Values.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler(message);
        }
    }

    /// <inheritdoc />
    public Task<IAsyncDisposable> SubscribeAsync(
        string documentId,
        Func<DocumentCollaborationBackplaneMessage, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var handlers = _subscriptions.GetOrAdd(documentId, _ => new ConcurrentDictionary<Guid, Func<DocumentCollaborationBackplaneMessage, Task>>());
        var key = Guid.NewGuid();
        handlers[key] = handler;
        return Task.FromResult<IAsyncDisposable>(new Subscription(() => handlers.TryRemove(key, out _)));
    }

    private sealed class Subscription(Action unsubscribe) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            unsubscribe();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// Collaboration provider for multi-server deployments: extends the in-memory provider with a
/// backplane so operation batches and cursors broadcast on one server instance fan out to all
/// others. Each instance keeps its own local sequence numbers; ordering consistency across
/// replicas comes from <see cref="DocumentOperationConflictResolver"/>'s logical-timestamp order,
/// not from server sequences.
/// </summary>
public class BackplaneDocumentCollaborationProvider : InMemoryDocumentCollaborationProvider, IAsyncDisposable
{
    private readonly IDocumentCollaborationBackplane _backplane;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<string, Task<IAsyncDisposable>> _subscriptions = new(StringComparer.Ordinal);

    /// <summary>Creates the provider on top of a shared backplane.</summary>
    public BackplaneDocumentCollaborationProvider(IDocumentCollaborationBackplane backplane)
    {
        _backplane = backplane ?? throw new ArgumentNullException(nameof(backplane));
    }

    /// <inheritdoc />
    public override async Task<DocumentCollaborationSession> JoinAsync(
        DocumentCollaborationJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await base.JoinAsync(request, cancellationToken);
        await EnsureSubscribedAsync(session.DocumentId, cancellationToken);
        return session;
    }

    /// <inheritdoc />
    public override async Task<DocumentCollaborationOperationBatch> BroadcastOperationBatchAsync(
        string sessionId,
        DocumentOperationBatch batch,
        CancellationToken cancellationToken = default)
    {
        var item = await base.BroadcastOperationBatchAsync(sessionId, batch, cancellationToken);
        await _backplane.PublishAsync(new DocumentCollaborationBackplaneMessage
        {
            DocumentId = batch.DocumentId,
            SourceInstanceId = _instanceId,
            Batch = item,
        }, cancellationToken);
        return item;
    }

    /// <inheritdoc />
    public override async Task BroadcastCursorAsync(DocumentCollaborationCursor cursor, CancellationToken cancellationToken = default)
    {
        await base.BroadcastCursorAsync(cursor, cancellationToken);
        await _backplane.PublishAsync(new DocumentCollaborationBackplaneMessage
        {
            DocumentId = cursor.DocumentId,
            SourceInstanceId = _instanceId,
            Cursor = cursor,
        }, cancellationToken);
    }

    private Task EnsureSubscribedAsync(string documentId, CancellationToken cancellationToken)
        => _subscriptions.GetOrAdd(documentId, id => _backplane.SubscribeAsync(id, HandleBackplaneMessageAsync, cancellationToken));

    private Task HandleBackplaneMessageAsync(DocumentCollaborationBackplaneMessage message)
    {
        if (string.Equals(message.SourceInstanceId, _instanceId, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        if (message.Batch is not null)
        {
            IngestExternalOperationBatch(message.DocumentId, message.Batch);
        }

        if (message.Cursor is not null)
        {
            IngestExternalCursor(message.Cursor);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var subscription in _subscriptions.Values.ToArray())
        {
            try
            {
                await (await subscription).DisposeAsync();
            }
            catch
            {
                // Best-effort unsubscription during teardown.
            }
        }

        _subscriptions.Clear();
        GC.SuppressFinalize(this);
    }
}
