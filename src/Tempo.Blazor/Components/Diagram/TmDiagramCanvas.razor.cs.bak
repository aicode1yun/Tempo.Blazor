using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>
/// Hybrid SVG + HTML diagram canvas. Renders <see cref="DiagramDocument"/> nodes and edges
/// and communicates with <c>diagram-editor.js</c> for pan, zoom, drag, and selection.
/// </summary>
public partial class TmDiagramCanvas : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascaded command stack ───────────────────────────────────────────────

    [CascadingParameter] public DiagramCommandStack? CommandStack { get; set; }

    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>Document to display and edit.</summary>
    [Parameter] public DiagramDocument? Document { get; set; }

    /// <summary>Raised after every mutation.</summary>
    [Parameter] public EventCallback<DiagramDocument> DocumentChanged { get; set; }

    /// <summary>Show grid lines on the canvas background.</summary>
    [Parameter] public bool ShowGrid { get; set; } = true;

    /// <summary>Snap-to-grid cell size in pixels. 0 = disabled.</summary>
    [Parameter] public int GridSize { get; set; } = 8;

    /// <summary>Prevent all editing interactions.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Additional CSS class applied to the canvas wrapper.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Current zoom level used for pixel-to-document conversion.</summary>
    [Parameter] public double Zoom { get; set; } = 1.0;

    /// <summary>Raised when the user changes the selection.</summary>
    [Parameter] public EventCallback<string[]> OnSelectionChanged { get; set; }

    /// <summary>Raised when user requests undo.</summary>
    [Parameter] public EventCallback OnUndo { get; set; }

    /// <summary>Raised when user requests redo.</summary>
    [Parameter] public EventCallback OnRedo { get; set; }

    /// <summary>Raised when the user switches tool mode.</summary>
    [Parameter] public EventCallback<string> OnToolModeChanged { get; set; }

    /// <summary>Raised when the zoom level changes (e.g. via wheel).</summary>
    [Parameter] public EventCallback<double> ZoomChanged { get; set; }

    /// <summary>Raised when the viewport changes after pan, zoom, or fit.</summary>
    [Parameter] public EventCallback<DiagramMinimapViewport> ViewportChanged { get; set; }

    /// <summary>Raised when a port is clicked to start drawing an edge.</summary>
    [Parameter] public EventCallback<(string NodeId, string PortId)> OnPortMouseDown { get; set; }

    /// <summary>Raised when a node is long-pressed (touch) or right-clicked to open a context menu.</summary>
    [Parameter] public EventCallback<(string NodeId, double ScreenX, double ScreenY)> OnContextMenu { get; set; }

    // ── Internal state ───────────────────────────────────────────────────────

    private ElementReference _containerRef;
    private ElementReference _svgRef;
    private ElementReference _labelInputRef;
    private DotNetObjectReference<TmDiagramCanvas>? _dotNetRef;
    private bool _jsInitialized;

    private readonly string _svgId = "tmd-" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _gridSmallId;
    private readonly string _gridLargeId;

    private string _viewBox = "0 0 3000 2000";
    private string _gs = "8";
    private string _gl = "80";

    private Dictionary<string, (double X, double Y)>? _dragStartPositions;
    private string[] _currentSelectionIds = [];

    private string? _resizeNodeId;
    private string? _resizeHandle;
    private (double X, double Y) _resizeStartScreen;
    private (double X, double Y, double W, double H) _resizeStartRect;
    private const double MinNodeSize = 20;
    private const double RotateSnap = 15.0;

    private double _viewportX;
    private double _viewportY;
    private double _viewportW = 3000;
    private double _viewportH = 2000;
    private const double ViewportMargin = 200;
    private string? _editingEdgeLabelId;
    private string _editingLabelValue = "";

    public TmDiagramCanvas()
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
                readOnly = ReadOnly,
                gridSize = GridSize,
                showGrid = ShowGrid,
                canvasWidth = Document?.Width ?? 3000,
                canvasHeight = Document?.Height ?? 2000,
            };

            await JS.InvokeVoidAsync("tmDiagramEditor.init", _containerRef, _dotNetRef, options);
        }
        else if (_jsInitialized && _currentSelectionIds.Length > 0)
        {
            await JS.InvokeVoidAsync("tmDiagramEditor.setSelection", _containerRef, _currentSelectionIds);
        }

        if (_editingEdgeLabelId is not null)
        {
            try { await _labelInputRef.FocusAsync(); }
            catch { /* ignore focus errors */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsInitialized)
        {
            try { await JS.InvokeVoidAsync("tmDiagramEditor.destroy", _containerRef); }
            catch { /* ignore JS errors during disposal */ }
        }
        _dotNetRef?.Dispose();
    }

    // ── JS → C# callbacks ───────────────────────────────────────────────────

    [JSInvokable]
    public void OnDragStarted(string[] ids)
    {
        if (Document is null) return;
        _dragStartPositions = [];
        foreach (var id in ids)
        {
            var node = Document.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is not null)
                _dragStartPositions[id] = (node.X, node.Y);
        }
    }

    [JSInvokable]
    public async Task OnElementMoved(string id, double x, double y)
    {
        if (Document is null || ReadOnly) return;

        var before = _dragStartPositions is not null && _dragStartPositions.TryGetValue(id, out var bp)
            ? new Dictionary<string, (double X, double Y)> { [id] = bp }
            : null;
        var after = new Dictionary<string, (double X, double Y)> { [id] = (x, y) };

        ExecuteMove(before, after);
        await RecalculateOrthogonalWaypointsForMovedNodesAsync([id]);
        await NotifyAndRender();
    }

    [JSInvokable]
    public async Task OnElementsMoved(ElementMove[] moves)
    {
        if (Document is null || ReadOnly) return;

        var before = _dragStartPositions;
        var after = moves.ToDictionary(m => m.Id, m => (m.X, m.Y));

        ExecuteMove(before, after);
        await RecalculateOrthogonalWaypointsForMovedNodesAsync(moves.Select(m => m.Id));
        await NotifyAndRender();
    }

    private void ExecuteMove(
        Dictionary<string, (double X, double Y)>? before,
        Dictionary<string, (double X, double Y)> after)
    {
        if (Document is null) return;

        if (CommandStack is not null)
        {
            var beforeSnapshot = before ?? after.Keys.ToDictionary(
                id => id,
                id => Document.Nodes.FirstOrDefault(n => n.Id == id) is { } node
                    ? (node.X, node.Y) : (0.0, 0.0));

            CommandStack.Push(new MoveNodesCommand(Document, beforeSnapshot, after));
        }
        else
        {
            foreach (var node in Document.Nodes)
            {
                if (after.TryGetValue(node.Id, out var pos))
                { node.X = pos.X; node.Y = pos.Y; }
            }
        }
    }

    // ── Resize handles ───────────────────────────────────────────────────────

    private static readonly string[] _resizeHandles = ["nw", "n", "ne", "e", "se", "s", "sw", "w"];
    private static readonly (string Css, string Label)[] _connectDirections =
    [
        ("n", "Top"),
        ("e", "Right"),
        ("s", "Bottom"),
        ("w", "Left")
    ];

    /// <summary>Raised when a connect arrow is clicked.</summary>
    [Parameter] public EventCallback<(string NodeId, string Direction)> OnConnectArrowClicked { get; set; }

    private void HandleConnectArrowClicked(string nodeId, string direction)
    {
        _ = OnConnectArrowClicked.InvokeAsync((nodeId, direction));
    }

    private void OnResizeStart(MouseEventArgs e, string nodeId, string handle)
    {
        if (ReadOnly || Document is null) return;
        var node = Document.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        _resizeNodeId = nodeId;
        _resizeHandle = handle;
        _resizeStartScreen = (e.ClientX, e.ClientY);
        _resizeStartRect = (node.X, node.Y, node.W, node.H);
    }

    private void OnRotateStartJs(MouseEventArgs e, string nodeId)
    {
        if (ReadOnly || Document is null) return;
        var node = Document.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null || !_jsInitialized) return;
        _ = JS.InvokeVoidAsync("tmDiagramEditor.startRotate", _containerRef, nodeId, e.ClientX, e.ClientY, node.Rotation, RotateSnap);
    }

    [JSInvokable]
    public async Task OnRotateEnded(string nodeId, double angle)
    {
        if (Document is null) return;
        var node = Document.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;
        var before = node.Rotation;
        node.Rotation = angle;
        if (CommandStack is not null && Math.Abs(angle - before) > 0.001)
        {
            CommandStack.Push(new RotateNodeCommand(Document, nodeId, before, angle));
            await DocumentChanged.InvokeAsync(Document);
        }
    }

    private static double SnapAngle(double angle, double snap)
    {
        if (snap <= 0) return angle;
        return Math.Round(angle / snap) * snap;
    }

    private void OnCanvasMouseMove(MouseEventArgs e)
    {
        if (_resizeNodeId is null || Document is null || string.IsNullOrEmpty(_resizeHandle) || Zoom <= 0) return;

        var dx = (e.ClientX - _resizeStartScreen.X) / Zoom;
        var dy = (e.ClientY - _resizeStartScreen.Y) / Zoom;

        var (x, y, w, h) = ComputeResize(_resizeStartRect, _resizeHandle, dx, dy);
        var resizeNode = Document.Nodes.FirstOrDefault(n => n.Id == _resizeNodeId);
        if (resizeNode is null) return;

        resizeNode.X = x;
        resizeNode.Y = y;
        resizeNode.W = w;
        resizeNode.H = h;
        StateHasChanged();

        // Re-sync JS selection outline to follow the new bounds
        if (_jsInitialized)
        {
            _ = JS.InvokeVoidAsync("tmDiagramEditor.updateSelectionOutlines", _containerRef);
        }
    }

    private void OnCanvasMouseUp(MouseEventArgs e)
    {
        if (_resizeNodeId is null || Document is null) return;

        var node = Document.Nodes.FirstOrDefault(n => n.Id == _resizeNodeId);
        if (node is not null && CommandStack is not null)
        {
            if (Math.Abs(node.X - _resizeStartRect.X) > 0.001 || Math.Abs(node.Y - _resizeStartRect.Y) > 0.001 ||
                Math.Abs(node.W - _resizeStartRect.W) > 0.001 || Math.Abs(node.H - _resizeStartRect.H) > 0.001)
            {
                CommandStack.Push(new ResizeNodeCommand(Document, _resizeNodeId,
                    _resizeStartRect.X, _resizeStartRect.Y, _resizeStartRect.W, _resizeStartRect.H,
                    node.X, node.Y, node.W, node.H));
                _ = DocumentChanged.InvokeAsync(Document);
            }
        }

        _resizeNodeId = null;
        _resizeHandle = null;
    }

    private (double X, double Y, double W, double H) ComputeResize(
        (double X, double Y, double W, double H) start, string handle, double dx, double dy)
    {
        var x = start.X;
        var y = start.Y;
        var w = start.W;
        var h = start.H;

        if (handle.Contains('e')) w = start.W + dx;
        if (handle.Contains('w')) { w = start.W - dx; x = start.X + dx; }
        if (handle.Contains('s')) h = start.H + dy;
        if (handle.Contains('n')) { h = start.H - dy; y = start.Y + dy; }

        if (w < MinNodeSize)
        {
            w = MinNodeSize;
            if (handle.Contains('w')) x = start.X + start.W - MinNodeSize;
        }
        if (h < MinNodeSize)
        {
            h = MinNodeSize;
            if (handle.Contains('n')) y = start.Y + start.H - MinNodeSize;
        }

        if (GridSize > 0)
        {
            x = Math.Round(x / GridSize) * GridSize;
            y = Math.Round(y / GridSize) * GridSize;
            w = Math.Round(w / GridSize) * GridSize;
            h = Math.Round(h / GridSize) * GridSize;
        }

        return (x, y, w, h);
    }

    /// <summary>Calls the JS Manhattan router for an edge and returns new waypoints.</summary>
    public async Task<List<DiagramPoint>> ComputeOrthogonalWaypointsAsync(DiagramEdge edge)
    {
        if (Document is null) return [];

        var srcNode = Document.Nodes.FirstOrDefault(n => n.Id == edge.SourceNodeId);
        var tgtNode = Document.Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
        if (srcNode is null || tgtNode is null) return [];

        var srcPort = edge.SourcePortId is not null
            ? srcNode.Ports.FirstOrDefault(p => p.Id == edge.SourcePortId)
            : srcNode.Ports.FirstOrDefault();
        var tgtPort = edge.TargetPortId is not null
            ? tgtNode.Ports.FirstOrDefault(p => p.Id == edge.TargetPortId)
            : tgtNode.Ports.FirstOrDefault();

        var (x1, y1) = ComputePortPosition(srcNode, srcPort ?? new DiagramPort { Side = PortSide.Right, Offset = 0.5 });
        var (x2, y2) = ComputePortPosition(tgtNode, tgtPort ?? new DiagramPort { Side = PortSide.Left, Offset = 0.5 });

        var side1 = (srcPort?.Side ?? PortSide.Right).ToString().ToLowerInvariant();
        var side2 = (tgtPort?.Side ?? PortSide.Left).ToString().ToLowerInvariant();

        var result = await JS.InvokeAsync<double[][]>("tmDiagramEditor.computeOrthogonalWaypoints", x1, y1, side1, x2, y2, side2);
        if (result is null) return [];
        return result.Select(r => new DiagramPoint(r[0], r[1])).ToList();
    }

    private async Task RecalculateOrthogonalWaypointsForMovedNodesAsync(IEnumerable<string> nodeIds)
    {
        if (Document is null) return;
        var affectedEdges = Document.Edges
            .Where(e => e.Routing == "orthogonal" &&
                (nodeIds.Contains(e.SourceNodeId) || nodeIds.Contains(e.TargetNodeId)))
            .ToList();

        foreach (var edge in affectedEdges)
        {
            var before = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
            var newWaypoints = await ComputeOrthogonalWaypointsAsync(edge);
            edge.Waypoints = newWaypoints;
            var after = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();

            if (CommandStack is not null && !WaypointListsEqual(before, after))
                CommandStack.Push(new UpdateEdgeWaypointsCommand(Document, edge.Id, before, after));
        }
    }

    private static bool WaypointListsEqual(List<DiagramPoint> a, List<DiagramPoint> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i].X != b[i].X || a[i].Y != b[i].Y) return false;
        return true;
    }

    [JSInvokable("OnSelectionChanged")]
    public async Task JsOnSelectionChanged(string[] ids)
    {
        _currentSelectionIds = ids;
        await OnSelectionChanged.InvokeAsync(ids);
    }

    [JSInvokable]
    public async Task OnMultiSelect(string[] ids)
    {
        _currentSelectionIds = ids;
        await OnSelectionChanged.InvokeAsync(ids);
    }

    [JSInvokable]
    public async Task OnDeleteSelected(string[] ids)
    {
        if (Document is null || ReadOnly) return;

        if (CommandStack is not null)
            CommandStack.Push(new RemoveNodesCommand(Document, ids));
        else
        {
            var idSet = ids.ToHashSet();
            Document.Nodes.RemoveAll(n => idSet.Contains(n.Id));
            Document.Edges.RemoveAll(e => idSet.Contains(e.SourceNodeId) || idSet.Contains(e.TargetNodeId));
        }

        await NotifyAndRender();
    }

    [JSInvokable]
    public async Task OnSelectAll()
    {
        if (Document is null) return;
        var ids = Document.Nodes.Select(n => n.Id).ToArray();
        _currentSelectionIds = ids;
        await JS.InvokeVoidAsync("tmDiagramEditor.setSelection", _containerRef, ids);
        await OnSelectionChanged.InvokeAsync(ids);
    }

    [JSInvokable]
    public async Task OnClearSelection()
    {
        _currentSelectionIds = [];
        await OnSelectionChanged.InvokeAsync([]);
    }

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

    [JSInvokable("OnToolModeChanged")]
    public async Task JsOnToolModeChanged(string mode)
        => await OnToolModeChanged.InvokeAsync(mode);

    [JSInvokable("OnContextMenu")]
    public async Task JsOnContextMenu(string nodeId, double screenX, double screenY)
        => await OnContextMenu.InvokeAsync((nodeId, screenX, screenY));

    [JSInvokable]
    public async Task OnZoomChanged(double scale)
    {
        OnZoomChangedInternal(scale);
        await ZoomChanged.InvokeAsync(scale);
    }

    [JSInvokable]
    public void OnViewBoxChanged(double x, double y, double w, double h)
    {
        _viewBox = $"{F(x)} {F(y)} {F(w)} {F(h)}";
    }

    [JSInvokable]
    public async Task OnViewportChanged(double x, double y, double w, double h)
    {
        _viewportX = x;
        _viewportY = y;
        _viewportW = w;
        _viewportH = h;
        await ViewportChanged.InvokeAsync(new DiagramMinimapViewport(x, y, w, h));
        await InvokeAsync(StateHasChanged);
    }

    private bool IsNodeVisible(DiagramNode node)
    {
        return node.X + node.W >= _viewportX - ViewportMargin
            && node.Y + node.H >= _viewportY - ViewportMargin
            && node.X <= _viewportX + _viewportW + ViewportMargin
            && node.Y <= _viewportY + _viewportH + ViewportMargin;
    }

    private bool IsLayerVisible(DiagramNode node)
    {
        if (node.LayerId is null) return true;
        var layer = Document?.Layers.FirstOrDefault(l => l.Id == node.LayerId);
        return layer?.IsVisible ?? true;
    }

    private bool IsNodeLocked(DiagramNode node)
    {
        if (node.LayerId is null) return false;
        var layer = Document?.Layers.FirstOrDefault(l => l.Id == node.LayerId);
        return layer?.IsLocked ?? false;
    }

    [JSInvokable]
    public async Task OnDuplicate(string[] ids)
    {
        if (Document is null || ReadOnly) return;
        var maxZ = Document.Nodes.Count > 0 ? Document.Nodes.Max(n => n.ZIndex) : 0;
        var offset = GridSize > 0 ? GridSize * 2 : 16;
        var added = new List<string>();

        foreach (var id in ids)
        {
            var src = Document.Nodes.FirstOrDefault(n => n.Id == id);
            if (src is null) continue;
            var copy = DeepCopyNode(src);
            copy.X += offset; copy.Y += offset;
            copy.ZIndex = ++maxZ;

            if (CommandStack is not null)
                CommandStack.Push(new AddNodeCommand(Document, copy));
            else
                Document.Nodes.Add(copy);

            added.Add(copy.Id);
        }

        if (added.Count == 0) return;
        await NotifyAndRender();
        await JS.InvokeVoidAsync("tmDiagramEditor.setSelection", _containerRef, added.ToArray());
        await OnSelectionChanged.InvokeAsync(added.ToArray());
    }

    // ── Drag & Drop from toolbox ─────────────────────────────────────────────

    [JSInvokable]
    public async Task OnDropFromToolbox(string stencilId, double x, double y)
    {
        if (ReadOnly || Document is null) return;
        if (string.IsNullOrWhiteSpace(stencilId)) return;

        x = Math.Round(x / GridSize) * GridSize;
        y = Math.Round(y / GridSize) * GridSize;

        await OnToolboxDrop.InvokeAsync((StencilId: stencilId, X: x, Y: y));
    }

    [Parameter] public EventCallback<(string StencilId, double X, double Y)> OnToolboxDrop { get; set; }

    [Parameter] public EventCallback<(string SourceNodeId, string SourcePortId, string TargetNodeId, string TargetPortId)> OnEdgeCreated { get; set; }

    // ── Port interactions ────────────────────────────────────────────────────

    private async Task HandlePortMouseDown(string nodeId, string portId)
    {
        if (ReadOnly) return;
        await OnPortMouseDownEvent.InvokeAsync((nodeId, portId));
    }

    [Parameter] public EventCallback<(string NodeId, string PortId)> OnPortMouseDownEvent { get; set; }

    private void HandlePortMouseEnter(string nodeId, string portId) { }
    private void HandlePortMouseLeave(string nodeId, string portId) { }

    [JSInvokable]
    public async Task JsOnEdgeCreated(string sourceNodeId, string sourcePortId, string targetNodeId, string targetPortId)
    {
        if (ReadOnly || Document is null) return;
        await OnEdgeCreated.InvokeAsync((sourceNodeId, sourcePortId, targetNodeId, targetPortId));
    }

    // ── Edge interactions ────────────────────────────────────────────────────

    private async Task OnEdgeClicked(string edgeId)
    {
        _currentSelectionIds = [edgeId];
        await JS.InvokeVoidAsync("tmDiagramEditor.setSelection", _containerRef, Array.Empty<string>());
        await OnSelectionChanged.InvokeAsync(_currentSelectionIds);
    }

    [JSInvokable]
    public async Task OnEdgeWaypointMoved(string edgeId, int waypointIndex, double x, double y)
    {
        if (ReadOnly || Document is null) return;
        var edge = Document.Edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge is null || waypointIndex < 0 || waypointIndex >= edge.Waypoints.Count) return;

        var before = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
        edge.Waypoints[waypointIndex] = new DiagramPoint(x, y);
        var after = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();

        if (CommandStack is not null)
            CommandStack.Push(new UpdateEdgeWaypointsCommand(Document, edgeId, before, after));

        await NotifyAndRender();
    }

    private void OnEdgeLabelClicked(string edgeId)
    {
        if (ReadOnly || Document is null) return;
        var edge = Document.Edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge is null) return;
        _editingEdgeLabelId = edgeId;
        _editingLabelValue = edge.Label ?? "";
    }

    private void OnLabelInputChanged(ChangeEventArgs e)
    {
        _editingLabelValue = e.Value?.ToString() ?? "";
    }

    private async Task OnLabelInputKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await SaveEdgeLabel();
        else if (e.Key == "Escape")
            CancelEdgeLabelEdit();
    }

    private async Task OnLabelInputBlur()
    {
        await SaveEdgeLabel();
    }

    private async Task SaveEdgeLabel()
    {
        if (Document is null || _editingEdgeLabelId is null) return;
        var edge = Document.Edges.FirstOrDefault(e => e.Id == _editingEdgeLabelId);
        if (edge is null)
        {
            CancelEdgeLabelEdit();
            return;
        }

        var newLabel = _editingLabelValue.Trim();
        if (newLabel != (edge.Label ?? ""))
        {
            if (CommandStack is not null)
                CommandStack.Push(new UpdateEdgeLabelCommand(Document, edge.Id, edge.Label, newLabel));
            else
                edge.Label = newLabel;
        }

        CancelEdgeLabelEdit();
        await NotifyAndRender();
    }

    private void CancelEdgeLabelEdit()
    {
        _editingEdgeLabelId = null;
        _editingLabelValue = "";
    }

    private async Task HandleSectionEdit(string nodeId, string dataKey, object value)
    {
        if (Document is null) return;
        var node = Document.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;
        var oldData = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(node.Data)) ?? new Dictionary<string, object>();
        var newData = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(node.Data)) ?? new Dictionary<string, object>();
        newData[dataKey] = value;
        if (CommandStack is not null)
        {
            CommandStack.Push(new UpdateNodeDataCommand(Document, nodeId, oldData, newData));
        }
        else
        {
            node.Data = newData;
        }
        await NotifyAndRender();
    }

    private (double X, double Y) ComputeEdgeMidpoint(DiagramEdge edge)
    {
        var pts = GetEdgePoints(edge);
        if (pts.Length < 2) return (0, 0);

        double totalLen = 0;
        var segs = new List<(int Idx, double Len)>();
        for (int i = 0; i < pts.Length - 1; i++)
        {
            var dx = pts[i + 1].X - pts[i].X;
            var dy = pts[i + 1].Y - pts[i].Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            segs.Add((i, len));
            totalLen += len;
        }

        if (totalLen == 0) return pts[0];

        var half = totalLen / 2;
        double accum = 0;
        foreach (var (idx, len) in segs)
        {
            if (accum + len >= half)
            {
                var t = (half - accum) / len;
                var x = pts[idx].X + t * (pts[idx + 1].X - pts[idx].X);
                var y = pts[idx].Y + t * (pts[idx + 1].Y - pts[idx].Y);
                return (x, y);
            }
            accum += len;
        }

        return pts[^1];
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task SetZoom(double scale)
    {
        if (!_jsInitialized) return;
        await JS.InvokeVoidAsync("tmDiagramEditor.zoomTo", _containerRef, scale);
    }

    public async Task FitToView()
    {
        if (!_jsInitialized) return;
        await JS.InvokeVoidAsync("tmDiagramEditor.fitToView", _containerRef, 40);
    }

    public async Task SetSelection(params string[] ids)
    {
        if (!_jsInitialized) return;
        _currentSelectionIds = ids;
        var nodeIds = Document is null
            ? ids
            : ids.Where(id => Document.Nodes.Any(n => n.Id == id)).ToArray();
        await JS.InvokeVoidAsync("tmDiagramEditor.setSelection", _containerRef, nodeIds);
    }

    public async Task SetToolMode(string mode)
    {
        if (!_jsInitialized) return;
        await JS.InvokeVoidAsync("tmDiagramEditor.setToolMode", _containerRef, mode);
    }

    public async Task UpdateCanvasSize(double w, double h)
    {
        if (!_jsInitialized) return;
        await JS.InvokeVoidAsync("tmDiagramEditor.updateCanvasSize", _containerRef, w, h);
    }

    public async Task ScrollTo(double centreX, double centreY)
    {
        if (!_jsInitialized) return;
        await JS.InvokeVoidAsync("tmDiagramEditor.scrollTo", _containerRef, centreX, centreY);
    }

    public ElementReference GetContainerRef() => _containerRef;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task NotifyAndRender()
    {
        if (Document is not null)
            await DocumentChanged.InvokeAsync(Document);
        await InvokeAsync(StateHasChanged);
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private double GetCurrentScale()
    {
        // Parse from viewBox width vs actual SVG width is done in JS;
        // For Drop we can approximate 1.0 if not initialized.
        if (!_jsInitialized) return 1.0;
        var vbW = Document?.Width ?? 3000;
        // We don't have direct DOM width here, but Drop offset is relative to container.
        // The container and SVG are 100% width, so offset is in screen pixels.
        // We need scale. We'll use JS to get scale, but for simplicity we can store it.
        return _currentScale;
    }

    private double _currentScale = 1.0;

    [JSInvokable]
    public void OnZoomChangedInternal(double scale) => _currentScale = scale;

    private static DiagramNode DeepCopyNode(DiagramNode src)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(src);
        var copy = System.Text.Json.JsonSerializer.Deserialize<DiagramNode>(json)!;
        copy.Id = Guid.NewGuid().ToString("N")[..8];
        foreach (var p in copy.Ports)
            p.Id = Guid.NewGuid().ToString("N")[..8];
        return copy;
    }

    // ── Geometry helpers ─────────────────────────────────────────────────────

    public static (double X, double Y) ComputePortPosition(DiagramNode node, DiagramPort port)
    {
        var x = port.Side switch
        {
            PortSide.Left => 0,
            PortSide.Right => node.W,
            _ => node.W * port.Offset,
        };
        var y = port.Side switch
        {
            PortSide.Top => 0,
            PortSide.Bottom => node.H,
            _ => node.H * port.Offset,
        };
        return (node.X + x, node.Y + y);
    }

    private (double X, double Y)[] GetEdgePoints(DiagramEdge edge)
    {
        if (Document is null) return [];

        var srcNode = Document.Nodes.FirstOrDefault(n => n.Id == edge.SourceNodeId);
        var tgtNode = Document.Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
        if (srcNode is null || tgtNode is null) return [];

        var srcPort = edge.SourcePortId is not null
            ? srcNode.Ports.FirstOrDefault(p => p.Id == edge.SourcePortId)
            : srcNode.Ports.FirstOrDefault();
        var tgtPort = edge.TargetPortId is not null
            ? tgtNode.Ports.FirstOrDefault(p => p.Id == edge.TargetPortId)
            : tgtNode.Ports.FirstOrDefault();

        var p1 = ComputePortPosition(srcNode, srcPort ?? new DiagramPort { Side = PortSide.Right, Offset = 0.5 });
        var p2 = ComputePortPosition(tgtNode, tgtPort ?? new DiagramPort { Side = PortSide.Left, Offset = 0.5 });

        var pts = new List<(double X, double Y)> { p1 };
        foreach (var wp in edge.Waypoints)
            pts.Add((wp.X, wp.Y));
        pts.Add(p2);
        return pts.ToArray();
    }

    private string ComputeEdgePath(DiagramEdge edge)
    {
        var pts = GetEdgePoints(edge);
        if (pts.Length < 2) return string.Empty;
        var sb = new System.Text.StringBuilder();
        sb.Append($"M {F(pts[0].X)} {F(pts[0].Y)}");
        for (int i = 1; i < pts.Length; i++)
            sb.Append($" L {F(pts[i].X)} {F(pts[i].Y)}");
        return sb.ToString();
    }

    private static string GetEdgeStrokeDasharray(DiagramEdge edge)
        => edge.ConnectorType == "dependency" ? "5,5" : "";

    private static string GetEdgeMarkerEnd(DiagramEdge edge)
        => edge.ConnectorType switch
        {
            "inheritance" => "url(#arrow-empty-triangle)",
            "dependency" => "url(#arrow-default)",
            _ => "url(#arrow-default)",
        };

    private static string GetEdgeMarkerStart(DiagramEdge edge)
        => edge.ConnectorType switch
        {
            "composition" => "url(#arrow-filled-diamond)",
            "aggregation" => "url(#arrow-empty-diamond)",
            _ => "",
        };

    // ── Helper DTOs ──────────────────────────────────────────────────────────

    public sealed class ElementMove
    {
        public string Id { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
    }


}
