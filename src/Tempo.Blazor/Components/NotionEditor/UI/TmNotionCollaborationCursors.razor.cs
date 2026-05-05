using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.UI;

/// <summary>
/// Renders a pill-bar of active collaborator avatars in the editor topbar and drives
/// per-block cursor highlights via JS DOM manipulation (tmNotionEditor.updateCollabCursors).
/// Subscribes to NotionCollaborationSync.RemoteCursorMoved; no-op when CollaborationProvider absent.
/// </summary>
public partial class TmNotionCollaborationCursors : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    // ── State ─────────────────────────────────────────────────────────────────

    // userId → latest cursor — only remote users (own cursor filtered by sync)
    private readonly Dictionary<string, CollaboratorCursor> _cursors = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        if (Context.CollaborationSync is not { } sync) return;
        if (Context.CollaborationProvider is not { } provider) return;

        sync.RemoteCursorMoved += HandleCursorMoved;

        // Load users already on the page
        try
        {
            var active = await provider.GetActiveCollaboratorsAsync(
                Context.DataProvider is not null ? "" : "");
            foreach (var c in active.Where(c => c.UserId != "demo"))
                _cursors[c.UserId] = c;
        }
        catch { }
    }

    // ── Cursor handling ───────────────────────────────────────────────────────

    private async void HandleCursorMoved(CollaboratorCursor cursor)
    {
        _cursors[cursor.UserId] = cursor;
        await InvokeAsync(async () =>
        {
            StateHasChanged();
            await PushCursorsToJsAsync();
        });
    }

    private async Task PushCursorsToJsAsync()
    {
        try
        {
            var dtos = _cursors.Select(kv => new
            {
                blockId     = kv.Value.BlockId.ToString(),
                displayName = kv.Value.DisplayName,
                color       = GetColor(kv.Key)
            }).ToArray();

            await JS.InvokeVoidAsync("tmNotionEditor.updateCollabCursors", (object)dtos);
        }
        catch { }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "?";
        var parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}"
            : displayName[..1];
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (Context.CollaborationSync is { } sync)
            sync.RemoteCursorMoved -= HandleCursorMoved;

        try
        {
            await JS.InvokeVoidAsync("tmNotionEditor.clearCollabCursors");
        }
        catch { }
    }
}
