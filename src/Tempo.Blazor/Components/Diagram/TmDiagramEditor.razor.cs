using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
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

    // ── Context menu ─────────────────────────────────────────────────────────

    private bool _contextMenuOpen;
    private string? _contextMenuNodeId;
    private double _contextMenuScreenX;
    private double _contextMenuScreenY;

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

    private async Task HandleEdgeRoutingChanged((string EdgeId, string Routing) args)
    {
        if (_document is null || _canvas is null || ReadOnly) return;
        var edge = _document.Edges.FirstOrDefault(e => e.Id == args.EdgeId);
        if (edge is null) return;

        if (args.Routing == "orthogonal")
        {
            edge.Waypoints = await _canvas.ComputeOrthogonalWaypointsAsync(edge);
        }
        else
        {
            edge.Waypoints.Clear();
        }

        await OnDocumentChanged(_document);
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
