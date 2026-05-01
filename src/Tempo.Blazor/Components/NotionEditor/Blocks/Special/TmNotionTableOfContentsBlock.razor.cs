using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Special;

public partial class TmNotionTableOfContentsBlock : ComponentBase
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascade ───────────────────────────────────────────────────────────────

    /// <summary>All page blocks, provided by TmNotionPage via CascadingValue.</summary>
    [CascadingParameter(Name = "TmPageBlocks")]
    private IReadOnlyList<IPageBlock>? AllPageBlocks { get; set; }

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public ITableOfContentsBlockContent? Content   { get; set; }
    [Parameter] public bool                          ReadOnly  { get; set; }
    [Parameter] public EventCallback                 OnFocused { get; set; }

    // ── Internal model ────────────────────────────────────────────────────────

    private record HeadingEntry(Guid BlockId, int Level, string PlainText);

    // ── State ────────────────────────────────────────────────────────────────

    private List<HeadingEntry>        _entries    = [];
    private IReadOnlyList<IPageBlock>? _lastBlocks;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(AllPageBlocks, _lastBlocks)) return;
        _lastBlocks = AllPageBlocks;
        RebuildEntries();
    }

    // ── Entry building ────────────────────────────────────────────────────────

    private void RebuildEntries()
    {
        var maxLevel = Content?.MaxLevel ?? 3;

        _entries = (AllPageBlocks ?? [])
            .Where(b => b.Type is BlockType.Heading1 or BlockType.Heading2 or BlockType.Heading3)
            .Select(b => new HeadingEntry(b.Id, HeadingLevel(b.Type), StripHtml((b.Content as ITextBlockContent)?.Html)))
            .Where(e => e.Level <= maxLevel && !string.IsNullOrWhiteSpace(e.PlainText))
            .ToList();
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    private async Task HandleItemClickAsync(Guid blockId)
    {
        await OnFocused.InvokeAsync();
        try
        {
            await JS.InvokeVoidAsync("tmNotionEditor.scrollToBlock", blockId.ToString());
        }
        catch { }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int HeadingLevel(BlockType type) => type switch
    {
        BlockType.Heading1 => 1,
        BlockType.Heading2 => 2,
        BlockType.Heading3 => 3,
        _                  => 0
    };

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        return Regex.Replace(html, "<[^>]+>", string.Empty)
                    .Replace("&nbsp;", " ")
                    .Replace("&amp;", "&")
                    .Replace("&lt;", "<")
                    .Replace("&gt;", ">")
                    .Replace("&quot;", "\"")
                    .Trim();
    }
}
