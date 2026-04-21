using Microsoft.AspNetCore.Components;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Blocks;

/// <summary>
/// Left-side block handle: add-below button, drag grip, and options menu trigger.
/// Positioned absolutely to the left of the parent block.
/// </summary>
public partial class TmNotionBlockHandle : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public IPageBlock Block { get; set; } = default!;

    [Parameter] public EventCallback          OnAddClicked         { get; set; }
    [Parameter] public EventCallback          OnDelete             { get; set; }
    [Parameter] public EventCallback          OnDuplicate          { get; set; }
    [Parameter] public EventCallback<BlockType> OnTurnInto         { get; set; }
    [Parameter] public EventCallback          OnMoveTo             { get; set; }
    [Parameter] public EventCallback          OnCopyLink           { get; set; }
    [Parameter] public EventCallback          OnComment            { get; set; }
    [Parameter] public EventCallback<string?> OnTextColorChange    { get; set; }
    [Parameter] public EventCallback<string?> OnBackgroundChange   { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private bool _showMenu;

    // ── Handlers ─────────────────────────────────────────────────────────────

    private async Task HandleAddClickedAsync() => await OnAddClicked.InvokeAsync();

    private void ToggleMenu() => _showMenu = !_showMenu;

    private void CloseMenu() => _showMenu = false;
}
