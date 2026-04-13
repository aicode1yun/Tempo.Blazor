using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Wireframe.Commands;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Top-level wireframe editor component. Composes the canvas, toolbox, properties panel,
/// minimap, and toolbar into a fully working editor.
///
/// <para>Creates and owns a <see cref="WireframeCommandStack"/> and exposes it to all child
/// components via <c>CascadingValue</c>. Each editor instance has an isolated undo history,
/// so multiple editors on the same page work independently.</para>
///
/// <para>Supports JSON import/export, SVG export, undo/redo, zoom, and a collapsible
/// toolbox / properties panel. All write operations are guarded by <see cref="ReadOnly"/>.</para>
/// </summary>
public partial class TmWireframeEditor : ComponentBase, IDisposable
{
    // ── DI ────────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ────────────────────────────────────────────────────────────

    /// <summary>Document being edited. Bind with <c>@bind-Document</c>.</summary>
    [Parameter] public WireframeDocument? Document { get; set; }

    /// <summary>Raised after every mutation.</summary>
    [Parameter] public EventCallback<WireframeDocument> DocumentChanged { get; set; }

    /// <summary>Prevent all editing interactions.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Show the component toolbox panel on the left.</summary>
    [Parameter] public bool ShowToolbox { get; set; } = true;

    /// <summary>Show the properties panel on the right.</summary>
    [Parameter] public bool ShowPropertiesPanel { get; set; } = true;

    /// <summary>Show the minimap overlay in the bottom-right corner of the canvas.</summary>
    [Parameter] public bool ShowMinimap { get; set; } = true;

    /// <summary>Show the top toolbar (title, zoom, undo/redo, export/import).</summary>
    [Parameter] public bool ShowToolbar { get; set; } = true;

    /// <summary>Show grid lines on the canvas.</summary>
    [Parameter] public bool ShowGrid { get; set; } = true;

    /// <summary>Snap-to-grid cell size. 0 = disabled.</summary>
    [Parameter] public int GridSize { get; set; } = 8;

    /// <summary>Additional CSS class on the editor root.</summary>
    [Parameter] public string? Class { get; set; }

    // ── Child component refs ──────────────────────────────────────────────────

    private TmWireframeDesignerCanvas? _canvas;
    private TmWireframeMinimap?        _minimap;
    private ElementReference           _downloadAnchor;

    // ── Command stack (owned by this component, cascaded down) ────────────────

    private readonly WireframeCommandStack _commandStack = new();

    // ── Derived / UI state ────────────────────────────────────────────────────

    // Internal copy of Document: keeps a stable reference for child components.
    private WireframeDocument? _document;

    private string[]         _selectedIds       = [];
    private MinimapViewport? _minimapViewport;

    private double _zoomLevel = 1.0;
    private string _zoomLabel => $"{(int)Math.Round(_zoomLevel * 100)}%";

    private bool _toolboxCollapsed;
    private bool _propsCollapsed;
    private bool _exportMenuOpen;
    private string? _importError;

    // Tool mode: 'select' | 'pan'
    private string _toolMode = "select";

    // Canvas size editing
    private bool   _canvasSizeEditing;
    private string _canvasWInput = "";
    private string _canvasHInput = "";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        _commandStack.OnStackChanged += OnStackChanged;
        _document = Document ?? CreateEmptyDocument();
    }

    protected override void OnParametersSet()
    {
        // If the caller supplies a new Document reference, adopt it and clear the stack.
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

    // ── Stack change → re-render ──────────────────────────────────────────────

    private void OnStackChanged() => InvokeAsync(StateHasChanged);

    // ── Document change propagation ───────────────────────────────────────────

    private async Task OnDocumentChanged(WireframeDocument doc)
    {
        _document = doc;
        await DocumentChanged.InvokeAsync(doc);
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    private void OnSelectionChanged(string[] ids)
    {
        _selectedIds = ids;
        StateHasChanged();
    }

    // ── Toolbar: Tool mode (Select / Pan) ────────────────────────────────────

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

    // ── Toolbar: Canvas size ──────────────────────────────────────────────────

    private void OpenCanvasSize()
    {
        _canvasWInput = ((int)(_document?.Width  ?? 1280)).ToString();
        _canvasHInput = ((int)(_document?.Height ?? 800)).ToString();
        _canvasSizeEditing = true;
    }

    private async Task ApplyCanvasSize()
    {
        _canvasSizeEditing = false;
        if (_document is null || ReadOnly) return;
        if (!double.TryParse(_canvasWInput, out var w) || w < 100) return;
        if (!double.TryParse(_canvasHInput, out var h) || h < 100) return;
        if (Math.Abs(w - _document.Width) < 0.5 && Math.Abs(h - _document.Height) < 0.5) return;

        _commandStack.Push(new Commands.ResizeCanvasCommand(_document, _document.Width, _document.Height, w, h));
        if (_canvas is not null) await _canvas.UpdateCanvasSize(w, h);
        await OnDocumentChanged(_document);
    }

    // ── Toolbar: Title ────────────────────────────────────────────────────────

    private async Task OnTitleChanged(ChangeEventArgs e)
    {
        if (_document is null || ReadOnly) return;
        _document.Title = e.Value?.ToString() ?? "";
        await OnDocumentChanged(_document);
    }

    // ── Toolbar: Undo / Redo ──────────────────────────────────────────────────

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

    // ── Toolbar: Zoom ─────────────────────────────────────────────────────────

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
            await _canvas.FitToView();
    }

    // ── Toolbar: Export ───────────────────────────────────────────────────────

    private void ToggleExportMenu() => _exportMenuOpen = !_exportMenuOpen;
    private void CloseExportMenu()  => _exportMenuOpen = false;

    private async Task ExportJson()
    {
        _exportMenuOpen = false;
        if (_document is null) return;
        var json     = WireframeSerializer.Serialize(_document);
        var fileName = SanitizeFileName(_document.Title) + ".wireframe.json";
        await DownloadFile(fileName, "application/json", System.Text.Encoding.UTF8.GetBytes(json));
    }

    private async Task ExportSvg()
    {
        _exportMenuOpen = false;
        if (_canvas is null) return;
        var svg      = await _canvas.ExportSvg();
        var fileName = SanitizeFileName(_document?.Title ?? "wireframe") + ".svg";
        await DownloadFile(fileName, "image/svg+xml", System.Text.Encoding.UTF8.GetBytes(svg));
    }

    // ── Toolbar: Import ───────────────────────────────────────────────────────

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

            if (!WireframeSerializer.TryDeserialize(json, out var doc) || doc is null)
            {
                _importError = "Failed to import: the file is not a valid wireframe JSON.";
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
            _importError = $"Failed to import: {ex.Message}";
        }
    }

    // ── Toolbar: Read-only toggle ─────────────────────────────────────────────

    private void ToggleReadOnly() => ReadOnly = !ReadOnly;

    // ── Toolbox keyboard activation ───────────────────────────────────────────

    private async Task OnToolboxActivated(string componentType)
    {
        if (_document is null || _canvas is null || ReadOnly) return;

        // Add at a sensible default position in the visible area
        var x   = 100.0;
        var y   = 100.0;
        var def = null as WireframeComponentDef;    // resolved inside canvas

        var el = WireframeDocumentExtensions.NewElement(
            componentType, x, y, 160, 40);
        el.ZIndex = _document.Elements.Count > 0
            ? _document.Elements.Max(e => e.ZIndex) + 1 : 0;

        await _canvas.AddElement(el);
        await _canvas.SelectElements(el.Id);
        OnSelectionChanged([el.Id]);
    }

    // ── Minimap navigation ────────────────────────────────────────────────────

    private async Task OnMinimapNavigate(MinimapNavigateArgs args)
    {
        if (_canvas is null) return;
        // ScrollTo the centre point by zooming to current level with pan offset
        // We reuse zoomTo which accepts scale; navigate via JS resetView + offset
        await JS.InvokeVoidAsync(
            "tmWireframeDesigner.scrollTo",
            _canvas.GetSvgRef(),
            args.CentreX, args.CentreY);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RerenderCanvas()
    {
        StateHasChanged();
        if (_document is not null)
            await DocumentChanged.InvokeAsync(_document);
    }

    private static WireframeDocument CreateEmptyDocument() =>
        new() { Title = "Untitled wireframe", Width = 1280, Height = 800 };

    private static string SanitizeFileName(string name)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "wireframe" : name;
    }

    private async Task DownloadFile(string fileName, string mimeType, byte[] data)
    {
        var base64 = Convert.ToBase64String(data);
        await JS.InvokeVoidAsync(
            "tmWireframeDesigner.downloadFile",
            _downloadAnchor, fileName, mimeType, base64);
    }
}
