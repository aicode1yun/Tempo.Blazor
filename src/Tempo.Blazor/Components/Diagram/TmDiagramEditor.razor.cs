using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Components.Diagram.Stencils;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>
/// Top-level diagram editor component. Composes the canvas, toolbox, properties panel,
/// and toolbar into a fully working editor.
/// </summary>
public partial class TmDiagramEditor : ComponentBase, IDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;
    [Inject] private DiagramStencilRegistry StencilRegistry { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>Document being edited. Bind with <c>@bind-Document</c>.</summary>
    [Parameter] public DiagramDocument? Document { get; set; }

    /// <summary>Raised after every mutation.</summary>
    [Parameter] public EventCallback<DiagramDocument> DocumentChanged { get; set; }

    /// <summary>Prevent all editing interactions.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Show the component toolbox panel on the left.</summary>
    [Parameter] public bool ShowToolbox { get; set; } = true;

    /// <summary>Show the properties panel on the right.</summary>
    [Parameter] public bool ShowPropertiesPanel { get; set; } = true;

    /// <summary>Show the layers panel on the right.</summary>
    [Parameter] public bool ShowLayersPanel { get; set; } = true;

    /// <summary>Show the minimap at the bottom of the right sidebar.</summary>
    [Parameter] public bool ShowMinimap { get; set; } = true;

    /// <summary>Show the top toolbar.</summary>
    [Parameter] public bool ShowToolbar { get; set; } = true;

    /// <summary>Show grid lines on the canvas background.</summary>
    [Parameter] public bool ShowGrid { get; set; } = true;

    /// <summary>Snap-to-grid cell size in pixels. 0 = disabled.</summary>
    [Parameter] public int GridSize { get; set; } = 8;

    /// <summary>Optional named HttpClient to use for SVG/PNG/PDF export calls.</summary>
    [Parameter] public string ExportHttpClientName { get; set; } = "";

    /// <summary>Additional CSS class on the editor root.</summary>
    [Parameter] public string? Class { get; set; }

    // ── Child component refs ─────────────────────────────────────────────────

    private TmDiagramCanvas? _canvas;
    private ElementReference _downloadAnchor;

    // ── Command stack ────────────────────────────────────────────────────────

    private readonly DiagramCommandStack _commandStack = new();

    // ── Derived / UI state ───────────────────────────────────────────────────

    private DiagramDocument? _document;
    private string[] _selectedIds = [];

    private double _zoomLevel = 1.0;
    private string _zoomLabel => $"{(int)Math.Round(_zoomLevel * 100)}%";

    private bool _toolboxCollapsed;
    private bool _propsCollapsed;
    private bool _layersCollapsed;
    private bool _minimapCollapsed;
    private bool _exportMenuOpen;
    private bool _exporting;
    private string? _importError;
    private string? _exportError;
    private DiagramMinimapViewport? _minimapViewport;

    private string? _activeLayerId;

    private string _toolMode = "select";

    private bool _canvasSizeEditing;
    private string _canvasWInput = "";
    private string _canvasHInput = "";

    // Connect modal state
    private bool _connectModalOpen;
    private string? _connectSourceNodeId;
    private string? _connectDirection;

    // ── Context menu ─────────────────────────────────────────────────────────

    private bool _contextMenuOpen;
    private string? _contextMenuNodeId;
    private double _contextMenuScreenX;
    private double _contextMenuScreenY;

    // ── Search panel ─────────────────────────────────────────────────────────

    private bool _showSearchPanel;
    private string _searchQuery = "";
    private List<DiagramSearchResult> _searchResults = [];
    private int _searchCurrentIndex;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        _commandStack.OnStackChanged += OnStackChanged;
        _document = Document ?? CreateEmptyDocument();
    }

    protected override void OnParametersSet()
    {
        if (Document is not null && !ReferenceEquals(Document, _document))
        {
            _document = Document;
            _commandStack.Clear();
            _selectedIds = [];
        }
    }

    public void Dispose()
    {
        _commandStack.OnStackChanged -= OnStackChanged;
    }

    // ── Stack change → re-render ─────────────────────────────────────────────

    private void OnStackChanged() => InvokeAsync(StateHasChanged);

    // ── Document change propagation ──────────────────────────────────────────

    private async Task OnDocumentChanged(DiagramDocument doc)
    {
        _document = doc;
        await DocumentChanged.InvokeAsync(doc);
    }

    // ── Selection ────────────────────────────────────────────────────────────

    private async Task OnSelectionChanged(string[] ids)
    {
        _selectedIds = ids;
        await InvokeAsync(StateHasChanged);
    }

    // ── Toolbar: Tool mode ───────────────────────────────────────────────────

    private async Task SetToolMode(string mode)
    {
        _toolMode = mode;
        if (_canvas is not null) await _canvas.SetToolMode(mode);
    }

    private void OnCanvasToolModeChanged(string mode)
    {
        _toolMode = mode;
        StateHasChanged();
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private async Task OnContextMenu((string NodeId, double ScreenX, double ScreenY) args)
    {
        if (ReadOnly) return;
        _contextMenuOpen = true;
        _contextMenuNodeId = args.NodeId;
        _contextMenuScreenX = args.ScreenX;
        _contextMenuScreenY = args.ScreenY;

        if (!_selectedIds.Contains(args.NodeId) && _canvas is not null)
        {
            _selectedIds = [args.NodeId];
            await _canvas.SetSelection(args.NodeId);
        }
        await InvokeAsync(StateHasChanged);
    }

    private void CloseContextMenu() => _contextMenuOpen = false;

    private async Task ContextMenuDelete()
    {
        _contextMenuOpen = false;
        if (_document is null || ReadOnly || _contextMenuNodeId is null) return;
        var ids = new[] { _contextMenuNodeId };
        _commandStack.Push(new RemoveNodesCommand(_document, ids));
        _selectedIds = [];
        if (_canvas is not null) await _canvas.SetSelection([]);
        await OnDocumentChanged(_document);
    }

    private async Task ContextMenuBringToFront()
    {
        _contextMenuOpen = false;
        if (_document is null || ReadOnly || _contextMenuNodeId is null) return;
        var node = _document.Nodes.FirstOrDefault(n => n.Id == _contextMenuNodeId);
        if (node is null) return;
        var maxZ = _document.Nodes.Count > 0 ? _document.Nodes.Max(n => n.ZIndex) : 0;
        if (node.ZIndex == maxZ) return;

        var before = new Dictionary<string, int> { [_contextMenuNodeId] = node.ZIndex };
        var after = new Dictionary<string, int> { [_contextMenuNodeId] = maxZ + 1 };
        _commandStack.Push(new UpdateZIndexCommand(_document, before, after));
        await OnDocumentChanged(_document);
    }

    private async Task ContextMenuSendToBack()
    {
        _contextMenuOpen = false;
        if (_document is null || ReadOnly || _contextMenuNodeId is null) return;
        var node = _document.Nodes.FirstOrDefault(n => n.Id == _contextMenuNodeId);
        if (node is null) return;
        var minZ = _document.Nodes.Count > 0 ? _document.Nodes.Min(n => n.ZIndex) : 0;
        if (node.ZIndex == minZ) return;

        var before = new Dictionary<string, int> { [_contextMenuNodeId] = node.ZIndex };
        var after = new Dictionary<string, int> { [_contextMenuNodeId] = minZ - 1 };
        _commandStack.Push(new UpdateZIndexCommand(_document, before, after));
        await OnDocumentChanged(_document);
    }

    // ── Search panel ─────────────────────────────────────────────────────────

    private void OpenSearchPanel()
    {
        _showSearchPanel = true;
        _searchQuery = "";
        _searchResults = [];
        _searchCurrentIndex = 0;
        if (_canvas is not null)
            _ = _canvas.SetActiveSearchResult(null);
    }

    private void CloseSearchPanel()
    {
        _showSearchPanel = false;
        _searchQuery = "";
        _searchResults = [];
        _searchCurrentIndex = 0;
        if (_canvas is not null)
            _ = _canvas.SetActiveSearchResult(null);
    }

    private void OnSearchQueryChanged(string query)
    {
        _searchQuery = query;
        _searchResults = string.IsNullOrWhiteSpace(query)
            ? []
            : DiagramSearchService.Search(_document, query);
        _searchCurrentIndex = _searchResults.Count > 0 ? 0 : 0;
        if (_canvas is not null)
        {
            var activeId = _searchResults.Count > 0
                ? _searchResults[0].NodeId ?? _searchResults[0].EdgeId
                : null;
            _ = _canvas.SetActiveSearchResult(activeId);
        }
    }

    private async Task OnSearchIndexChanged(int index)
    {
        _searchCurrentIndex = index;
        if (_searchResults.Count == 0 || _canvas is null) return;

        var result = _searchResults[index];
        var activeId = result.NodeId ?? result.EdgeId;
        await _canvas.SetActiveSearchResult(activeId);

        if (result.NodeId is not null)
        {
            await _canvas.FocusOnNode(result.NodeId);
            _selectedIds = [result.NodeId];
        }
        else if (result.EdgeId is not null)
        {
            await _canvas.FocusOnEdge(result.EdgeId);
            _selectedIds = [result.EdgeId];
        }
        await InvokeAsync(StateHasChanged);
    }

    private void OnCanvasZoomChanged(double scale)
    {
        _zoomLevel = scale;
        StateHasChanged();
    }

    private void OnCanvasViewportChanged(DiagramMinimapViewport vp)
    {
        _minimapViewport = vp;
    }

    private async Task OnMinimapNavigate(DiagramMinimapNavigateArgs args)
    {
        if (_canvas is not null)
            await _canvas.ScrollTo(args.CentreX, args.CentreY);
    }

    // ── Toolbar: Canvas size ─────────────────────────────────────────────────

    private void OpenCanvasSize()
    {
        _canvasWInput = ((int)(_document?.Width ?? 3000)).ToString();
        _canvasHInput = ((int)(_document?.Height ?? 2000)).ToString();
        _canvasSizeEditing = true;
    }

    private async Task ApplyCanvasSize()
    {
        _canvasSizeEditing = false;
        if (_document is null || ReadOnly) return;
        if (!double.TryParse(_canvasWInput, out var w) || w < 100) return;
        if (!double.TryParse(_canvasHInput, out var h) || h < 100) return;
        if (Math.Abs(w - _document.Width) < 0.5 && Math.Abs(h - _document.Height) < 0.5) return;

        // Direct mutation (no dedicated command for canvas resize in MVP)
        _document.Width = w;
        _document.Height = h;
        if (_canvas is not null) await _canvas.UpdateCanvasSize(w, h);
        await OnDocumentChanged(_document);
    }

    // ── Toolbar: Title ───────────────────────────────────────────────────────

    private async Task OnTitleChanged(ChangeEventArgs e)
    {
        if (_document is null || ReadOnly) return;
        _document.Title = e.Value?.ToString() ?? "";
        await OnDocumentChanged(_document);
    }

    // ── Toolbar: Undo / Redo ─────────────────────────────────────────────────

    private async Task OnUndoClicked()
    {
        if (ReadOnly) return;
        _commandStack.Undo();
        await RerenderCanvas();
    }

    private async Task OnRedoClicked()
    {
        if (ReadOnly) return;
        _commandStack.Redo();
        await RerenderCanvas();
    }

    // ── Toolbar: Format Painter ──────────────────────────────────────────────

    private void OnCopyStyle()
    {
        if (_document is null || ReadOnly || _selectedIds.Length == 0) return;
        var node = _document.Nodes.FirstOrDefault(n => n.Id == _selectedIds[0]);
        if (node is null) return;
        new CopyStyleCommand(node, includeSize: false).Execute();
    }

    private async Task OnPasteStyle()
    {
        if (_document is null || ReadOnly || _selectedIds.Length == 0) return;
        _commandStack.Push(new PasteStyleCommand(_document, _selectedIds));
        await OnDocumentChanged(_document);
    }

    private async Task OnPasteSize()
    {
        if (_document is null || ReadOnly || _selectedIds.Length == 0) return;
        _commandStack.Push(new PasteSizeCommand(_document, _selectedIds));
        await OnDocumentChanged(_document);
    }

    // ── Toolbar: Zoom ────────────────────────────────────────────────────────

    private static readonly double[] ZoomSteps = [0.25, 0.33, 0.5, 0.67, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0];

    private async Task ZoomIn()
    {
        var next = ZoomSteps.FirstOrDefault(z => z > _zoomLevel + 1e-9);
        if (next == 0) next = ZoomSteps[^1];
        await ApplyZoom(next);
    }

    private async Task ZoomOut()
    {
        var prev = ZoomSteps.LastOrDefault(z => z < _zoomLevel - 1e-9);
        if (prev == 0) prev = ZoomSteps[0];
        await ApplyZoom(prev);
    }

    private async Task ApplyZoom(double level)
    {
        _zoomLevel = level;
        if (_canvas is not null)
            await _canvas.SetZoom(level);
    }

    private async Task FitToView()
    {
        if (_canvas is not null)
        {
            var scale = await JS.InvokeAsync<double>("tmDiagramEditor.fitToView", _canvas.GetContainerRef(), 40);
            _zoomLevel = scale;
        }
    }

    private bool _layoutMenuOpen;

    private void ToggleLayoutMenu() => _layoutMenuOpen = !_layoutMenuOpen;

    private async Task RunLayout(string direction)
    {
        _layoutMenuOpen = false;
        if (_canvas is null) return;
        await _canvas.RunLayoutAsync(direction);
    }

    // ── Toolbar: Export ──────────────────────────────────────────────────────

    private void ToggleExportMenu() => _exportMenuOpen = !_exportMenuOpen;

    private async Task ExportJson()
    {
        _exportMenuOpen = false;
        if (_document is null) return;
        var json = DiagramSerializer.Serialize(_document);
        var fileName = SanitizeFileName(_document.Title) + ".diagram.json";
        await DownloadFile(fileName, "application/json", System.Text.Encoding.UTF8.GetBytes(json));
    }

    private async Task ExportSvg()
    {
        _exportMenuOpen = false;
        await ExportToServerAsync("svg", "image/svg+xml", ".svg");
    }

    private async Task ExportPng()
    {
        _exportMenuOpen = false;
        await ExportToServerAsync("png", "image/png", ".png");
    }

    private async Task ExportPdf()
    {
        _exportMenuOpen = false;
        await ExportToServerAsync("pdf", "application/pdf", ".pdf");
    }

    private async Task ExportToServerAsync(string format, string mimeType, string extension)
    {
        if (_document is null) return;

        _exporting = true;
        _exportError = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            var client = string.IsNullOrEmpty(ExportHttpClientName)
                ? HttpClientFactory.CreateClient()
                : HttpClientFactory.CreateClient(ExportHttpClientName);

            var response = await client.PostAsJsonAsync($"/api/diagram/export/{format}", _document);
            response.EnsureSuccessStatusCode();

            byte[] data;
            if (format == "svg")
            {
                var svg = await response.Content.ReadAsStringAsync();
                data = System.Text.Encoding.UTF8.GetBytes(svg);
            }
            else
            {
                data = await response.Content.ReadAsByteArrayAsync();
            }

            var fileName = SanitizeFileName(_document.Title) + extension;
            await DownloadFile(fileName, mimeType, data);
        }
        catch (Exception ex)
        {
            _exportError = Loc["TmDiagramEditor_ExportError", ex.Message];
        }
        finally
        {
            _exporting = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Toolbar: Import ──────────────────────────────────────────────────────

    private async Task OnImportFile(InputFileChangeEventArgs e)
    {
        _importError = null;
        var file = e.File;
        if (file is null) return;

        try
        {
            await using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
            using var reader = new System.IO.StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            if (!DiagramSerializer.TryDeserialize(json, out var doc) || doc is null)
            {
                _importError = Loc["TmDiagramEditor_ImportError_InvalidJson"];
                return;
            }

            _document = doc;
            _commandStack.Clear();
            _selectedIds = [];
            await DocumentChanged.InvokeAsync(_document);
            await RerenderCanvas();
        }
        catch (Exception ex)
        {
            _importError = Loc["TmDiagramEditor_ImportError_Generic", ex.Message];
        }
    }

    // ── Toolbar: Read-only toggle ────────────────────────────────────────────

    private void ToggleReadOnly() => ReadOnly = !ReadOnly;

    // ── Toolbox drag activation ──────────────────────────────────────────────

    private void OnToolboxDragStart(string stencilId)
    {
        // Toolbox handles the HTML5 dragstart; this is just a hook if needed
    }

    private async Task OnToolboxDrop((string StencilId, double X, double Y) drop)
    {
        if (_document is null || ReadOnly) return;

        var stencil = StencilRegistry.GetStencil(drop.StencilId);
        var w = stencil?.DefaultWidth ?? 120;
        var h = stencil?.DefaultHeight ?? 60;
        var x = Math.Round(drop.X / GridSize) * GridSize;
        var y = Math.Round(drop.Y / GridSize) * GridSize;

        var node = new DiagramNode
        {
            StencilId = drop.StencilId,
            X = x,
            Y = y,
            W = w,
            H = h,
            ZIndex = _document.Nodes.Count > 0 ? _document.Nodes.Max(n => n.ZIndex) + 1 : 0,
            LayerId = _activeLayerId,
            IsCollapsible = stencil?.IsCollapsible ?? false,
        };

        // Copy default data
        foreach (var kvp in stencil?.DefaultData ?? [])
        {
            node.Data[kvp.Key] = kvp.Value;
        }

        // Generate ports from stencil
        foreach (var portDef in stencil?.Ports ?? [])
        {
            node.Ports.Add(new DiagramPort
            {
                Name = portDef.Name,
                Side = portDef.Side,
                Offset = portDef.Offset,
                IsInput = portDef.IsInput,
                IsOutput = portDef.IsOutput,
            });
        }

        _commandStack.Push(new AddNodeCommand(_document, node));
        await OnDocumentChanged(_document);
        if (_canvas is not null)
        {
            await _canvas.SetSelection(node.Id);
            await OnSelectionChanged([node.Id]);
        }
    }

    // ── Port interaction / Edge creation ────────────────────────────────────

    private void OnPortMouseDownEvent((string NodeId, string PortId) args)
    {
        // Edge drawing is handled in JS; this is a hook for future extensions
    }

    private async Task OnEdgeCreated((string SourceNodeId, string SourcePortId, string TargetNodeId, string TargetPortId) args)
    {
        if (_document is null || ReadOnly) return;

        var edge = new DiagramEdge
        {
            SourceNodeId = args.SourceNodeId,
            SourcePortId = args.SourcePortId,
            TargetNodeId = args.TargetNodeId,
            TargetPortId = args.TargetPortId,
        };

        _commandStack.Push(new AddEdgeCommand(_document, edge));
        await OnDocumentChanged(_document);

        // Select the new edge
        _selectedIds = [edge.Id];
        if (_canvas is not null)
        {
            await _canvas.SetSelection(edge.Id);
        }
    }

    private async Task HandleEdgeRoutingChanged((string EdgeId, string OldRouting, string NewRouting) args)
    {
        if (_document is null || _canvas is null || ReadOnly) return;
        var edge = _document.Edges.FirstOrDefault(e => e.Id == args.EdgeId);
        if (edge is null) return;

        var oldWaypoints = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();

        if (args.NewRouting is "orthogonal" or "elbow" or "segment")
        {
            edge.Waypoints = await _canvas.ComputeOrthogonalWaypointsAsync(edge);
        }
        else
        {
            edge.Waypoints.Clear();
        }

        var newWaypoints = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
        _commandStack.Push(new UpdateEdgeRoutingCommand(_document, edge.Id, args.OldRouting, args.NewRouting, oldWaypoints, newWaypoints));
        await OnDocumentChanged(_document);
    }

    // ── Connect modal ────────────────────────────────────────────────────────

    private void OnConnectArrowClicked((string NodeId, string Direction) args)
    {
        if (ReadOnly || _document is null) return;
        _connectSourceNodeId = args.NodeId;
        _connectDirection = args.Direction;
        _connectModalOpen = true;
        StateHasChanged();
    }

    private void CloseConnectModal()
    {
        _connectModalOpen = false;
        _connectSourceNodeId = null;
        _connectDirection = null;
    }

    private async Task OnStencilSelectedForConnect(string stencilId)
    {
        if (_document is null || ReadOnly || _connectSourceNodeId is null || _connectDirection is null) return;

        var sourceNode = _document.Nodes.FirstOrDefault(n => n.Id == _connectSourceNodeId);
        var stencil = StencilRegistry.GetStencil(stencilId);
        if (sourceNode is null || stencil is null) return;

        var w = stencil.DefaultWidth;
        var h = stencil.DefaultHeight;
        var (x, y) = ComputeConnectPosition(sourceNode, w, h, _connectDirection);
        x = Math.Round(x / GridSize) * GridSize;
        y = Math.Round(y / GridSize) * GridSize;

        var newNode = new DiagramNode
        {
            StencilId = stencilId,
            X = x,
            Y = y,
            W = w,
            H = h,
            ZIndex = _document.Nodes.Count > 0 ? _document.Nodes.Max(n => n.ZIndex) + 1 : 0,
            LayerId = sourceNode.LayerId ?? _activeLayerId,
            IsCollapsible = stencil?.IsCollapsible ?? false,
        };

        foreach (var kvp in stencil.DefaultData)
            newNode.Data[kvp.Key] = kvp.Value;

        foreach (var portDef in stencil.Ports)
        {
            newNode.Ports.Add(new DiagramPort
            {
                Name = portDef.Name,
                Side = portDef.Side,
                Offset = portDef.Offset,
                IsInput = portDef.IsInput,
                IsOutput = portDef.IsOutput,
            });
        }

        _commandStack.Push(new AddNodeCommand(_document, newNode));

        // Determine ports based on direction
        var (sourcePort, targetPort) = ResolveConnectPorts(sourceNode, newNode, _connectDirection);

        var edge = new DiagramEdge
        {
            SourceNodeId = sourceNode.Id,
            SourcePortId = sourcePort?.Id,
            TargetNodeId = newNode.Id,
            TargetPortId = targetPort?.Id,
        };

        _commandStack.Push(new AddEdgeCommand(_document, edge));

        if (_canvas is not null)
        {
            edge.Waypoints = await _canvas.ComputeOrthogonalWaypointsAsync(edge);
        }

        _selectedIds = [newNode.Id];
        await OnDocumentChanged(_document);
        if (_canvas is not null)
        {
            await _canvas.SetSelection(newNode.Id);
            await OnSelectionChanged([newNode.Id]);
        }

        CloseConnectModal();
    }

    private static (double X, double Y) ComputeConnectPosition(DiagramNode source, double newW, double newH, string direction)
    {
        const double padding = 40.0;
        return direction switch
        {
            "n" => (source.X + source.W / 2.0 - newW / 2.0, source.Y - newH - padding),
            "e" => (source.X + source.W + padding, source.Y + source.H / 2.0 - newH / 2.0),
            "s" => (source.X + source.W / 2.0 - newW / 2.0, source.Y + source.H + padding),
            "w" => (source.X - newW - padding, source.Y + source.H / 2.0 - newH / 2.0),
            _ => (source.X + source.W + padding, source.Y),
        };
    }

    private static (DiagramPort? SourcePort, DiagramPort? TargetPort) ResolveConnectPorts(DiagramNode source, DiagramNode target, string direction)
    {
        var sourceSide = direction switch
        {
            "n" => PortSide.Top,
            "e" => PortSide.Right,
            "s" => PortSide.Bottom,
            "w" => PortSide.Left,
            _ => PortSide.Right,
        };
        var targetSide = direction switch
        {
            "n" => PortSide.Bottom,
            "e" => PortSide.Left,
            "s" => PortSide.Top,
            "w" => PortSide.Right,
            _ => PortSide.Left,
        };

        var sp = source.Ports.FirstOrDefault(p => p.Side == sourceSide) ?? source.Ports.FirstOrDefault();
        var tp = target.Ports.FirstOrDefault(p => p.Side == targetSide) ?? target.Ports.FirstOrDefault();
        return (sp, tp);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RerenderCanvas()
    {
        StateHasChanged();
        if (_document is not null)
            await DocumentChanged.InvokeAsync(_document);
    }

    private static DiagramDocument CreateEmptyDocument()
        => new() { Title = "Untitled diagram", Width = 3000, Height = 2000 };

    private static string SanitizeFileName(string name)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "diagram" : name;
    }

    private async Task DownloadFile(string fileName, string mimeType, byte[] data)
    {
        var base64 = Convert.ToBase64String(data);
        await JS.InvokeVoidAsync(
            "tmWireframeDesigner.downloadFile",
            _downloadAnchor, fileName, mimeType, base64);
    }
}
