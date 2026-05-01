using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Special;

public partial class TmNotionTemplateButtonBlock : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public ITemplateButtonBlockContent? Content { get; set; }

    [Parameter] public bool ReadOnly { get; set; }

    [Parameter] public bool IsFocused { get; set; }

    [Parameter] public EventCallback<IReadOnlyList<IPageBlock>> OnInsertTemplateBlocks { get; set; }

    [Parameter] public EventCallback<TemplateButtonBlockContent> OnUpdated { get; set; }

    [Parameter] public EventCallback OnFocused { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private bool             _isConfigOpen;
    private string           _labelBuffer  = string.Empty;
    private List<BlockType>  _configBlocks = [];
    private int              _addBlockType = (int)BlockType.Paragraph;
    private ElementReference _labelInputRef;
    private bool             _focusPending;

    private static readonly BlockType[] _addableBlockTypes =
    [
        BlockType.Paragraph,
        BlockType.Heading1,
        BlockType.Heading2,
        BlockType.Heading3,
        BlockType.BulletList,
        BlockType.NumberedList,
        BlockType.TodoItem,
        BlockType.Quote,
        BlockType.Callout,
        BlockType.Code,
        BlockType.Toggle,
        BlockType.Divider,
        BlockType.Table,
    ];

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _labelInputId => $"tbl-label-{_instanceId}";

    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    private string _label => string.IsNullOrWhiteSpace(Content?.Label)
        ? Loc["TmNotionTemplateButtonBlock_DefaultLabel"]
        : Content.Label;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusPending)
        {
            _focusPending = false;
            try { await _labelInputRef.FocusAsync(); } catch { }
        }
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private async Task HandleInsertClickAsync()
    {
        if (Content?.TemplateBlocks.Count == 0) return;
        await OnInsertTemplateBlocks.InvokeAsync(Content!.TemplateBlocks);
    }

    private Task ToggleConfigAsync()
    {
        if (_isConfigOpen) return CloseConfigAsync();
        return OpenConfigAsync();
    }

    private Task OpenConfigAsync()
    {
        _labelBuffer  = Content?.Label ?? string.Empty;
        _configBlocks = Content?.TemplateBlocks.Select(b => b.Type).ToList() ?? [];
        _addBlockType = (int)BlockType.Paragraph;
        _isConfigOpen = true;
        _focusPending = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task CloseConfigAsync()
    {
        _isConfigOpen = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task SaveConfigAsync()
    {
        var templateBlocks = _configBlocks
            .Select((type, i) => (IPageBlock)new PageBlock
            {
                Id      = Guid.NewGuid(),
                Type    = type,
                Order   = i,
                Content = CreateDefaultContent(type)
            })
            .ToList();

        var updated = new TemplateButtonBlockContent
        {
            Label          = _labelBuffer.Trim(),
            TemplateBlocks = templateBlocks
        };

        _isConfigOpen = false;
        await OnUpdated.InvokeAsync(updated);
    }

    private void RemoveConfigBlock(int idx)
    {
        if (idx >= 0 && idx < _configBlocks.Count)
            _configBlocks.RemoveAt(idx);
    }

    private void AddConfigBlock()
    {
        var type = (BlockType)_addBlockType;
        _configBlocks.Add(type);
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && _isConfigOpen)
            await CloseConfigAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MarkupString BlockTypeIcon(BlockType type) => type switch
    {
        BlockType.Heading1 or BlockType.Heading2 or BlockType.Heading3
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><path d=\"M4 6h16M4 12h10M4 18h7\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"/></svg>",
        BlockType.BulletList
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><circle cx=\"5\" cy=\"6\" r=\"1.5\" fill=\"currentColor\"/><path d=\"M9 6h11\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\"/><circle cx=\"5\" cy=\"12\" r=\"1.5\" fill=\"currentColor\"/><path d=\"M9 12h11\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\"/><circle cx=\"5\" cy=\"18\" r=\"1.5\" fill=\"currentColor\"/><path d=\"M9 18h11\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\"/></svg>",
        BlockType.NumberedList
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><path d=\"M10 6h11M10 12h11M10 18h11\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\"/><text x=\"3\" y=\"8\" font-size=\"7\" fill=\"currentColor\">1</text><text x=\"3\" y=\"14\" font-size=\"7\" fill=\"currentColor\">2</text><text x=\"3\" y=\"20\" font-size=\"7\" fill=\"currentColor\">3</text></svg>",
        BlockType.TodoItem
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"3\" stroke=\"currentColor\" stroke-width=\"1.5\"/><path d=\"M8 12l3 3 5-6\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>",
        BlockType.Quote
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><path d=\"M3 6h18M3 12h18M3 18h14\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\"/><rect x=\"2\" y=\"4\" width=\"3\" height=\"16\" rx=\"1\" fill=\"currentColor\"/></svg>",
        BlockType.Callout
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><path d=\"M12 2a10 10 0 1 0 0 20A10 10 0 0 0 12 2Z\" stroke=\"currentColor\" stroke-width=\"1.5\"/><path d=\"M12 8v4M12 16h.01\" stroke=\"currentColor\" stroke-width=\"1.75\" stroke-linecap=\"round\"/></svg>",
        BlockType.Code
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><path d=\"M8 8 3 12l5 4M16 8l5 4-5 4\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>",
        BlockType.Toggle
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>",
        BlockType.Divider
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><path d=\"M3 12h18\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"/></svg>",
        BlockType.Table
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"2\" stroke=\"currentColor\" stroke-width=\"1.5\"/><path d=\"M3 9h18M3 15h18M9 3v18\" stroke=\"currentColor\" stroke-width=\"1.5\"/></svg>",
        _
            => (MarkupString)"<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" aria-hidden=\"true\"><path d=\"M4 6h16M4 10h16M4 14h10\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\"/></svg>",
    };

    private static IBlockContent CreateDefaultContent(BlockType type) => type switch
    {
        BlockType.Heading1 or BlockType.Heading2 or BlockType.Heading3
            => new HeadingBlockContent { Level = type switch { BlockType.Heading1 => 1, BlockType.Heading2 => 2, _ => 3 } },
        BlockType.Quote        => new TextBlockContent(),
        BlockType.Callout      => new CalloutBlockContent { IconEmoji = "💡" },
        BlockType.Code         => new CodeBlockContent(),
        BlockType.BulletList   => new ListBlockContent(),
        BlockType.NumberedList => new ListBlockContent(),
        BlockType.TodoItem     => new TodoBlockContent(),
        BlockType.Toggle       => new ToggleBlockContent(),
        BlockType.Divider      => new DividerBlockContent(),
        _                      => new TextBlockContent()
    };
}
