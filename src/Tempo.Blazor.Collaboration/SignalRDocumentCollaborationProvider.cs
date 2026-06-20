using Microsoft.AspNetCore.SignalR.Client;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>SignalR-backed realtime document collaboration provider.</summary>
public sealed class SignalRDocumentCollaborationProvider : IDocumentCollaborationRealtimeProvider, IAsyncDisposable
{
    /// <summary>Hub method names used by the provider.</summary>
    public static class HubMethods
    {
        /// <summary>Joins a document collaboration group.</summary>
        public const string JoinDocument = nameof(JoinDocument);

        /// <summary>Leaves a document collaboration group.</summary>
        public const string LeaveDocument = nameof(LeaveDocument);

        /// <summary>Broadcasts a local operation batch.</summary>
        public const string BroadcastOperationBatch = nameof(BroadcastOperationBatch);

        /// <summary>Gets missed operation batches for reconnect recovery.</summary>
        public const string GetOperationBatches = nameof(GetOperationBatches);

        /// <summary>Broadcasts a local cursor.</summary>
        public const string BroadcastCursor = nameof(BroadcastCursor);

        /// <summary>Gets currently active cursors for reconnect recovery.</summary>
        public const string GetCursors = nameof(GetCursors);

        /// <summary>Client event carrying a remote operation batch.</summary>
        public const string RemoteOperationBatchReceived = nameof(RemoteOperationBatchReceived);

        /// <summary>Client event carrying a remote cursor.</summary>
        public const string RemoteCursorReceived = nameof(RemoteCursorReceived);
    }

    private readonly IDocumentCollaborationProvider? _transport;
    private readonly HubConnection? _hub;
    private bool _started;

    /// <summary>Creates an in-process realtime adapter around an existing provider.</summary>
    public SignalRDocumentCollaborationProvider(IDocumentCollaborationProvider transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <summary>Creates a SignalR provider from a hub URL.</summary>
    public SignalRDocumentCollaborationProvider(string hubUrl)
        : this(new HubConnectionBuilder().WithUrl(hubUrl).WithAutomaticReconnect().Build())
    {
    }

    /// <summary>Creates a SignalR provider from a prepared hub connection.</summary>
    public SignalRDocumentCollaborationProvider(HubConnection hub)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _hub.On<DocumentCollaborationOperationBatch>(HubMethods.RemoteOperationBatchReceived, batch =>
            _ = ReceiveRemoteOperationBatchAsync(batch));
        _hub.On<DocumentCollaborationCursor>(HubMethods.RemoteCursorReceived, cursor =>
            _ = ReceiveRemoteCursorAsync(cursor));
    }

    /// <inheritdoc />
    public event Func<DocumentCollaborationOperationBatch, CancellationToken, Task>? RemoteOperationBatchReceived;

    /// <inheritdoc />
    public event Func<DocumentCollaborationCursor, CancellationToken, Task>? RemoteCursorReceived;

    /// <inheritdoc />
    public async Task<DocumentCollaborationSession> JoinAsync(
        DocumentCollaborationJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
        {
            return await _transport.JoinAsync(request, cancellationToken).ConfigureAwait(false);
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await _hub!.InvokeAsync<DocumentCollaborationSession>(
            HubMethods.JoinDocument,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LeaveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
        {
            await _transport.LeaveAsync(sessionId, cancellationToken).ConfigureAwait(false);
            return;
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _hub!.InvokeAsync(HubMethods.LeaveDocument, sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DocumentCollaborationOperationBatch> BroadcastOperationBatchAsync(
        string sessionId,
        DocumentOperationBatch batch,
        CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
        {
            return await _transport.BroadcastOperationBatchAsync(sessionId, batch, cancellationToken).ConfigureAwait(false);
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await _hub!.InvokeAsync<DocumentCollaborationOperationBatch>(
            HubMethods.BroadcastOperationBatch,
            sessionId,
            batch,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentCollaborationOperationBatch>> GetOperationBatchesAsync(
        string documentId,
        long afterSequence,
        CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
        {
            return await _transport.GetOperationBatchesAsync(documentId, afterSequence, cancellationToken).ConfigureAwait(false);
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await _hub!.InvokeAsync<IReadOnlyList<DocumentCollaborationOperationBatch>>(
            HubMethods.GetOperationBatches,
            documentId,
            afterSequence,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task BroadcastCursorAsync(DocumentCollaborationCursor cursor, CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
        {
            await _transport.BroadcastCursorAsync(cursor, cancellationToken).ConfigureAwait(false);
            return;
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _hub!.InvokeAsync(HubMethods.BroadcastCursor, cursor, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentCollaborationCursor>> GetCursorsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
        {
            return await _transport.GetCursorsAsync(documentId, cancellationToken).ConfigureAwait(false);
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await _hub!.InvokeAsync<IReadOnlyList<DocumentCollaborationCursor>>(
            HubMethods.GetCursors,
            documentId,
            cancellationToken).ConfigureAwait(false);
    }

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

    /// <inheritdoc />
    public async Task ReceiveRemoteCursorAsync(
        DocumentCollaborationCursor cursor,
        CancellationToken cancellationToken = default)
    {
        var handler = RemoteCursorReceived;
        if (handler is null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList().Cast<Func<DocumentCollaborationCursor, CancellationToken, Task>>())
        {
            await subscriber(cursor, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_hub is null || _started)
        {
            return;
        }

        _started = true;
        await _hub.StartAsync(cancellationToken).ConfigureAwait(false);
    }
}
