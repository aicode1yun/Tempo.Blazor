using Microsoft.AspNetCore.SignalR;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Hubs;

/// <summary>
/// SignalR hub for real-time Notion editor collaboration.
/// Clients join page groups and receive block-change + cursor-move events from
/// all other members of that group. The hub is stateless: active-user tracking
/// is kept in memory via ConnectedUsers for GetActiveCollaborators calls.
/// </summary>
public class NotionCollaborationHub : Hub
{
    // connectionId → (pageId, cursor)
    private static readonly Dictionary<string, (string PageId, CollaboratorCursor Cursor)> _connected = new();
    private static readonly Lock _lock = new();

    // ── Join / Leave ──────────────────────────────────────────────────────────

    public async Task JoinPage(string pageId, string userId, string displayName, string? avatarUrl)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, PageGroup(pageId));

        var cursor = new CollaboratorCursor(userId, displayName, avatarUrl,
            GetColor(userId), Guid.Empty, 0);

        lock (_lock)
            _connected[Context.ConnectionId] = (pageId, cursor);

        // Notify others that this user joined (cursor at no particular block yet)
        await Clients.OthersInGroup(PageGroup(pageId))
            .SendAsync("CursorMoved", cursor);
    }

    public async Task LeavePage(string pageId, string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, PageGroup(pageId));

        lock (_lock)
            _connected.Remove(Context.ConnectionId);

        // Broadcast a "gone" cursor with empty BlockId so clients can remove it
        var gone = new CollaboratorCursor(userId, string.Empty, null,
            GetColor(userId), Guid.Empty, -1);

        await Clients.OthersInGroup(PageGroup(pageId))
            .SendAsync("CursorMoved", gone);
    }

    // ── Block change broadcast ────────────────────────────────────────────────

    public async Task BroadcastBlockChange(string pageId, string blockId,
        string changeType, string userId)
    {
        // Relay to all other clients in the page group.
        // We send a lightweight DTO (no IPageBlock payload) to avoid interface
        // deserialization issues on the receiver side. Clients re-fetch the block.
        await Clients.OthersInGroup(PageGroup(pageId))
            .SendAsync("BlockChanged", new { blockId, changeType, userId });
    }

    // ── Cursor broadcast ──────────────────────────────────────────────────────

    public async Task BroadcastCursor(string pageId, CollaboratorCursor cursor)
    {
        lock (_lock)
        {
            if (_connected.TryGetValue(Context.ConnectionId, out var entry))
                _connected[Context.ConnectionId] = (entry.PageId, cursor);
        }

        await Clients.OthersInGroup(PageGroup(pageId))
            .SendAsync("CursorMoved", cursor);
    }

    // ── Active collaborators ──────────────────────────────────────────────────

    public Task<IEnumerable<CollaboratorCursor>> GetActiveCollaborators(string pageId)
    {
        lock (_lock)
        {
            var cursors = _connected.Values
                .Where(e => e.PageId == pageId)
                .Select(e => e.Cursor);
            return Task.FromResult(cursors);
        }
    }

    // ── Disconnect cleanup ────────────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? pageId = null;
        CollaboratorCursor? cursor = null;

        lock (_lock)
        {
            if (_connected.TryGetValue(Context.ConnectionId, out var entry))
            {
                pageId = entry.PageId;
                cursor = entry.Cursor;
                _connected.Remove(Context.ConnectionId);
            }
        }

        if (pageId is not null && cursor is not null)
        {
            var gone = cursor with { DisplayName = string.Empty, Offset = -1 };
            await Clients.Group(PageGroup(pageId)).SendAsync("CursorMoved", gone);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string PageGroup(string pageId) => $"page:{pageId}";

    private static readonly string[] _palette =
    [
        "#E03E3E", "#0F9B8E", "#6940A5", "#CF9300",
        "#D9730D", "#2382E2", "#4DA64D", "#AD1A72"
    ];

    private static string GetColor(string userId)
    {
        var idx = Math.Abs(userId.GetHashCode()) % _palette.Length;
        return _palette[idx];
    }
}
