using Microsoft.AspNetCore.SignalR;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Demo.Api.Hubs;

/// <summary>SignalR hub for realtime document editor collaboration.</summary>
public sealed class DocumentEditorCollaborationHub : Hub
{
    private static readonly Dictionary<string, DocumentCollaborationSession> SessionsByConnection = [];
    private static readonly Lock SessionLock = new();
    private readonly InMemoryDocumentCollaborationProvider _collaboration;
    private readonly ILogger<DocumentEditorCollaborationHub> _logger;

    /// <summary>Creates the hub with the shared collaboration store.</summary>
    public DocumentEditorCollaborationHub(
        InMemoryDocumentCollaborationProvider collaboration,
        ILogger<DocumentEditorCollaborationHub> logger)
    {
        _collaboration = collaboration;
        _logger = logger;
    }

    /// <summary>Joins a document collaboration group.</summary>
    public async Task<DocumentCollaborationSession> JoinDocument(DocumentCollaborationJoinRequest request)
    {
        var session = await _collaboration.JoinAsync(request);
        await Groups.AddToGroupAsync(Context.ConnectionId, DocumentGroup(session.DocumentId));
        lock (SessionLock)
        {
            SessionsByConnection[Context.ConnectionId] = session;
        }

        _logger.LogInformation(
            "Document collaboration joined. DocumentId={DocumentId}, SessionId={SessionId}, ClientId={ClientId}",
            session.DocumentId,
            session.Id,
            session.ClientId);
        return session;
    }

    /// <summary>Leaves a document collaboration group.</summary>
    public async Task LeaveDocument(string sessionId)
    {
        DocumentCollaborationSession? session = null;
        lock (SessionLock)
        {
            if (SessionsByConnection.TryGetValue(Context.ConnectionId, out var current)
                && string.Equals(current.Id, sessionId, StringComparison.Ordinal))
            {
                session = current;
                SessionsByConnection.Remove(Context.ConnectionId);
            }
        }

        if (session is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, DocumentGroup(session.DocumentId));
        }

        await _collaboration.LeaveAsync(sessionId);
    }

    /// <summary>Broadcasts a local operation batch to other document sessions.</summary>
    public async Task<DocumentCollaborationOperationBatch> BroadcastOperationBatch(
        string sessionId,
        DocumentOperationBatch batch)
    {
        var broadcast = await _collaboration.BroadcastOperationBatchAsync(sessionId, batch);
        _logger.LogInformation(
            "Document collaboration batch broadcast. DocumentId={DocumentId}, SessionId={SessionId}, Sequence={Sequence}, Operations={OperationCount}, TransactionId={TransactionId}, LocalSequence={LocalSequence}",
            broadcast.Batch.DocumentId,
            broadcast.SessionId,
            broadcast.Sequence,
            broadcast.Batch.Operations.Count,
            broadcast.Batch.TransactionId,
            broadcast.Batch.LocalSequence);
        await Clients.OthersInGroup(DocumentGroup(broadcast.Batch.DocumentId))
            .SendAsync(SignalRDocumentCollaborationProvider.HubMethods.RemoteOperationBatchReceived, broadcast);
        return broadcast;
    }

    /// <summary>Gets missed operation batches for reconnect recovery.</summary>
    public Task<IReadOnlyList<DocumentCollaborationOperationBatch>> GetOperationBatches(
        string documentId,
        long afterSequence)
    {
        return _collaboration.GetOperationBatchesAsync(documentId, afterSequence);
    }

    /// <summary>Broadcasts a local cursor to other document sessions.</summary>
    public async Task BroadcastCursor(DocumentCollaborationCursor cursor)
    {
        await _collaboration.BroadcastCursorAsync(cursor);
        _logger.LogDebug(
            "Document collaboration cursor broadcast. DocumentId={DocumentId}, SessionId={SessionId}, Offset={Offset}",
            cursor.DocumentId,
            cursor.SessionId,
            cursor.Offset);
        await Clients.OthersInGroup(DocumentGroup(cursor.DocumentId))
            .SendAsync(SignalRDocumentCollaborationProvider.HubMethods.RemoteCursorReceived, cursor);
    }

    /// <summary>Gets currently active cursors for reconnect recovery.</summary>
    public Task<IReadOnlyList<DocumentCollaborationCursor>> GetCursors(string documentId)
    {
        return _collaboration.GetCursorsAsync(documentId);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        DocumentCollaborationSession? session = null;
        lock (SessionLock)
        {
            if (SessionsByConnection.Remove(Context.ConnectionId, out var current))
            {
                session = current;
            }
        }

        if (session is not null)
        {
            await _collaboration.LeaveAsync(session.Id);
            await Clients.OthersInGroup(DocumentGroup(session.DocumentId))
                .SendAsync(SignalRDocumentCollaborationProvider.HubMethods.RemoteCursorReceived, new DocumentCollaborationCursor
                {
                    DocumentId = session.DocumentId,
                    SessionId = session.Id,
                    ClientId = session.ClientId,
                    DisplayName = string.Empty,
                    Offset = -1
                });
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Gets the SignalR group name for a document.</summary>
    public static string DocumentGroup(string documentId) => $"document-editor:{documentId}";
}
