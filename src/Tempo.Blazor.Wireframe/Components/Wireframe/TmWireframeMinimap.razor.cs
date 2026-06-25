using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Minimap – a scaled-down SVG overview of the wireframe canvas.
///
/// <para>Shows all element bounding boxes as simplified rectangles (no detailed rendering).
/// Selected elements are highlighted in blue. The current viewport is shown as a dashed
/// rectangle that the user can drag to pan the main canvas.</para>
///
/// <para>Integrates with the canvas via <see cref="ViewportChanged"/> /
/// <see cref="NavigateRequested"/> callbacks rather than JS interop so that
/// it can also be used without a live JS canvas (e.g. in tests or read-only previews).</para>
/// </summary>
public partial class TmWireframeMinimap : ComponentBase
{
    // ── Parameters ────────────────────────────────────────────────────────────

    /// <summary>Document to render.</summary>
    [Parameter] public WireframeDocument? Document { get; set; }

    /// <summary>IDs of currently selected elements (highlighted in blue).</summary>
    [Parameter] public string[] SelectedIds { get; set; } = [];

    /// <summary>
    /// Current viewport state – document-space rectangle that is visible on the main canvas.
    /// When null the viewport rect is not shown.
    /// </summary>
    [Parameter] public MinimapViewport? Viewport { get; set; }

    /// <summary>Fixed pixel width of the minimap panel. Height is derived from the canvas aspect ratio.</summary>
    [Parameter] public int Width { get; set; } = 200;

    /// <summary>
    /// Raised when the user clicks or drags the minimap to navigate.
    /// Arguments are the <em>document-space</em> centre point to scroll to.
    /// </summary>
    [Parameter] public EventCallback<MinimapNavigateArgs> NavigateRequested { get; set; }

    /// <summary>Additional CSS class on the wrapper div.</summary>
    [Parameter] public string? Class { get; set; }

    // ── Internal state ────────────────────────────────────────────────────────

    private ElementReference _container;
    private ElementReference _svg;

    // Viewport rect in document-space coordinates (kept in sync with Viewport parameter).
    private (double X, double Y, double W, double H)? _viewportRect;

    // Drag state: dragging the viewport rect
    private bool   _dragging;
    private double _dragStartSvgX, _dragStartSvgY;   // SVG-space pointer at drag start
    private double _dragStartVpX,  _dragStartVpY;    // viewport origin at drag start

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        _viewportRect = Viewport is not null
            ? (Viewport.X, Viewport.Y, Viewport.Width, Viewport.Height)
            : null;
    }

    // ── Scale helpers ─────────────────────────────────────────────────────────

    /// <summary>Returns the uniform scale factor mapping document pixels → minimap pixels.</summary>
    internal double ComputeScale() =>
        Document is null || Document.Width <= 0
            ? 1.0
            : Width / Document.Width;

    // ── Mouse interaction ─────────────────────────────────────────────────────

    private async Task OnSvgMouseDown(MouseEventArgs e)
    {
        if (Document is null) return;

        var svgPt = ClientToSvg(e.OffsetX, e.OffsetY);

        // Hit-test the viewport rect: if click is inside it start a drag, else navigate
        if (_viewportRect is { } vr && IsInsideRect(svgPt.X, svgPt.Y, vr.X, vr.Y, vr.W, vr.H))
        {
            _dragging       = true;
            _dragStartSvgX  = svgPt.X;
            _dragStartSvgY  = svgPt.Y;
            _dragStartVpX   = vr.X;
            _dragStartVpY   = vr.Y;
        }
        else
        {
            // Navigate: emit centre = click point
            await NavigateRequested.InvokeAsync(new MinimapNavigateArgs(svgPt.X, svgPt.Y));
        }
    }

    private async Task OnSvgMouseMove(MouseEventArgs e)
    {
        if (!_dragging || Document is null) return;

        var svgPt = ClientToSvg(e.OffsetX, e.OffsetY);
        var dx    = svgPt.X - _dragStartSvgX;
        var dy    = svgPt.Y - _dragStartSvgY;

        var newX  = _dragStartVpX + dx;
        var newY  = _dragStartVpY + dy;

        // Clamp to document bounds
        if (_viewportRect is { } vr2)
        {
            newX = Math.Clamp(newX, 0, Math.Max(0, Document.Width  - vr2.W));
            newY = Math.Clamp(newY, 0, Math.Max(0, Document.Height - vr2.H));
        }

        // Notify parent: navigate to the centre of the new viewport position
        var vpW = _viewportRect?.W ?? 0;
        var vpH = _viewportRect?.H ?? 0;
        await NavigateRequested.InvokeAsync(new MinimapNavigateArgs(newX + vpW / 2, newY + vpH / 2));
    }

    private void OnSvgMouseUp(MouseEventArgs _)    => _dragging = false;
    private void OnSvgMouseLeave(MouseEventArgs _) => _dragging = false;

    // ── Coordinate conversion ─────────────────────────────────────────────────

    /// <summary>
    /// Converts an offset (CSS-pixel) point on the SVG element to document-space coordinates.
    /// Since the SVG viewBox == document space the only transform needed is the scale factor.
    /// </summary>
    private (double X, double Y) ClientToSvg(double offsetX, double offsetY)
    {
        var scale = ComputeScale();
        if (scale <= 0) return (offsetX, offsetY);
        return (offsetX / scale, offsetY / scale);
    }

    private static bool IsInsideRect(double px, double py, double rx, double ry, double rw, double rh)
        => px >= rx && px <= rx + rw && py >= ry && py <= ry + rh;

    // ── Public API (called by TmWireframeEditor) ──────────────────────────────

    /// <summary>Updates the viewport rectangle from the main canvas pan/zoom state.</summary>
    public void UpdateViewport(double x, double y, double width, double height)
    {
        _viewportRect = (x, y, width, height);
        StateHasChanged();
    }
}

// ── Supporting types ──────────────────────────────────────────────────────────

/// <summary>Current viewport region in document coordinates.</summary>
public sealed class MinimapViewport
{
    /// <summary>Left edge of the viewport in document coordinates.</summary>
    public double X      { get; init; }
    /// <summary>Top edge of the viewport in document coordinates.</summary>
    public double Y      { get; init; }
    /// <summary>Width of the viewport in document coordinates.</summary>
    public double Width  { get; init; }
    /// <summary>Height of the viewport in document coordinates.</summary>
    public double Height { get; init; }

    /// <summary>Initialises a viewport with document-space position and size.</summary>
    public MinimapViewport(double x, double y, double width, double height)
    { X = x; Y = y; Width = width; Height = height; }
}

/// <summary>Navigate-to arguments: document-space centre point.</summary>
public sealed class MinimapNavigateArgs
{
    /// <summary>Horizontal centre of the requested viewport in document coordinates.</summary>
    public double CentreX { get; init; }
    /// <summary>Vertical centre of the requested viewport in document coordinates.</summary>
    public double CentreY { get; init; }

    /// <summary>Initialises navigate args with the requested document-space centre point.</summary>
    public MinimapNavigateArgs(double cx, double cy) { CentreX = cx; CentreY = cy; }
}

