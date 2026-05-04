using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;
using System.Text;

namespace Tempo.Blazor.Components.Inputs;

/// <summary>
/// A signature pad component that captures freehand drawing via pointer events
/// and renders strokes as SVG polylines. Supports customization of stroke color,
/// width, background, and disabled state.
/// </summary>
public partial class TmSignature
{
    private readonly List<Stroke> _strokes = [];
    private Stroke? _currentStroke;
    private bool _isDrawing;

    // ── Parameters ───────────────────────────────────────────────

    /// <summary>The current signature value as an SVG data URL or raw SVG markup.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Event fired when the signature value changes.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Stroke color. Default is <c>#000000</c>.</summary>
    [Parameter] public string StrokeColor { get; set; } = "#000000";

    /// <summary>Stroke width in pixels. Default is <c>2</c>.</summary>
    [Parameter] public double StrokeWidth { get; set; } = 2;

    /// <summary>Background color of the canvas. When null, the canvas is transparent.</summary>
    [Parameter] public string? BackgroundColor { get; set; }

    /// <summary>Canvas width in pixels. Default is <c>400</c>.</summary>
    [Parameter] public int Width { get; set; } = 400;

    /// <summary>Canvas height in pixels. Default is <c>200</c>.</summary>
    [Parameter] public int Height { get; set; } = 200;

    /// <summary>When true, the canvas is read-only and the clear button is hidden.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Whether to show the clear button. Default is <c>true</c>.</summary>
    [Parameter] public bool ShowClearButton { get; set; } = true;

    /// <summary>Additional CSS class for the wrapper element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional attributes spread onto the wrapper element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Lifecycle ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // If Value is provided and strokes are empty, try to parse it
        if (!string.IsNullOrEmpty(Value) && _strokes.Count == 0)
        {
            ParseValue(Value);
        }
    }

    // ── Pointer handlers ─────────────────────────────────────────

    private void OnPointerDown(PointerEventArgs e)
    {
        if (Disabled) return;
        _isDrawing = true;
        _currentStroke = new Stroke(StrokeColor, StrokeWidth)
        {
            PointsBuilder = new StringBuilder()
        };
        _currentStroke.PointsBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0:0.0},{1:0.0}", e.OffsetX, e.OffsetY);
        _strokes.Add(_currentStroke);
    }

    private void OnPointerMove(PointerEventArgs e)
    {
        if (Disabled || !_isDrawing || _currentStroke is null) return;
        _currentStroke.PointsBuilder.AppendFormat(CultureInfo.InvariantCulture, " {0:0.0},{1:0.0}", e.OffsetX, e.OffsetY);
    }

    private async Task OnPointerUp(PointerEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;
        _currentStroke = null;
        await NotifyChangedAsync();
    }

    // ── Actions ──────────────────────────────────────────────────

    private async Task ClearAsync()
    {
        _strokes.Clear();
        _currentStroke = null;
        _isDrawing = false;
        await ValueChanged.InvokeAsync(null);
    }

    // ── Value serialization ──────────────────────────────────────

    private async Task NotifyChangedAsync()
    {
        var svg = BuildSvgString();
        await ValueChanged.InvokeAsync(svg);
    }

    private string BuildSvgString()
    {
        if (_strokes.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Width}\" height=\"{Height}\">");
        if (!string.IsNullOrEmpty(BackgroundColor))
        {
            sb.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"{BackgroundColor}\"/>");
        }
        foreach (var stroke in _strokes)
        {
            sb.AppendLine($"<polyline points=\"{stroke.Points}\" fill=\"none\" stroke=\"{stroke.Color}\" stroke-width=\"{stroke.Width.ToString("0.0", CultureInfo.InvariantCulture)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
        }
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private void ParseValue(string value)
    {
        // Minimal parsing: if it contains polylines, we could parse them back.
        // For production, full SVG parsing would be needed. Here we keep it simple:
        // if the value starts with <svg we accept it as-is but we don't rebuild
        // the stroke list from it to avoid complexity. The user can re-render
        // the raw SVG via a separate mechanism if needed.
    }

    private string GetCanvasStyle()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(BackgroundColor))
        {
            sb.Append($"background-color: {BackgroundColor};");
        }
        return sb.ToString();
    }

    // ── Inner types ──────────────────────────────────────────────

    private sealed class Stroke(string color, double width)
    {
        public string Color { get; } = color;
        public double Width { get; } = width;
        public StringBuilder PointsBuilder { get; set; } = new();
        public string Points => PointsBuilder.ToString();
    }
}
