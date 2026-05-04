using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Inputs;

/// <summary>
/// A flat color picker that combines the color gradient, palette, and preview
/// in a single panel.
/// </summary>
public partial class TmFlatColorPicker
{
    private string _currentValue = string.Empty;

    // ── Parameters ───────────────────────────────────────────────

    /// <summary>The current color value.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Event fired when the color value changes.</summary>
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Output format. Default is <see cref="ColorFormat.Hex"/>.</summary>
    [Parameter] public ColorFormat Format { get; set; } = ColorFormat.Hex;

    /// <summary>Whether to show the alpha channel. Default true.</summary>
    [Parameter] public bool ShowAlpha { get; set; } = true;

    /// <summary>Whether to show the color palette. Default true.</summary>
    [Parameter] public bool ShowPalette { get; set; } = true;

    /// <summary>Whether to show the preview row. Default true.</summary>
    [Parameter] public bool ShowPreview { get; set; } = true;

    /// <summary>Whether to show the clear button in the palette. Default true.</summary>
    [Parameter] public bool ShowClearButton { get; set; } = true;

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional attributes spread onto the wrapper.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Lifecycle ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _currentValue = Value ?? string.Empty;
    }

    // ── Event handlers ───────────────────────────────────────────

    private async Task OnGradientValueChanged(string value)
    {
        _currentValue = value;
        await ValueChanged.InvokeAsync(value);
    }

    private async Task OnPaletteValueChanged(string value)
    {
        _currentValue = value;
        await ValueChanged.InvokeAsync(value);
    }
}
