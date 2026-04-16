using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Static clipboard holding the last copied style and size for Format Painter.</summary>
public static class DiagramClipboard
{
    /// <summary>Copied style snapshot.</summary>
    public static DiagramStyle? Style { get; set; }

    /// <summary>Copied width.</summary>
    public static double? Width { get; set; }

    /// <summary>Copied height.</summary>
    public static double? Height { get; set; }
}
