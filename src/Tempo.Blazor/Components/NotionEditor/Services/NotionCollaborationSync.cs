using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Bridges INotionCollaborationProvider observables to simple .NET events consumed by
/// TmNotionCollaborationCursors and TmNotionPage. Owns join/leave lifecycle per page.
/// Last-write-wins conflict detection: remote changes are always applied; the local
/// block store is the source of truth for the current user's own edits.
/// </summary>
public sealed class NotionCollaborationSync : IAsyncDisposable
{
    private INotionCollaborationProvider? _provider;
    private IDisposable? _blockSub;
    private IDisposable? _cursorSub;
    private string? _pageId;
    private string? _userId;

    /// <summary>Fired (on the observer thread) when a remote collaborator changes a block.</summary>
    public event Action<BlockChange>?        RemoteBlockChanged;

    /// <summary>Fired (on the observer thread) when a remote collaborator's cursor moves.</summary>
    public event Action<CollaboratorCursor>? RemoteCursorMoved;

    /// <summary>Joins a page, subscribing to both streams. Automatically leaves any previous page.</summary>
    public async Task JoinAsync(INotionCollaborationProvider provider, string pageId, string userId)
    {
        await DetachAsync();

        _provider = provider;
        _pageId   = pageId;
        _userId   = userId;

        _blockSub = provider.OnBlockChanged.Subscribe(new SyncObserver<BlockChange>(change =>
        {
            if (change.UserId != userId)
                RemoteBlockChanged?.Invoke(change);
        }));

        _cursorSub = provider.OnCursorMoved.Subscribe(new SyncObserver<CollaboratorCursor>(cursor =>
        {
            if (cursor.UserId != userId)
                RemoteCursorMoved?.Invoke(cursor);
        }));

        await provider.JoinPageAsync(pageId, userId);
    }

    /// <summary>Broadcasts a local block change to all other collaborators.</summary>
    public async Task BroadcastBlockChangeAsync(BlockChange change)
    {
        if (_provider is not null)
            await _provider.BroadcastBlockChangeAsync(change);
    }

    /// <summary>Broadcasts the local cursor position to all other collaborators.</summary>
    public async Task BroadcastCursorAsync(CollaboratorCursor cursor)
    {
        if (_provider is not null)
            await _provider.BroadcastCursorAsync(cursor);
    }

    private async Task DetachAsync()
    {
        _blockSub?.Dispose();
        _cursorSub?.Dispose();
        _blockSub  = null;
        _cursorSub = null;

        if (_provider is not null && _pageId is not null && _userId is not null)
        {
            try { await _provider.LeavePageAsync(_pageId, _userId); } catch { }
        }

        _provider = null;
        _pageId   = null;
        _userId   = null;
    }

    public async ValueTask DisposeAsync() => await DetachAsync();
}

file sealed class SyncObserver<T>(Action<T> onNext) : IObserver<T>
{
    public void OnCompleted() { }
    public void OnError(Exception _) { }
    public void OnNext(T value)
    {
        try { onNext(value); } catch { }
    }
}
