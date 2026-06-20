using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Inputs;

/// <summary>
/// A color gradient picker that allows users to select a color by adjusting
/// saturation/value on a 2D gradient, hue on a slider, and optionally alpha.
/// </summary>
public partial class TmColorGradient
{
    private double _hue;        // 0-360
    private double _saturation; // 0-1
    private double _value;      // 0-1
    private byte _r, _g, _b;
    private double _alpha = 1.0;
    private bool _isDraggingGradient;
    private bool _isDraggingHue;
    private bool _isDraggingAlpha;

    // ── Parameters ───────────────────────────────────────────────

    /// <summary>The current color value (hex, rgb, or rgba).</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Event fired when the color value changes.</summary>
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Output format. Default is <see cref="ColorFormat.Hex"/>.</summary>
    [Parameter] public ColorFormat Format { get; set; } = ColorFormat.Hex;

    /// <summary>Whether to show the alpha slider and input. Default true.</summary>
    [Parameter] public bool ShowAlpha { get; set; } = true;

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional attributes spread onto the wrapper.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Lifecycle ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ParseValue(Value);
    }

    // ── Value parsing / formatting ───────────────────────────────

    private void ParseValue(string? value)
    {
        var (r, g, b, a) = ColorHelper.Parse(value);
        _r = r; _g = g; _b = b; _alpha = a;
        (_hue, _saturation, _value) = ColorHelper.RgbToHsv(_r, _g, _b);
        UpdateHueColor();
    }

    private string _hueColor = "#ff0000";

    private void UpdateHueColor()
    {
        var (hr, hg, hb) = ColorHelper.HsvToRgb(_hue, 1, 1);
        _hueColor = ColorHelper.ToHex(hr, hg, hb);
    }

    private string FormatValue()
    {
        return Format switch
        {
            ColorFormat.Rgb => ColorHelper.ToRgb(_r, _g, _b),
            ColorFormat.Rgba => ColorHelper.ToRgba(_r, _g, _b, _alpha),
            _ => ColorHelper.ToHex(_r, _g, _b, ShowAlpha ? _alpha : 1.0)
        };
    }

    private async Task NotifyChangedAsync()
    {
        var formatted = FormatValue();
        await ValueChanged.InvokeAsync(formatted);
    }

    // ── RGB input handlers ───────────────────────────────────────

    private async Task OnRedChanged(ChangeEventArgs e)
    {
        if (byte.TryParse(e.Value?.ToString(), out var v)) { _r = v; }
        UpdateHsvFromRgb();
        await NotifyChangedAsync();
    }

    private async Task OnGreenChanged(ChangeEventArgs e)
    {
        if (byte.TryParse(e.Value?.ToString(), out var v)) { _g = v; }
        UpdateHsvFromRgb();
        await NotifyChangedAsync();
    }

    private async Task OnBlueChanged(ChangeEventArgs e)
    {
        if (byte.TryParse(e.Value?.ToString(), out var v)) { _b = v; }
        UpdateHsvFromRgb();
        await NotifyChangedAsync();
    }

    private async Task OnAlphaInputChanged(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            _alpha = Math.Clamp(v, 0.0, 1.0);
        }
        await NotifyChangedAsync();
    }

    private void UpdateHsvFromRgb()
    {
        (_hue, _saturation, _value) = ColorHelper.RgbToHsv(_r, _g, _b);
        UpdateHueColor();
    }

    // ── Gradient (SV) pointer handlers ───────────────────────────

    private void OnGradientPointerDown(PointerEventArgs e)
    {
        _isDraggingGradient = true;
        UpdateGradientFromPointer(e);
    }

    private void OnGradientPointerMove(PointerEventArgs e)
    {
        if (!_isDraggingGradient) return;
        UpdateGradientFromPointer(e);
    }

    private async Task OnGradientPointerUp(PointerEventArgs e)
    {
        if (!_isDraggingGradient) return;
        _isDraggingGradient = false;
        UpdateGradientFromPointer(e);
        await NotifyChangedAsync();
    }

    private void UpdateGradientFromPointer(PointerEventArgs e)
    {
        // In a real implementation we would use JS interop to get element bounds.
        // For bUnit tests and basic functionality we approximate via offsetX/Y
        // relative to the gradient area. The CSS size is fixed at 200x150.
        const double width = 200;
        const double height = 150;
        _saturation = Math.Clamp(e.OffsetX / width, 0, 1);
        _value = Math.Clamp(1 - (e.OffsetY / height), 0, 1);
        UpdateRgbFromHsv();
    }

    // ── Hue slider pointer handlers ──────────────────────────────

    private void OnHuePointerDown(PointerEventArgs e)
    {
        _isDraggingHue = true;
        UpdateHueFromPointer(e);
    }

    private void OnHuePointerMove(PointerEventArgs e)
    {
        if (!_isDraggingHue) return;
        UpdateHueFromPointer(e);
    }

    private async Task OnHuePointerUp(PointerEventArgs e)
    {
        if (!_isDraggingHue) return;
        _isDraggingHue = false;
        UpdateHueFromPointer(e);
        await NotifyChangedAsync();
    }

    private void UpdateHueFromPointer(PointerEventArgs e)
    {
        const double width = 200;
        _hue = Math.Clamp(e.OffsetX / width * 360, 0, 360);
        UpdateHueColor();
        UpdateRgbFromHsv();
    }

    // ── Alpha slider pointer handlers ────────────────────────────

    private void OnAlphaPointerDown(PointerEventArgs e)
    {
        _isDraggingAlpha = true;
        UpdateAlphaFromPointer(e);
    }

    private void OnAlphaPointerMove(PointerEventArgs e)
    {
        if (!_isDraggingAlpha) return;
        UpdateAlphaFromPointer(e);
    }

    private async Task OnAlphaPointerUp(PointerEventArgs e)
    {
        if (!_isDraggingAlpha) return;
        _isDraggingAlpha = false;
        UpdateAlphaFromPointer(e);
        await NotifyChangedAsync();
    }

    private void UpdateAlphaFromPointer(PointerEventArgs e)
    {
        const double width = 200;
        _alpha = Math.Clamp(e.OffsetX / width, 0, 1);
    }

    // ── RGB update from HSV ──────────────────────────────────────

    private void UpdateRgbFromHsv()
    {
        (_r, _g, _b) = ColorHelper.HsvToRgb(_hue, _saturation, _value);
    }
}
