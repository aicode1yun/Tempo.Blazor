namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Predefined page sizes for diagram pages.</summary>
public enum DiagramPageSize
{
    /// <summary>Custom dimensions defined by the user.</summary>
    Custom,

    /// <summary>A4 paper size (210 × 297 mm).</summary>
    A4,

    /// <summary>A3 paper size (297 × 420 mm).</summary>
    A3,

    /// <summary>US Letter size (216 × 279 mm).</summary>
    Letter,

    /// <summary>US Legal size (216 × 356 mm).</summary>
    Legal,

    /// <summary>A5 paper size (148 × 210 mm).</summary>
    A5,
}

/// <summary>Page orientation.</summary>
public enum DiagramPageOrientation
{
    /// <summary>Portrait orientation (height >= width).</summary>
    Portrait,

    /// <summary>Landscape orientation (width > height).</summary>
    Landscape,
}
