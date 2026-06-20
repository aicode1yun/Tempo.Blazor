using Microsoft.AspNetCore.SignalR.Client;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// INotionCollaborationProvider backed by a SignalR hub at /hubs/notion-collaboration.
/// The hub URL is derived from IHttpClientFactory's "DemoApi" named client base address.
/// Connection is established lazily on the first JoinPageAsync call and kept alive.
/// Disconnect / reconnect is handled automatically by HubConnection.
/// </summary>
public sealed class SignalRCollaborationProvider : INotionCollaborationProvider, IAsyncDisposable
{
    private readonly HubConnection _hub;
    private readonly SimpleSubject<BlockChange>        _blockChanges = new();
    private readonly SimpleSubject<CollaboratorCursor> _cursorMoves  = new();
    private bool    _started;
    private string? _currentPageId;

    public IObservable<BlockChange>        OnBlockChanged { get; }
    public IObservable<CollaboratorCursor> OnCursorMoved  { get; }

    public SignalRCollaborationProvider(IHttpClientFactory factory)
    {
        var base64 = factory.CreateClient("DemoApi").BaseAddress!.ToString().TrimEnd('/');
        var hubUrl = $"{base64}/hubs/notion-collaboration";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Wire hub → subjects
        _hub.On<CollaboratorCursor>("CursorMoved", cursor =>
        {
            // Offset == -1 signals "user left" — we still forward so UI can remove the cursor
            _cursorMoves.OnNext(cursor);
        });

        _hub.On<BlockChangeDto>("BlockChanged", dto =>
        {
            if (!Enum.TryParse<BlockChangeType>(dto.ChangeType, out var ct))
                ct = BlockChangeType.Updated;
            _blockChanges.OnNext(new BlockChange(Guid.Parse(dto.BlockId), ct, null, dto.UserId));
        });

        OnBlockChanged = _blockChanges;
        OnCursorMoved  = _cursorMoves;
    }

    // ── INotionCollaborationProvider ──────────────────────────────────────────

    public async Task JoinPageAsync(string pageId, string userId)
    {
        await EnsureConnectedAsync();
        _currentPageId = pageId;
        await _hub.InvokeAsync("JoinPage", pageId, userId, "Demo User", (string?)null);
    }

    public async Task LeavePageAsync(string pageId, string userId)
    {
        if (_hub.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("LeavePage", pageId, userId);
    }

    public async Task BroadcastBlockChangeAsync(BlockChange change)
    {
        if (_hub.State != HubConnectionState.Connected || _currentPageId is null) return;
        await _hub.InvokeAsync("BroadcastBlockChange",
            _currentPageId,
            change.BlockId.ToString("D"),
            change.ChangeType.ToString(),
            change.UserId);
    }

    public async Task BroadcastCursorAsync(CollaboratorCursor cursor)
    {
        if (_hub.State != HubConnectionState.Connected || _currentPageId is null) return;
        await _hub.InvokeAsync("BroadcastCursor", _currentPageId, cursor);
    }

    public async Task<IEnumerable<CollaboratorCursor>> GetActiveCollaboratorsAsync(string pageId)
    {
        if (_hub.State != HubConnectionState.Connected) return [];
        try
        {
            return await _hub.InvokeAsync<IEnumerable<CollaboratorCursor>>(
                "GetActiveCollaborators", pageId) ?? [];
        }
        catch
        {
            return [];
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task EnsureConnectedAsync()
    {
        if (_started) return;
        _started = true;
        await _hub.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _blockChanges.OnCompleted();
        _cursorMoves.OnCompleted();
        await _hub.DisposeAsync();
    }

    private sealed record BlockChangeDto(string BlockId, string ChangeType, string UserId);
}
