using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Page;

public partial class TmNotionLinkedPageBlock : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public ILinkedPageBlockContent? Content    { get; set; }
    [Parameter] public bool                     ReadOnly   { get; set; }
    [Parameter] public EventCallback            OnNavigate { get; set; }
    [Parameter] public EventCallback            OnFocused  { get; set; }

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _icon  => Content?.IconEmoji ?? string.Empty;
    private string _title => string.IsNullOrEmpty(Content?.Title) ? Loc["TmNotionEditor_Untitled"] : Content.Title;

    // ── Interactions ──────────────────────────────────────────────────────────

    private async Task HandleClickAsync()
    {
        await OnFocused.InvokeAsync();
        await OnNavigate.InvokeAsync();
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await HandleClickAsync();
    }
}
