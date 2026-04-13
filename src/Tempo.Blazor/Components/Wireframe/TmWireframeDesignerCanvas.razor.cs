using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Wireframe.Commands;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// SVG-based wireframe canvas. Renders <see cref="WireframeDocument"/> elements and
/// communicates with <c>wireframe-designer.js</c> for pan, zoom, drag, resize, and
/// multi-select interactions.
///
/// All mutating operations go through the cascaded <see cref="WireframeCommandStack"/>
/// so that Undo/Redo works automatically. The stack is optional: when null (e.g. used
/// standalone without <c>TmWireframeEditor</c>), mutations are applied directly.
/// </summary>
public partial class TmWireframeDesignerCanvas : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private WireframeComponentRegistry _registry { get; set; } = default!;

    // ── Cascaded command stack (provided by TmWireframeEditor) ───────────────

    /// <summary>
    /// Cascaded from <c>TmWireframeEditor</c>. When present all mutations are routed
    /// through it; when absent mutations are applied directly (standalone usage).
    /// </summary>
    [CascadingParameter] public WireframeCommandStack? CommandStack { get; set; }

    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>Document to display and edit. Mutated in-place; bind with @bind-Document.</summary>
    [Parameter] public WireframeDocument? Document { get; set; }

    /// <summary>Raised after every mutation so callers can persist the document.</summary>
    [Parameter] public EventCallback<WireframeDocument> DocumentChanged { get; set; }

    /// <summary>Show grid lines on the canvas background.</summary>
    [Parameter] public bool ShowGrid { get; set; } = true;

    /// <summary>Snap-to-grid cell size in SVG units (pixels). 0 = disabled.</summary>
    [Parameter] public int GridSize { get; set; } = 8;

    /// <summary>Prevent all editing interactions (drag, resize, drop).</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Additional CSS class applied to the canvas wrapper.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Raised when the user changes the selection (single or multi).</summary>
    [Parameter] public EventCallback<string[]> OnSelectionChanged { get; set; }

    /// <summary>Raised when user requests undo (Ctrl+Z). Parent handles the command stack.</summary>
    [Parameter] public EventCallback OnUndo { get; set; }

    /// <summary>Raised when user requests redo (Ctrl+Y / Ctrl+Shift+Z).</summary>
    [Parameter] public EventCallback OnRedo { get; set; }

    /// <summary>Raised when the user switches tool mode via H/V keyboard shortcut.</summary>
    [Parameter] public EventCallback<string> OnToolModeChanged { get; set; }

    // ── Internal state ───────────────────────────────────────────────────────

    private ElementReference _svgRef;
    private ElementReference _wrapRef;
    private DotNetObjectReference<TmWireframeDesignerCanvas>? _dotNetRef;
    private bool _jsInitialized;

    // Stable IDs for SVG defs so multiple canvas instances on the same page don't collide
    private readonly string _svgId       = "tm-wd-" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _gridSmallId;
    private readonly string _gridLargeId;

    // ViewBox tracks pan/zoom state driven by JS; not re-rendered by Blazor
    private string _viewBox = "0 0 1200 800";

    // Grid dimensions as strings for Razor binding
    private string _gs = "8";
    private string _gl = "80";

    // Snapshot of element positions at drag-start (for MoveElementsCommand coalescing)
    private Dictionary<string, (double X, double Y)>? _dragStartPositions;

    // Current JS selection – kept in sync so we can re-apply handles after Blazor re-renders
    private string[] _currentSelectionIds = [];

    public TmWireframeDesignerCanvas()
    {
        _gridSmallId = _svgId + "-gs";
        _gridLargeId = _svgId + "-gl";
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        _gs = GridSize.ToString(CultureInfo.InvariantCulture);
        _gl = (GridSize * 10).ToString(CultureInfo.InvariantCulture);

        if (Document is not null)
        {
            _viewBox = $"0 0 {Document.Width.ToString("0.##", CultureInfo.InvariantCulture)} " +
                       $"{Document.Height.ToString("0.##", CultureInfo.InvariantCulture)}";
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_jsInitialized)
        {
            _jsInitialized = true;
            _dotNetRef = DotNetObjectReference.Create(this);

            var options = new
            {
                readOnly     = ReadOnly,
                gridSize     = GridSize,
                showGrid     = ShowGrid,
                canvasWidth  = Document?.Width  ?? 1200,
                canvasHeight = Document?.Height ?? 800,
            };

            await JS.InvokeVoidAsync("tmWireframeDesigner.init", _svgRef, _dotNetRef, options);
        }
        else if (_jsInitialized && _currentSelectionIds.Length > 0)
        {
            // After Blazor re-renders the SVG (e.g. element resized via Properties Panel),
            // the JS selection handles read data-w/data-h from the DOM. Re-apply the
            // selection so handles snap to the updated element dimensions.
            await JS.InvokeVoidAsync("tmWireframeDesigner.setSelection", _svgRef, _currentSelectionIds);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsInitialized)
        {
            try { await JS.InvokeVoidAsync("tmWireframeDesigner.destroy", _svgRef); }
            catch { /* ignore JS errors during disposal */ }
        }
        _dotNetRef?.Dispose();
    }

    // ── JS → C# callbacks ───────────────────────────────────────────────────

    /// <summary>
    /// Called by JS at mousedown on an element to snapshot positions before drag begins.
    /// Needed so <see cref="MoveElementsCommand"/> can record the "before" state.
    /// </summary>
    [JSInvokable]
    public void OnDragStarted(string[] ids)
    {
        if (Document is null) return;
        _dragStartPositions = [];
        foreach (var id in ids)
        {
            var el = Document.Elements.FirstOrDefault(e => e.Id == id);
            if (el is not null)
                _dragStartPositions[id] = (el.X, el.Y);
        }
    }

    /// <summary>Single element moved by dragging.</summary>
    [JSInvokable]
    public async Task OnElementMoved(string id, double x, double y)
    {
        if (Document is null || ReadOnly) return;

        var before = _dragStartPositions is not null && _dragStartPositions.TryGetValue(id, out var bp)
            ? new Dictionary<string, (double X, double Y)> { [id] = bp }
            : null;
        var after  = new Dictionary<string, (double X, double Y)> { [id] = (x, y) };

        ExecuteMove(before, after);
        await NotifyAndRender();
    }

    /// <summary>Multiple elements moved together (group drag).</summary>
    [JSInvokable]
    public async Task OnElementsMoved(ElementMove[] moves)
    {
        if (Document is null || ReadOnly) return;

        var before = _dragStartPositions;
        var after  = moves.ToDictionary(m => m.Id, m => (m.X, m.Y));

        ExecuteMove(before, after);
        await NotifyAndRender();
    }

    private void ExecuteMove(
        Dictionary<string, (double X, double Y)>? before,
        Dictionary<string, (double X, double Y)> after)
    {
        if (Document is null) return;

        if (CommandStack is not null)
        {
            // Build "before" from current document state when no snapshot exists
            var beforeSnapshot = before ?? after.Keys.ToDictionary(
                id => id,
                id => Document.Elements.FirstOrDefault(e => e.Id == id) is { } el
                    ? (el.X, el.Y) : (0.0, 0.0));

            CommandStack.Push(new MoveElementsCommand(Document, beforeSnapshot, after));
        }
        else
        {
            // Standalone mode: apply directly
            foreach (var el in Document.Elements)
            {
                if (after.TryGetValue(el.Id, out var pos))
                { el.X = pos.X; el.Y = pos.Y; }
            }
        }
    }

    /// <summary>Element resized via a handle.</summary>
    [JSInvokable]
    public async Task OnElementResized(string id, double x, double y, double w, double h)
    {
        if (Document is null || ReadOnly) return;
        var el = Document.Elements.FirstOrDefault(e => e.Id == id);
        if (el is null) return;

        if (CommandStack is not null)
        {
            CommandStack.Push(new ResizeElementCommand(
                Document, id,
                el.X, el.Y, el.W, el.H,
                x, y, w, h));
        }
        else
        {
            el.X = x; el.Y = y; el.W = w; el.H = h;
        }

        await NotifyAndRender();
    }

    /// <summary>Component dropped from toolbox onto the canvas.</summary>
    [JSInvokable]
    public async Task OnElementDropped(string type, double x, double y)
    {
        if (Document is null || ReadOnly) return;
        var def = _registry.GetDef(type);
        var el  = WireframeDocumentExtensions.NewElement(
            type, x, y,
            def?.DefaultWidth  ?? 160,
            def?.DefaultHeight ?? 40);
        el.ZIndex = Document.Elements.Count > 0
            ? Document.Elements.Max(e => e.ZIndex) + 1
            : 0;

        if (CommandStack is not null)
            CommandStack.Push(new AddElementCommand(Document, el));
        else
            Document.Elements.Add(el);

        await NotifyAndRender();
    }

    /// <summary>Rubber-band selection completed.</summary>
    [JSInvokable]
    public async Task OnMultiSelect(string[] ids)
    {
        _currentSelectionIds = ids;
        await OnSelectionChanged.InvokeAsync(ids);
    }

    /// <summary>Selection changed (single click or shift+click).</summary>
    [JSInvokable("OnSelectionChanged")]
    public async Task JsOnSelectionChanged(string[] ids)
    {
        _currentSelectionIds = ids;
        await OnSelectionChanged.InvokeAsync(ids);
    }

    /// <summary>Delete key pressed – remove the selected elements.</summary>
    [JSInvokable]
    public async Task OnDeleteSelected(string[] ids)
    {
        if (Document is null || ReadOnly) return;

        if (CommandStack is not null)
            CommandStack.Push(new RemoveElementsCommand(Document, ids));
        else
            Document.Elements.RemoveAll(e => ids.Contains(e.Id));

        await NotifyAndRender();
    }

    /// <summary>Select-all (Ctrl+A).</summary>
    [JSInvokable]
    public async Task OnSelectAll()
    {
        if (Document is null) return;
        var ids = Document.Elements.Select(e => e.Id).ToArray();
        _currentSelectionIds = ids;
        await JS.InvokeVoidAsync("tmWireframeDesigner.setSelection", _svgRef, ids);
        await OnSelectionChanged.InvokeAsync(ids);
    }

    /// <summary>Clear selection (Escape).</summary>
    [JSInvokable]
    public async Task OnClearSelection()
    {
        _currentSelectionIds = [];
        await OnSelectionChanged.InvokeAsync([]);
    }

    /// <summary>Undo requested by keyboard shortcut.</summary>
    [JSInvokable("OnUndo")]
    public async Task JsOnUndo()
    {
        if (CommandStack is not null)
        {
            CommandStack.Undo();
            await NotifyAndRender();
        }
        else
        {
            await OnUndo.InvokeAsync();
        }
    }

    /// <summary>Redo requested by keyboard shortcut.</summary>
    [JSInvokable("OnRedo")]
    public async Task JsOnRedo()
    {
        if (CommandStack is not null)
        {
            CommandStack.Redo();
            await NotifyAndRender();
        }
        else
        {
            await OnRedo.InvokeAsync();
        }
    }

    /// <summary>Duplicate selected elements (Ctrl+D).</summary>
    [JSInvokable]
    public async Task OnDuplicate(string[] ids)
    {
        if (Document is null || ReadOnly) return;
        var maxZ   = Document.Elements.Count > 0 ? Document.Elements.Max(e => e.ZIndex) : 0;
        var offset = GridSize > 0 ? GridSize * 2 : 16;
        var added  = new List<string>();

        foreach (var id in ids)
        {
            var src = Document.Elements.FirstOrDefault(e => e.Id == id);
            if (src is null) continue;
            var copy = src.DeepCopy();
            copy.X += offset; copy.Y += offset;
            copy.ZIndex = ++maxZ;

            if (CommandStack is not null)
                CommandStack.Push(new AddElementCommand(Document, copy));
            else
                Document.Elements.Add(copy);

            added.Add(copy.Id);
        }

        if (added.Count == 0) return;
        await NotifyAndRender();
        await JS.InvokeVoidAsync("tmWireframeDesigner.setSelection", _svgRef, added.ToArray());
        await OnSelectionChanged.InvokeAsync(added.ToArray());
    }

    /// <summary>Zoom level changed by wheel.</summary>
    [JSInvokable]
    public Task OnZoomChanged(double scale) => Task.CompletedTask;

    /// <summary>ViewBox updated after pan.</summary>
    [JSInvokable]
    public void OnViewBoxChanged(double x, double y, double w, double h)
    {
        // Kept in JS; no Blazor re-render needed
        _viewBox = $"{F(x)} {F(y)} {F(w)} {F(h)}";
    }

    /// <summary>Context menu on element.</summary>
    [JSInvokable]
    public Task OnElementContextMenu(string id, double offsetX, double offsetY)
        => Task.CompletedTask;

    /// <summary>Context menu on empty canvas.</summary>
    [JSInvokable]
    public Task OnCanvasContextMenu(double svgX, double svgY, double offsetX, double offsetY)
        => Task.CompletedTask;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Programmatically zoom to a specific scale level.</summary>
    public async Task SetZoom(double scale)
    {
        if (!_jsInitialized) return;
        await JS.InvokeVoidAsync("tmWireframeDesigner.zoomTo", _svgRef, scale);
    }

    /// <summary>Fit all elements into view.</summary>
    public async Task FitToView()
    {
        if (!_jsInitialized) return;
        await JS.InvokeVoidAsync("tmWireframeDesigner.fitToView", _svgRef, 40);
    }

    /// <summary>Add an element via command stack (undoable) and trigger a re-render.</summary>
    public async Task AddElement(WireframeElement el)
    {
        if (Document is null) return;
        if (el.ZIndex == 0 && Document.Elements.Count > 0)
            el.ZIndex = Document.Elements.Max(e => e.ZIndex) + 1;

        if (CommandStack is not null)
            CommandStack.Push(new AddElementCommand(Document, el));
        else
            Document.Elements.Add(el);

        await NotifyAndRender();
    }

    /// <summary>Remove elements via command stack (undoable) and trigger a re-render.</summary>
    public async Task RemoveElements(params string[] ids)
    {
        if (Document is null) return;

        if (CommandStack is not null)
            CommandStack.Push(new RemoveElementsCommand(Document, ids));
        else
            Document.Elements.RemoveAll(e => ids.Contains(e.Id));

        await NotifyAndRender();
    }

    /// <summary>Set the JS-side selection highlights without raising OnSelectionChanged.</summary>
    public async Task SelectElements(params string[] ids)
    {
        if (!_jsInitialized) return;
        _currentSelectionIds = ids;
        await JS.InvokeVoidAsync("tmWireframeDesigner.setSelection", _svgRef, ids);
    }

    /// <summary>Clear all selection highlights.</summary>
    public async Task ClearSelection()
    {
        if (!_jsInitialized) return;
        await JS.InvokeVoidAsync("tmWireframeDesigner.setSelection", _svgRef, Array.Empty<string>());
    }

    /// <summary>Switches the JS tool mode programmatically (called from toolbar buttons).</summary>
    public async Task SetToolMode(string mode)
    {
        if (!_jsInitialized) return;
        await JS.InvokeVoidAsync("tmWireframeDesigner.setToolMode", _svgRef, mode);
    }

    /// <summary>Notifies JS of updated canvas dimensions after a ResizeCanvasCommand.</summary>
    public async Task UpdateCanvasSize(double w, double h)
    {
        if (!_jsInitialized) return;
        await JS.InvokeVoidAsync("tmWireframeDesigner.updateCanvasSize", _svgRef, w, h);
    }

    /// <summary>JS invokes this when user presses H or V to switch tool mode.</summary>
    [JSInvokable("OnToolModeChanged")]
    public async Task JsOnToolModeChanged(string mode)
        => await OnToolModeChanged.InvokeAsync(mode);

    /// <summary>Returns the underlying SVG element reference (needed by TmWireframeEditor for JS calls).</summary>
    public ElementReference GetSvgRef() => _svgRef;

    /// <summary>Export the canvas as a clean SVG string (selection handles stripped).</summary>
    public async Task<string> ExportSvg()
    {
        if (!_jsInitialized) return string.Empty;
        return await JS.InvokeAsync<string>("tmWireframeDesigner.exportSvg", _svgRef);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task NotifyAndRender()
    {
        if (Document is not null)
            await DocumentChanged.InvokeAsync(Document);
        await InvokeAsync(StateHasChanged);
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // ── Helper DTOs ──────────────────────────────────────────────────────────

    /// <summary>Payload sent by JS for a bulk move operation.</summary>
    public sealed class ElementMove
    {
        public string Id { get; set; } = "";
        public double X  { get; set; }
        public double Y  { get; set; }
    }
}

/// <summary>Internal deep-copy extension for <see cref="WireframeElement"/>.</summary>
file static class WireframeElementExtensions
{
    internal static WireframeElement DeepCopy(this WireframeElement src)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(src, WireframeJsonOptions.Default);
        var copy = System.Text.Json.JsonSerializer.Deserialize<WireframeElement>(json, WireframeJsonOptions.Default)!;
        copy.Id  = Guid.NewGuid().ToString("N")[..8];
        return copy;
    }
}
