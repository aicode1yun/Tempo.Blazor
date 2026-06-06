using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Stencils;

/// <summary>Provides Tempo-original ArchiMate 3.2 stencil definitions.</summary>
public sealed class Archimate3DiagramStencilProvider : IDiagramStencilProvider
{
    private const string SetId = "archimate3";
    private const string SetNameResourceKey = "DiagramStencilSet_Archimate3";
    private const string Category = "ArchiMate 3.2";

    /// <inheritdoc />
    public int Priority => 10;

    /// <inheritdoc />
    public IEnumerable<DiagramStencilSet> GetStencilSets()
    {
        return
        [
            new()
            {
                Id = SetId,
                Name = "ArchiMate 3.2",
                NameResourceKey = SetNameResourceKey,
                Stencils =
                [
                    .. BusinessPalette(),
                    .. ApplicationPalette(),
                    .. TechnologyPalette(),
                    .. MotivationPalette(),
                    .. StrategyPalette(),
                    .. RelationshipPalette()
                ]
            }
        ];
    }

    private static IEnumerable<DiagramStencil> BusinessPalette()
    {
        const string fill = "#fff2b8";
        yield return Element("archimate3.business.actor", "Business Actor", "DiagramStencil_Archimate3BusinessActor", "archimate3.business", "DiagramStencilPalette_Archimate3Business", 0, 0, "rectangle", fill, "tm-archimate3-marker-actor", ActorMarker(), "Business Actor");
        yield return Element("archimate3.business.role", "Business Role", "DiagramStencil_Archimate3BusinessRole", "archimate3.business", "DiagramStencilPalette_Archimate3Business", 0, 1, "rectangle", fill, "tm-archimate3-marker-role", BadgeMarker("R"), "Business Role");
        yield return Element("archimate3.business.process", "Business Process", "DiagramStencil_Archimate3BusinessProcess", "archimate3.business", "DiagramStencilPalette_Archimate3Business", 0, 2, "rounded", fill, "tm-archimate3-marker-process", ChevronMarker(), "Business Process");
        yield return Element("archimate3.business.function", "Business Function", "DiagramStencil_Archimate3BusinessFunction", "archimate3.business", "DiagramStencilPalette_Archimate3Business", 0, 3, "rectangle", fill, "tm-archimate3-marker-function", FunctionMarker(), "Business Function");
        yield return Element("archimate3.business.service", "Business Service", "DiagramStencil_Archimate3BusinessService", "archimate3.business", "DiagramStencilPalette_Archimate3Business", 0, 4, "rounded", fill, "tm-archimate3-marker-service", ServiceMarker(), "Business Service");
        yield return Element("archimate3.business.object", "Business Object", "DiagramStencil_Archimate3BusinessObject", "archimate3.business", "DiagramStencilPalette_Archimate3Business", 0, 5, "rectangle", fill, "tm-archimate3-marker-object", ObjectMarker(), "Business Object");
    }

    private static IEnumerable<DiagramStencil> ApplicationPalette()
    {
        const string fill = "#cfe8ff";
        yield return Element("archimate3.application.component", "Application Component", "DiagramStencil_Archimate3ApplicationComponent", "archimate3.application", "DiagramStencilPalette_Archimate3Application", 1, 0, "rectangle", fill, "tm-archimate3-marker-component", ComponentMarker(), "Application Component");
        yield return Element("archimate3.application.interface", "Application Interface", "DiagramStencil_Archimate3ApplicationInterface", "archimate3.application", "DiagramStencilPalette_Archimate3Application", 1, 1, "ellipse", fill, "tm-archimate3-marker-interface", InterfaceMarker(), "Application Interface", 118, 82);
        yield return Element("archimate3.application.function", "Application Function", "DiagramStencil_Archimate3ApplicationFunction", "archimate3.application", "DiagramStencilPalette_Archimate3Application", 1, 2, "rectangle", fill, "tm-archimate3-marker-function", FunctionMarker(), "Application Function");
        yield return Element("archimate3.application.process", "Application Process", "DiagramStencil_Archimate3ApplicationProcess", "archimate3.application", "DiagramStencilPalette_Archimate3Application", 1, 3, "rounded", fill, "tm-archimate3-marker-process", ChevronMarker(), "Application Process");
        yield return Element("archimate3.application.service", "Application Service", "DiagramStencil_Archimate3ApplicationService", "archimate3.application", "DiagramStencilPalette_Archimate3Application", 1, 4, "rounded", fill, "tm-archimate3-marker-service", ServiceMarker(), "Application Service");
        yield return Element("archimate3.application.data-object", "Data Object", "DiagramStencil_Archimate3DataObject", "archimate3.application", "DiagramStencilPalette_Archimate3Application", 1, 5, "document", fill, "tm-archimate3-marker-data-object", DocumentMarker(), "Data Object");
    }

    private static IEnumerable<DiagramStencil> TechnologyPalette()
    {
        const string fill = "#d9f2d0";
        yield return Element("archimate3.technology.node", "Node", "DiagramStencil_Archimate3TechnologyNode", "archimate3.technology", "DiagramStencilPalette_Archimate3Technology", 2, 0, "rectangle", fill, "tm-archimate3-marker-node", NodeMarker(), "Node");
        yield return Element("archimate3.technology.device", "Device", "DiagramStencil_Archimate3Device", "archimate3.technology", "DiagramStencilPalette_Archimate3Technology", 2, 1, "rectangle", fill, "tm-archimate3-marker-device", DeviceMarker(), "Device");
        yield return Element("archimate3.technology.system-software", "System Software", "DiagramStencil_Archimate3SystemSoftware", "archimate3.technology", "DiagramStencilPalette_Archimate3Technology", 2, 2, "rectangle", fill, "tm-archimate3-marker-system-software", StackMarker(), "System Software");
        yield return Element("archimate3.technology.interface", "Technology Interface", "DiagramStencil_Archimate3TechnologyInterface", "archimate3.technology", "DiagramStencilPalette_Archimate3Technology", 2, 3, "ellipse", fill, "tm-archimate3-marker-interface", InterfaceMarker(), "Technology Interface", 118, 82);
        yield return Element("archimate3.technology.path", "Path", "DiagramStencil_Archimate3Path", "archimate3.technology", "DiagramStencilPalette_Archimate3Technology", 2, 4, "rounded", fill, "tm-archimate3-marker-path", PathMarker(), "Path");
        yield return Element("archimate3.technology.artifact", "Artifact", "DiagramStencil_Archimate3Artifact", "archimate3.technology", "DiagramStencilPalette_Archimate3Technology", 2, 5, "document", fill, "tm-archimate3-marker-artifact", DocumentMarker(), "Artifact");
    }

    private static IEnumerable<DiagramStencil> MotivationPalette()
    {
        const string fill = "#eadcff";
        yield return Element("archimate3.motivation.stakeholder", "Stakeholder", "DiagramStencil_Archimate3Stakeholder", "archimate3.motivation", "DiagramStencilPalette_Archimate3Motivation", 3, 0, "rectangle", fill, "tm-archimate3-marker-stakeholder", ActorMarker(), "Stakeholder");
        yield return Element("archimate3.motivation.driver", "Driver", "DiagramStencil_Archimate3Driver", "archimate3.motivation", "DiagramStencilPalette_Archimate3Motivation", 3, 1, "rounded", fill, "tm-archimate3-marker-driver", BadgeMarker("D"), "Driver");
        yield return Element("archimate3.motivation.assessment", "Assessment", "DiagramStencil_Archimate3Assessment", "archimate3.motivation", "DiagramStencilPalette_Archimate3Motivation", 3, 2, "rectangle", fill, "tm-archimate3-marker-assessment", AssessmentMarker(), "Assessment");
        yield return Element("archimate3.motivation.goal", "Goal", "DiagramStencil_Archimate3Goal", "archimate3.motivation", "DiagramStencilPalette_Archimate3Motivation", 3, 3, "rounded", fill, "tm-archimate3-marker-goal", GoalMarker(), "Goal");
        yield return Element("archimate3.motivation.outcome", "Outcome", "DiagramStencil_Archimate3Outcome", "archimate3.motivation", "DiagramStencilPalette_Archimate3Motivation", 3, 4, "ellipse", fill, "tm-archimate3-marker-outcome", TargetMarker(), "Outcome", 124, 86);
        yield return Element("archimate3.motivation.principle", "Principle", "DiagramStencil_Archimate3Principle", "archimate3.motivation", "DiagramStencilPalette_Archimate3Motivation", 3, 5, "rectangle", fill, "tm-archimate3-marker-principle", BadgeMarker("P"), "Principle");
        yield return Element("archimate3.motivation.requirement", "Requirement", "DiagramStencil_Archimate3Requirement", "archimate3.motivation", "DiagramStencilPalette_Archimate3Motivation", 3, 6, "rectangle", fill, "tm-archimate3-marker-requirement", RequirementMarker(), "Requirement");
        yield return Element("archimate3.motivation.constraint", "Constraint", "DiagramStencil_Archimate3Constraint", "archimate3.motivation", "DiagramStencilPalette_Archimate3Motivation", 3, 7, "rectangle", fill, "tm-archimate3-marker-constraint", ConstraintMarker(), "Constraint");
    }

    private static IEnumerable<DiagramStencil> StrategyPalette()
    {
        const string fill = "#ffe1c2";
        yield return Element("archimate3.strategy.resource", "Resource", "DiagramStencil_Archimate3Resource", "archimate3.strategy", "DiagramStencilPalette_Archimate3Strategy", 4, 0, "rectangle", fill, "tm-archimate3-marker-resource", ResourceMarker(), "Resource");
        yield return Element("archimate3.strategy.capability", "Capability", "DiagramStencil_Archimate3Capability", "archimate3.strategy", "DiagramStencilPalette_Archimate3Strategy", 4, 1, "rounded", fill, "tm-archimate3-marker-capability", CapabilityMarker(), "Capability");
        yield return Element("archimate3.strategy.value-stream", "Value Stream", "DiagramStencil_Archimate3ValueStream", "archimate3.strategy", "DiagramStencilPalette_Archimate3Strategy", 4, 2, "hexagon", fill, "tm-archimate3-marker-value-stream", ValueStreamMarker(), "Value Stream", 160, 80);
    }

    private static IEnumerable<DiagramStencil> RelationshipPalette()
    {
        yield return Edge("archimate3.relationship.association", "Association", "DiagramStencil_Archimate3Association", 0, "archimate-association", "none", null);
        yield return Edge("archimate3.relationship.triggering", "Triggering", "DiagramStencil_Archimate3Triggering", 1, "archimate-triggering", "block", true);
        yield return Edge("archimate3.relationship.flow", "Flow", "DiagramStencil_Archimate3Flow", 2, "archimate-flow", "open", false, dashPattern: "dashed");
        yield return Edge("archimate3.relationship.access", "Access", "DiagramStencil_Archimate3Access", 3, "archimate-access", "open", false, dashPattern: "dotted");
        yield return Edge("archimate3.relationship.serving", "Serving", "DiagramStencil_Archimate3Serving", 4, "archimate-serving", "open", false);
        yield return Edge("archimate3.relationship.realization", "Realization", "DiagramStencil_Archimate3Realization", 5, "archimate-realization", "open", false, dashPattern: "dashed");
        yield return Edge("archimate3.relationship.assignment", "Assignment", "DiagramStencil_Archimate3Assignment", 6, "archimate-assignment", "block", true);
        yield return Edge("archimate3.relationship.aggregation", "Aggregation", "DiagramStencil_Archimate3Aggregation", 7, "archimate-aggregation", "none", null, startArrow: "diamond", startArrowFill: false);
        yield return Edge("archimate3.relationship.composition", "Composition", "DiagramStencil_Archimate3Composition", 8, "archimate-composition", "none", null, startArrow: "diamond", startArrowFill: true);
        yield return Edge("archimate3.relationship.specialization", "Specialization", "DiagramStencil_Archimate3Specialization", 9, "archimate-specialization", "open", false);
        yield return Edge("archimate3.relationship.influence", "Influence", "DiagramStencil_Archimate3Influence", 10, "archimate-influence", "open", false, dashPattern: "dashed");
    }

    private static DiagramStencil Element(
        string id,
        string name,
        string nameResourceKey,
        string paletteId,
        string paletteNameResourceKey,
        int paletteOrder,
        int order,
        string backgroundShape,
        string fill,
        string markerClass,
        string markerSvg,
        string defaultLabel,
        double width = 148,
        double height = 76)
    {
        var shapeSvg = ShapeSvg(backgroundShape, markerClass, markerSvg);
        return new()
        {
            Id = id,
            Name = name,
            NameResourceKey = nameResourceKey,
            Category = Category,
            SetId = SetId,
            SetNameResourceKey = SetNameResourceKey,
            PaletteId = paletteId,
            PaletteNameResourceKey = paletteNameResourceKey,
            PaletteOrder = paletteOrder,
            Order = order,
            Kind = DiagramStencilKind.Node,
            Origin = DiagramStencilOrigin.TempoOriginal,
            IconSvg = IconSvg(backgroundShape, markerSvg),
            DefaultWidth = width,
            DefaultHeight = height,
            Tags = ["archimate", "archimate3", "archimate32", "enterprise-architecture"],
            Keywords = [name, paletteId, "ArchiMate 3.2", "3.2", "architecture", "view"],
            Ports = CardinalPorts(),
            ConnectionPoints = CardinalConnectionPoints(),
            Layout = new()
            {
                BackgroundShape = backgroundShape,
                ShapeSvg = shapeSvg,
                Fill = fill,
                Stroke = "#1f2937",
                StrokeWidth = 1.5,
                Sections = [TextSection("label", defaultLabel)]
            },
            DefaultData = new() { ["label"] = defaultLabel }
        };
    }

    private static DiagramStencil Edge(
        string id,
        string name,
        string nameResourceKey,
        int order,
        string connectorType,
        string endArrow,
        bool? endArrowFill,
        string? dashPattern = null,
        string startArrow = "none",
        bool? startArrowFill = null)
        => new()
        {
            Id = id,
            Name = name,
            NameResourceKey = nameResourceKey,
            Category = Category,
            SetId = SetId,
            SetNameResourceKey = SetNameResourceKey,
            PaletteId = "archimate3.relationships",
            PaletteNameResourceKey = "DiagramStencilPalette_Archimate3Relationships",
            PaletteOrder = 5,
            Order = order,
            Kind = DiagramStencilKind.Edge,
            Origin = DiagramStencilOrigin.TempoOriginal,
            IconSvg = EdgeIcon(startArrow, endArrow, dashPattern),
            Tags = ["archimate", "archimate3", "archimate32", "relationship"],
            Keywords = [name, connectorType, "ArchiMate 3.2", "3.2", "architecture"],
            EdgeDefaults = new()
            {
                Routing = "straight",
                ConnectorType = connectorType,
                Shape = "connector",
                StartArrow = startArrow,
                EndArrow = endArrow,
                StartArrowFill = startArrowFill,
                EndArrowFill = endArrowFill,
                Style = dashPattern is null ? null : new DiagramStyle { StrokeDashPattern = dashPattern }
            }
        };

    private static DiagramStencilSection TextSection(string dataKey, string defaultText)
        => new()
        {
            Type = "text",
            DataKey = dataKey,
            DefaultText = defaultText,
            Padding = 10,
            TextStyle = new()
            {
                TextAlign = StencilTextAlign.Center,
                FontSize = 12,
                IsBold = false
            }
        };

    private static string ShapeSvg(string backgroundShape, string markerClass, string markerSvg)
    {
        var bg = backgroundShape switch
        {
            "rectangle" or "rounded" or "ellipse" or "hexagon" => string.Empty,
            "document" => "<path d='M0 0 H78 L100 22 V100 H0 Z' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/><path d='M78 0 V22 H100' fill='none' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>",
            _ => "<rect x='0' y='0' width='100' height='100' rx='3' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>"
        };
        return $"{bg}<g class='{markerClass}' fill='none' stroke='var(--stencil-stroke)' stroke-width='2.6' stroke-linecap='round' stroke-linejoin='round' vector-effect='non-scaling-stroke'>{markerSvg}</g>";
    }

    private static string IconSvg(string backgroundShape, string markerSvg)
    {
        var bg = backgroundShape switch
        {
            "rounded" => "<rect x='4' y='8' width='24' height='16' rx='5' fill='none' stroke='currentColor' stroke-width='2'/>",
            "ellipse" => "<ellipse cx='16' cy='16' rx='12' ry='9' fill='none' stroke='currentColor' stroke-width='2'/>",
            "document" => "<path d='M7 5 H22 L27 10 V27 H7 Z M22 5 V10 H27' fill='none' stroke='currentColor' stroke-width='2' stroke-linejoin='round'/>",
            "hexagon" => "<polygon points='9,6 23,6 29,16 23,26 9,26 3,16' fill='none' stroke='currentColor' stroke-width='2'/>",
            _ => "<rect x='4' y='7' width='24' height='18' rx='2' fill='none' stroke='currentColor' stroke-width='2'/>"
        };
        return $"{bg}<g transform='translate(-70 -70) scale(.9)' fill='none' stroke='currentColor' stroke-width='3' stroke-linecap='round' stroke-linejoin='round'>{markerSvg}</g>";
    }

    private static string ActorMarker()
        => "<circle cx='86' cy='17' r='5'/><path d='M86 22 V36 M76 28 H96 M78 48 L86 36 L94 48'/>";

    private static string ComponentMarker()
        => "<rect x='78' y='14' width='16' height='18' rx='2'/><path d='M74 19 H80 M74 27 H80'/>";

    private static string NodeMarker()
        => "<path d='M75 18 L86 10 L97 18 V34 L86 42 L75 34 Z M75 18 L86 26 L97 18 M86 26 V42'/>";

    private static string DeviceMarker()
        => "<rect x='74' y='13' width='22' height='28' rx='3'/><path d='M81 46 H89 M78 41 H92'/>";

    private static string StackMarker()
        => "<rect x='75' y='14' width='22' height='10' rx='1'/><rect x='75' y='27' width='22' height='10' rx='1'/><path d='M80 19 H83 M80 32 H83'/>";

    private static string InterfaceMarker()
        => "<circle cx='86' cy='25' r='12'/><path d='M98 25 H104'/>";

    private static string PathMarker()
        => "<path d='M74 32 C80 16 92 44 100 24'/><path d='M95 24 H100 V29'/>";

    private static string DocumentMarker()
        => "<path d='M74 12 H91 L99 20 V44 H74 Z M91 12 V20 H99'/>";

    private static string FunctionMarker()
        => "<path d='M76 18 H96 M76 26 H96 M76 34 H88'/>";

    private static string ChevronMarker()
        => "<path d='M75 19 L87 25 L75 31 M87 19 L99 25 L87 31'/>";

    private static string ServiceMarker()
        => "<path d='M76 24 C78 14 94 14 96 24 C102 25 102 38 94 38 H80 C72 38 71 27 76 24 Z'/>";

    private static string ObjectMarker()
        => "<path d='M76 14 H92 L99 21 V43 H76 Z M92 14 V21 H99'/>";

    private static string BadgeMarker(string text)
        => $"<circle cx='86' cy='26' r='14'/><text x='86' y='31' text-anchor='middle' font-size='14' fill='var(--stencil-stroke)' stroke='none'>{text}</text>";

    private static string AssessmentMarker()
        => "<path d='M74 38 L84 16 L94 38 Z'/><path d='M84 24 V31 M84 36 H84.1'/>";

    private static string GoalMarker()
        => "<circle cx='86' cy='26' r='16'/><circle cx='86' cy='26' r='9'/><circle cx='86' cy='26' r='3'/>";

    private static string TargetMarker()
        => GoalMarker();

    private static string RequirementMarker()
        => "<path d='M75 16 H97 V42 H75 Z M80 23 H92 M80 30 H92 M80 37 H88'/>";

    private static string ConstraintMarker()
        => "<rect x='76' y='16' width='20' height='24' rx='2'/><path d='M80 20 L92 36 M92 20 L80 36'/>";

    private static string ResourceMarker()
        => "<path d='M77 18 H96 L91 42 H72 Z M80 25 H92 M78 32 H90'/>";

    private static string CapabilityMarker()
        => "<rect x='76' y='17' width='22' height='22' rx='5'/><path d='M82 31 L87 36 L95 23'/>";

    private static string ValueStreamMarker()
        => "<path d='M75 18 H90 L100 26 L90 34 H75 L83 26 Z'/>";

    private static string EdgeIcon(string startArrow, string endArrow, string? dashPattern)
    {
        var dash = dashPattern is "dashed" ? " stroke-dasharray='4 3'" : dashPattern is "dotted" ? " stroke-dasharray='1 3'" : string.Empty;
        var start = startArrow == "diamond" ? "<path d='M4 16 L9 11 L14 16 L9 21 Z' fill='none' stroke='currentColor' stroke-width='2'/>" : string.Empty;
        var end = endArrow == "none" ? string.Empty : "<path d='M23 11 L28 16 L23 21 Z' fill='none' stroke='currentColor' stroke-width='2' stroke-linejoin='round'/>";
        return $"{start}<path d='M5 16 H28' fill='none' stroke='currentColor' stroke-width='2'{dash}/>{end}";
    }

    private static List<DiagramStencilPortDef> CardinalPorts()
        =>
        [
            new() { Name = "top", Side = PortSide.Top, Offset = 0.5, MagnetStrategy = "perimeter" },
            new() { Name = "right", Side = PortSide.Right, Offset = 0.5, MagnetStrategy = "perimeter" },
            new() { Name = "bottom", Side = PortSide.Bottom, Offset = 0.5, MagnetStrategy = "perimeter" },
            new() { Name = "left", Side = PortSide.Left, Offset = 0.5, MagnetStrategy = "perimeter" }
        ];

    private static List<DiagramStencilConnectionPoint> CardinalConnectionPoints()
        =>
        [
            new() { Name = "N", RelativeX = 0.5, RelativeY = 0, Perimeter = true },
            new() { Name = "E", RelativeX = 1, RelativeY = 0.5, Perimeter = true },
            new() { Name = "S", RelativeX = 0.5, RelativeY = 1, Perimeter = true },
            new() { Name = "W", RelativeX = 0, RelativeY = 0.5, Perimeter = true },
            new() { Name = "C", RelativeX = 0.5, RelativeY = 0.5, Perimeter = false }
        ];
}
