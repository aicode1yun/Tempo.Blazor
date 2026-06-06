namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>A declarative stencil definition used to create diagram nodes.</summary>
public sealed class DiagramStencil
{
    /// <summary>Unique stencil identifier (e.g. "uml.class").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable name displayed in the toolbox.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Localization resource key used for the stencil display name.</summary>
    public string NameResourceKey { get; set; } = string.Empty;

    /// <summary>Category used for grouping in the toolbox (e.g. "UML", "BPMN", "General").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Logical stencil set identifier (e.g. "uml25", "bpmn2").</summary>
    public string SetId { get; set; } = string.Empty;

    /// <summary>Localization resource key used for the owning set display name.</summary>
    public string SetNameResourceKey { get; set; } = string.Empty;

    /// <summary>Logical palette identifier within the set (e.g. "uml25.class").</summary>
    public string PaletteId { get; set; } = string.Empty;

    /// <summary>Localization resource key used for the owning palette display name.</summary>
    public string PaletteNameResourceKey { get; set; } = string.Empty;

    /// <summary>Display order of the owning palette within the set.</summary>
    public int PaletteOrder { get; set; }

    /// <summary>Display order within the palette.</summary>
    public int Order { get; set; }

    /// <summary>Search tags for toolbox discovery.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Additional search keywords for toolbox discovery.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>Kind of diagram element created by this stencil.</summary>
    public DiagramStencilKind Kind { get; set; } = DiagramStencilKind.Node;

    /// <summary>Compact SVG icon for the toolbox.</summary>
    public string IconSvg { get; set; } = string.Empty;

    /// <summary>Default width when dropped onto the canvas.</summary>
    public double DefaultWidth { get; set; } = 120;

    /// <summary>Default height when dropped onto the canvas.</summary>
    public double DefaultHeight { get; set; } = 60;

    /// <summary>Default ports generated for nodes created from this stencil.</summary>
    public List<DiagramStencilPortDef> Ports { get; set; } = [];

    /// <summary>
    /// Predefined fixed connection points on the stencil perimeter, draw.io style.
    /// When populated, these render as hover dots and create edges with
    /// <see cref="DiagramConnectionConstraint"/> instead of floating port snapping.
    /// </summary>
    public List<DiagramStencilConnectionPoint> ConnectionPoints { get; set; } = [];

    /// <summary>Declarative layout defining the visual appearance.</summary>
    public DiagramStencilLayout Layout { get; set; } = new();

    /// <summary>Whether nodes created from this stencil support collapse/expand.</summary>
    public bool IsCollapsible { get; set; }

    /// <summary>Whether nodes created from this stencil act as swimlane containers.</summary>
    public bool IsSwimlane { get; set; }

    /// <summary>Whether nodes created from this stencil act as tables.</summary>
    public bool IsTable { get; set; }

    /// <summary>Default edge values when <see cref="Kind"/> is <see cref="DiagramStencilKind.Edge"/>.</summary>
    public DiagramEdgeStencilDefaults? EdgeDefaults { get; set; }

    /// <summary>Default data values for nodes created from this stencil.</summary>
    public Dictionary<string, object> DefaultData { get; set; } = [];

    /// <summary>Origin metadata used to keep built-in stencil libraries license-safe.</summary>
    public DiagramStencilOrigin Origin { get; set; }

    /// <summary>Optional external asset source identifier. Built-in Tempo stencils leave this empty.</summary>
    public string? ExternalAssetSourceId { get; set; }
}
