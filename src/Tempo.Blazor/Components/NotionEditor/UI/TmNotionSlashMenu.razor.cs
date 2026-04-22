using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public partial class TmNotionSlashMenu : ComponentBase
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public bool   Visible { get; set; }
    [Parameter] public double Top     { get; set; }
    [Parameter] public double Left    { get; set; }

    /// <summary>Raised when the user selects a block type.</summary>
    [Parameter] public EventCallback<BlockType> OnItemSelected { get; set; }

    /// <summary>Raised when the user dismisses the menu (Escape / backdrop click).</summary>
    [Parameter] public EventCallback OnClosed { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private string   _query         = string.Empty;
    private int      _selectedIndex;
    private double   _top;
    private double   _left;
    private bool     _wasVisible;
    private bool     _needsFocus;

    private List<BlockType> _recentlyUsed = [];
    private List<(SlashMenuCategory Category, List<SlashMenuItem> Items)> _groups = [];
    private int _totalItems;

    private ElementReference _menuRef;
    private ElementReference _inputRef;
    private ElementReference _listRef;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnParametersSetAsync()
    {
        if (Visible && !_wasVisible)
        {
            // Menu just opened — reset state
            _query         = string.Empty;
            _selectedIndex = 0;
            _top           = Top;
            _left          = Left;
            _needsFocus    = true;

            await LoadRecentAsync();
            RebuildGroups();
        }
        else if (!Visible && _wasVisible)
        {
            _query  = string.Empty;
            _groups = [];
        }

        _wasVisible = Visible;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsFocus && Visible)
        {
            _needsFocus = false;
            try
            {
                await _inputRef.FocusAsync();
                await JS.InvokeVoidAsync("tmNotionEditor.adjustSlashMenuPosition", _menuRef);
            }
            catch { /* SSR / test */ }
        }
    }

    // ── Query handling ────────────────────────────────────────────────────────

    private async Task HandleQueryInputAsync(ChangeEventArgs e)
    {
        _query         = e.Value?.ToString() ?? string.Empty;
        _selectedIndex = 0;
        RebuildGroups();
        await ScrollToSelectedAsync();
    }

    private void RebuildGroups()
    {
        _groups     = SlashMenuRegistry.GetGrouped(_query, _recentlyUsed, ResolveName, ResolveDesc);
        _totalItems = _groups.Sum(g => g.Items.Count);
    }

    private string ResolveName(SlashMenuItem item)        => Loc[item.Name];
    private string ResolveDesc(SlashMenuItem item)        => Loc[item.Description];

    // ── Keyboard navigation ───────────────────────────────────────────────────

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowDown":
                if (_totalItems > 0)
                    _selectedIndex = (_selectedIndex + 1) % _totalItems;
                await ScrollToSelectedAsync();
                break;

            case "ArrowUp":
                if (_totalItems > 0)
                    _selectedIndex = (_selectedIndex - 1 + _totalItems) % _totalItems;
                await ScrollToSelectedAsync();
                break;

            case "Enter":
                var item = GetFlatItem(_selectedIndex);
                if (item is not null) await SelectItemAsync(item);
                break;

            case "Escape":
                await HandleBackdropClickAsync();
                break;
        }
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    private async Task SelectItemAsync(SlashMenuItem item)
    {
        await SaveRecentAsync(item.Type);
        await JS.InvokeVoidAsync("tmNotionEditor.clearSlashQuery");
        await OnItemSelected.InvokeAsync(item.Type);
    }

    // ── Close ─────────────────────────────────────────────────────────────────

    private async Task HandleBackdropClickAsync()
    {
        try { await JS.InvokeVoidAsync("tmNotionEditor.refocusSlashElement"); }
        catch { /* SSR / test */ }
        await OnClosed.InvokeAsync();
    }

    // ── Recently used (localStorage) ─────────────────────────────────────────

    private async Task LoadRecentAsync()
    {
        try
        {
            var raw = await JS.InvokeAsync<int[]>("tmNotionEditor.getRecentSlashItems");
            _recentlyUsed = raw
                .Select(i => (BlockType)i)
                .Where(t => SlashMenuRegistry.FindByType(t) is not null)
                .Distinct()
                .ToList();
        }
        catch
        {
            _recentlyUsed = [];
        }
    }

    private async Task SaveRecentAsync(BlockType type)
    {
        try { await JS.InvokeVoidAsync("tmNotionEditor.addRecentSlashItem", (int)type); }
        catch { }
    }

    // ── Category label mapping ────────────────────────────────────────────────

    private string CategoryLabel(SlashMenuCategory cat) => cat switch
    {
        SlashMenuCategory.Recent   => Loc["TmNotionSlashMenu_CategoryRecent"],
        SlashMenuCategory.Basic    => Loc["TmNotionSlashMenu_CategoryBasic"],
        SlashMenuCategory.Media    => Loc["TmNotionSlashMenu_CategoryMedia"],
        SlashMenuCategory.Embeds   => Loc["TmNotionSlashMenu_CategoryEmbeds"],
        SlashMenuCategory.Page     => Loc["TmNotionSlashMenu_CategoryPage"],
        SlashMenuCategory.Advanced => Loc["TmNotionSlashMenu_CategoryAdvanced"],
        _                          => cat.ToString()
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SlashMenuItem? GetFlatItem(int flatIndex)
    {
        var idx = 0;
        foreach (var (_, items) in _groups)
        {
            foreach (var item in items)
            {
                if (idx == flatIndex) return item;
                idx++;
            }
        }
        return null;
    }

    private async Task ScrollToSelectedAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("tmNotionEditor.scrollSlashItemIntoView", _listRef, _selectedIndex);
        }
        catch { }
    }
}
