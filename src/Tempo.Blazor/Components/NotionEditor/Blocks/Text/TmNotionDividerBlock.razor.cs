using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Text;

/// <summary>
/// Divider block — a focusable horizontal rule.
/// Enter adds a Paragraph below; Backspace/Delete removes the block.
/// </summary>
public partial class TmNotionDividerBlock : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public bool ReadOnly { get; set; }

    [Parameter] public bool IsFocused { get; set; }

    /// <summary>Fired when the block receives focus (click or Tab).</summary>
    [Parameter] public EventCallback OnFocused { get; set; }

    /// <summary>Fired when Enter is pressed — parent should add a Paragraph block below.</summary>
    [Parameter] public EventCallback OnAddAfter { get; set; }

    /// <summary>Fired when Backspace or Delete is pressed — parent should remove the block.</summary>
    [Parameter] public EventCallback OnDeleteRequested { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private bool _isFocused;

    // ── Handlers ─────────────────────────────────────────────────────────────

    private async Task HandleClickAsync()
    {
        if (ReadOnly) return;
        _isFocused = true;
        await OnFocused.InvokeAsync();
    }

    private async Task HandleFocusAsync()
    {
        _isFocused = true;
        await OnFocused.InvokeAsync();
    }

    private void HandleBlurAsync() => _isFocused = false;

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (ReadOnly) return;

        switch (e.Key)
        {
            case "Enter":
                await OnAddAfter.InvokeAsync();
                break;

            case "Backspace":
            case "Delete":
                await OnDeleteRequested.InvokeAsync();
                break;
        }
    }
}
