using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.Inputs;

/// <summary>
/// A color palette that displays predefined colors in a grid.
/// Users can click a swatch to select a color.
/// </summary>
public partial class TmColorPalette
{
    // ── Default palette ──────────────────────────────────────────
    private static readonly string[] DefaultColors =
    [
        "#000000", "#1A1A1A", "#333333", "#4D4D4D", "#666666", "#808080", "#999999", "#B3B3B3",
        "#FF0000", "#FF4D4D", "#FF6666", "#FF8080", "#FF9999", "#FFB3B3", "#FFCCCC", "#FFE6E6",
        "#00FF00", "#4DFF4D", "#66FF66", "#80FF80", "#99FF99", "#B3FFB3", "#CCFFCC", "#E6FFE6",
        "#0000FF", "#4D4DFF", "#6666FF", "#8080FF", "#9999FF", "#B3B3FF", "#CCCCFF", "#E6E6FF",
        "#FFFF00", "#FFFF4D", "#FFFF66", "#FFFF80", "#FFFF99", "#FFFFB3", "#FFFFCC", "#FFFFE6",
        "#FF00FF", "#FF4DFF", "#FF66FF", "#FF80FF", "#FF99FF", "#FFB3FF", "#FFCCFF", "#FFE6FF",
        "#00FFFF", "#4DFFFF", "#66FFFF", "#80FFFF", "#99FFFF", "#B3FFFF", "#CCFFFF", "#E6FFFF",
        "#FFFFFF", "#1A1AFF", "#331AFF", "#4D1AFF", "#661AFF", "#801AFF", "#991AFF", "#B31AFF",
    ];

    // ── Parameters ───────────────────────────────────────────────

    /// <summary>The currently selected color value.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Event fired when a color is selected.</summary>
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Predefined colors to display. Defaults to a standard palette.</summary>
    [Parameter] public IReadOnlyList<string>? Colors { get; set; }

    /// <summary>Number of columns in the grid. Default 8.</summary>
    [Parameter] public int Columns { get; set; } = 8;

    /// <summary>Shows a clear button. Default true.</summary>
    [Parameter] public bool ShowClearButton { get; set; } = true;

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional attributes spread onto the wrapper.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Computed ─────────────────────────────────────────────────

    private IReadOnlyList<string> _effectiveColors => Colors ?? DefaultColors;

    private bool IsSelected(string color)
        => string.Equals(color, Value, StringComparison.OrdinalIgnoreCase);

    // ── Actions ──────────────────────────────────────────────────

    private async Task SelectColorAsync(string color)
    {
        Value = color;
        await ValueChanged.InvokeAsync(color);
    }

    private async Task ClearAsync()
    {
        Value = null;
        await ValueChanged.InvokeAsync(string.Empty);
    }
}
