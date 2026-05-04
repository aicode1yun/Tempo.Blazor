namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Supported color formats for TmColorPicker components.</summary>
public enum ColorFormat
{
    /// <summary>Hexadecimal format (#RRGGBB or #RRGGBBAA).</summary>
    Hex,

    /// <summary>RGB format (rgb(r, g, b)).</summary>
    Rgb,

    /// <summary>RGBA format (rgba(r, g, b, a)).</summary>
    Rgba
}
