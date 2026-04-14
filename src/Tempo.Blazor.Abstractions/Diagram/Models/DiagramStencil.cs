namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>A declarative stencil definition used to create diagram nodes.</summary>
public sealed class DiagramStencil
{
    /// <summary>Unique stencil identifier (e.g. "uml.class").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable name displayed in the toolbox.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Category used for grouping in the toolbox (e.g. "UML", "BPMN", "General").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Compact SVG icon for the toolbox.</summary>
    public string IconSvg { get; set; } = string.Empty;

    /// <summary>Default width when dropped onto the canvas.</summary>
    public double DefaultWidth { get; set; } = 120;

    /// <summary>Default height when dropped onto the canvas.</summary>
    public double DefaultHeight { get; set; } = 60;

    /// <summary>Default ports generated for nodes created from this stencil.</summary>
    public List<DiagramStencilPortDef> Ports { get; set; } = [];

    /// <summary>Declarative layout defining the visual appearance.</summary>
    public DiagramStencilLayout Layout { get; set; } = new();

    /// <summary>Default data values for nodes created from this stencil.</summary>
    public Dictionary<string, object> DefaultData { get; set; } = [];
}
