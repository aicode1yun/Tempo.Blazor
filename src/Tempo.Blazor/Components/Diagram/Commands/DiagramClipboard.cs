using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Static clipboard holding the last copied style, size, nodes and edges for diagram editing.</summary>
public static class DiagramClipboard
{
    /// <summary>Copied style snapshot.</summary>
    public static DiagramStyle? Style { get; set; }

    /// <summary>Copied width.</summary>
    public static double? Width { get; set; }

    /// <summary>Copied height.</summary>
    public static double? Height { get; set; }

    /// <summary>Copied nodes.</summary>
    public static List<DiagramNode> Nodes { get; set; } = [];

    /// <summary>Copied edges.</summary>
    public static List<DiagramEdge> Edges { get; set; } = [];

    /// <summary>Whether the clipboard contains any nodes.</summary>
    public static bool HasNodes => Nodes.Count > 0;
}
