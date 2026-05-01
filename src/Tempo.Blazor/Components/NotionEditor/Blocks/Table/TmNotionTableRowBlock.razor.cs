using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Table;

public partial class TmNotionTableRowBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public IPageBlock Row { get; set; } = default!;

    [Parameter] public int  RowIndex        { get; set; }
    [Parameter] public int  ColumnCount     { get; set; }
    [Parameter] public bool IsHeaderRow     { get; set; }
    [Parameter] public bool HasHeaderColumn { get; set; }
    [Parameter] public bool ReadOnly        { get; set; }
    [Parameter] public bool IsDragging      { get; set; }
    [Parameter] public bool IsDragOver      { get; set; }
    [Parameter] public bool CanDelete       { get; set; }

    [Parameter] public EventCallback<IReadOnlyList<string>> OnCellsChanged          { get; set; }
    [Parameter] public EventCallback                        OnDeleteRequested        { get; set; }
    [Parameter] public EventCallback                        OnFocused               { get; set; }
    [Parameter] public EventCallback                        OnTabFromLastCell        { get; set; }
    [Parameter] public EventCallback                        OnShiftTabFromFirstCell  { get; set; }
    [Parameter] public EventCallback                        OnDragStart             { get; set; }
    [Parameter] public EventCallback                        OnDragOver              { get; set; }
    [Parameter] public EventCallback                        OnDrop                  { get; set; }
    [Parameter] public EventCallback                        OnDragEnd               { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                              _rowRef;
    private ElementReference[]                           _cellRefs     = [];
    private DotNetObjectReference<TmNotionTableRowBlock>? _dotNetRef;
    private List<string>                                 _cells        = [];
    private int                                          _lastColumnCount;
    private bool                                         _initialized;
    private bool                                         _kbInitialized;
    private bool                                         _needsContentUpdate;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        var source = (Row.Content as ITableRowBlockContent)?.Cells
                     ?? (IReadOnlyList<string>)Array.Empty<string>();

        if (!_initialized)
        {
            _initialized     = true;
            _lastColumnCount = ColumnCount;
            _cellRefs        = new ElementReference[ColumnCount];
            _cells           = Enumerable.Range(0, ColumnCount)
                .Select(i => i < source.Count ? source[i] : string.Empty)
                .ToList();
            _needsContentUpdate = true;
        }
        else if (ColumnCount != _lastColumnCount)
        {
            var prev         = _cells;
            _lastColumnCount = ColumnCount;
            _cellRefs        = new ElementReference[ColumnCount];
            _cells           = Enumerable.Range(0, ColumnCount)
                .Select(i => i < source.Count ? source[i] : (i < prev.Count ? prev[i] : string.Empty))
                .ToList();
            _needsContentUpdate = true;
            _kbInitialized      = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ReadOnly) return;

        if (!_kbInitialized)
        {
            _kbInitialized = true;
            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                await JS.InvokeVoidAsync("tmNotionEditor.initTableRowKeyboardHandler",
                    _rowRef, _dotNetRef, ColumnCount);
            }
            catch { }
        }

        if (_needsContentUpdate)
        {
            _needsContentUpdate = false;
            for (var c = 0; c < Math.Min(_cells.Count, _cellRefs.Length); c++)
            {
                try { await JS.InvokeVoidAsync("tmNotionEditor.setHtml", _cellRefs[c], _cells[c]); }
                catch { }
            }
        }
    }

    // ── Cell events ───────────────────────────────────────────────────────────

    private async Task OnCellBlurAsync(int colIndex)
    {
        if (ReadOnly || colIndex >= _cellRefs.Length) return;
        try
        {
            var html = await JS.InvokeAsync<string>("tmNotionEditor.getHtml", _cellRefs[colIndex]);
            if (html == _cells[colIndex]) return;
            _cells[colIndex] = html;
            await OnCellsChanged.InvokeAsync(_cells.AsReadOnly());
        }
        catch { }
    }

    private async Task OnFocusedAsync() => await OnFocused.InvokeAsync();

    // ── JS callbacks for Tab navigation ──────────────────────────────────────

    [JSInvokable]
    public async Task InvokeTabFromLastCell() => await OnTabFromLastCell.InvokeAsync();

    [JSInvokable]
    public async Task InvokeShiftTabFromFirstCell() => await OnShiftTabFromFirstCell.InvokeAsync();

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_kbInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyTableRowKeyboardHandler", _rowRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
