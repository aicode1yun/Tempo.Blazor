using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>
/// Demo-friendly SignalR collaboration boundary adapter.
/// Host applications can wrap their actual SignalR hub client behind this provider without adding SignalR dependencies to abstractions.
/// </summary>
public class SignalRDocumentCollaborationProvider : IDocumentCollaborationRealtimeProvider
{
    private readonly IDocumentCollaborationProvider _transport;

    /// <summary>Creates the adapter around a concrete transport provider.</summary>
    public SignalRDocumentCollaborationProvider(IDocumentCollaborationProvider transport)
    {
        _transport = transport;
    }

    /// <inheritdoc />
    public event Func<DocumentCollaborationOperationBatch, CancellationToken, Task>? RemoteOperationBatchReceived;

    /// <inheritdoc />
    public Task<DocumentCollaborationSession> JoinAsync(DocumentCollaborationJoinRequest request, CancellationToken cancellationToken = default)
        => _transport.JoinAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task LeaveAsync(string sessionId, CancellationToken cancellationToken = default)
        => _transport.LeaveAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task<DocumentCollaborationOperationBatch> BroadcastOperationBatchAsync(
        string sessionId,
        DocumentOperationBatch batch,
        CancellationToken cancellationToken = default)
        => _transport.BroadcastOperationBatchAsync(sessionId, batch, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentCollaborationOperationBatch>> GetOperationBatchesAsync(
        string documentId,
        long afterSequence,
        CancellationToken cancellationToken = default)
        => _transport.GetOperationBatchesAsync(documentId, afterSequence, cancellationToken);

    /// <inheritdoc />
    public Task BroadcastCursorAsync(DocumentCollaborationCursor cursor, CancellationToken cancellationToken = default)
        => _transport.BroadcastCursorAsync(cursor, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentCollaborationCursor>> GetCursorsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
        => _transport.GetCursorsAsync(documentId, cancellationToken);

    /// <inheritdoc />
    public async Task ReceiveRemoteOperationBatchAsync(
        DocumentCollaborationOperationBatch batch,
        CancellationToken cancellationToken = default)
    {
        var handler = RemoteOperationBatchReceived;
        if (handler is null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList().Cast<Func<DocumentCollaborationOperationBatch, CancellationToken, Task>>())
        {
            await subscriber(batch, cancellationToken).ConfigureAwait(false);
        }
    }
}
