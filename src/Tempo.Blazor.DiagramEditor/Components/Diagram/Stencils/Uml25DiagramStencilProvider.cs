using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Stencils;

/// <summary>Provides Tempo-original UML 2.5 stencil definitions.</summary>
public sealed class Uml25DiagramStencilProvider : IDiagramStencilProvider
{
    private const string SetId = "uml25";
    private const string SetNameResourceKey = "DiagramStencilSet_Uml25";
    private const string Category = "UML 2.5";
    private const string MonoFont = "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace";

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
                Name = "UML 2.5",
                NameResourceKey = SetNameResourceKey,
                Stencils =
                [
                    .. ClassPalette(),
                    .. UseCasePalette(),
                    .. ActivityPalette(),
                    .. SequencePalette(),
                    .. DeploymentPalette(),
                    .. RelationshipPalette()
                ]
            }
        ];
    }

    private static IEnumerable<DiagramStencil> ClassPalette()
    {
        yield return ClassLike(
            "uml25.class",
            "Class",
            "DiagramStencil_Uml25Class",
            order: 0,
            stereotype: null,
            defaultName: "ClassName",
            width: 210,
            height: 170,
            iconSvg: "<rect x='4' y='4' width='24' height='24' rx='1.5' fill='none' stroke='currentColor' stroke-width='2'/><path d='M4 12 H28 M4 20 H28' stroke='currentColor' stroke-width='2'/>");

        yield return ClassLike(
            "uml25.abstract-class",
            "Abstract Class",
            "DiagramStencil_Uml25AbstractClass",
            order: 1,
            stereotype: "<<abstract>>",
            defaultName: "AbstractClass",
            width: 210,
            height: 180,
            iconSvg: "<rect x='4' y='4' width='24' height='24' rx='1.5' fill='none' stroke='currentColor' stroke-width='2'/><path d='M4 13 H28 M4 21 H28' stroke='currentColor' stroke-width='2'/><path d='M10 9 H22' stroke='currentColor' stroke-width='1.5'/>");

        yield return ClassLike(
            "uml25.interface",
            "Interface",
            "DiagramStencil_Uml25Interface",
            order: 2,
            stereotype: "<<interface>>",
            defaultName: "IService",
            width: 210,
            height: 170,
            iconSvg: "<rect x='4' y='4' width='24' height='24' rx='1.5' fill='none' stroke='currentColor' stroke-width='2'/><path d='M4 13 H28 M4 21 H28' stroke='currentColor' stroke-width='2'/><circle cx='23' cy='8' r='3' fill='none' stroke='currentColor' stroke-width='1.5'/>");

        yield return Node(
            "uml25.package",
            "Package",
            "DiagramStencil_Uml25Package",
            paletteId: "uml25.class",
            paletteNameResourceKey: "DiagramStencilPalette_Uml25Class",
            paletteOrder: 0,
            order: 3,
            iconSvg: "<path d='M4 9 H13 L15 13 H28 V28 H4 Z' fill='none' stroke='currentColor' stroke-width='2' stroke-linejoin='round'/>",
            width: 220,
            height: 150,
            shapeSvg: "<path d='M0 12 H32 L38 25 H100 V100 H0 Z' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>",
            sections:
            [
                TextSection("name", "Package", 8, bold: true, align: StencilTextAlign.Left)
            ],
            defaultData: new() { ["name"] = "Package" },
            isCollapsible: true);

        yield return Node(
            "uml25.enumeration",
            "Enumeration",
            "DiagramStencil_Uml25Enumeration",
            paletteId: "uml25.class",
            paletteNameResourceKey: "DiagramStencilPalette_Uml25Class",
            paletteOrder: 0,
            order: 4,
            iconSvg: "<rect x='4' y='4' width='24' height='24' rx='1.5' fill='none' stroke='currentColor' stroke-width='2'/><path d='M4 14 H28 M4 21 H28' stroke='currentColor' stroke-width='2'/><path d='M11 9 H21' stroke='currentColor' stroke-width='1.5'/>",
            width: 190,
            height: 150,
            sections:
            [
                TextSection("stereotype", "<<enumeration>>", 4, align: StencilTextAlign.Center, fontSize: 10, fontFamily: MonoFont),
                TextSection("name", "EnumName", 4, bold: true, align: StencilTextAlign.Center),
                Divider(),
                ListSection("literals", "Value", 7)
            ],
            defaultData: new()
            {
                ["stereotype"] = "<<enumeration>>",
                ["name"] = "EnumName",
                ["literals"] = new[] { "Value1", "Value2", "Value3" }
            });
    }

    private static IEnumerable<DiagramStencil> UseCasePalette()
    {
        yield return Node(
            "uml25.actor",
            "Actor",
            "DiagramStencil_Uml25Actor",
            paletteId: "uml25.usecase",
            paletteNameResourceKey: "DiagramStencilPalette_Uml25UseCase",
            paletteOrder: 1,
            order: 0,
            iconSvg: "<circle cx='16' cy='7' r='3' fill='none' stroke='currentColor' stroke-width='2'/><path d='M16 10 V18 M10 13 H22 M12 26 L16 18 L20 26' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round'/>",
            width: 90,
            height: 125,
            shapeSvg: "<circle cx='50' cy='18' r='10' fill='none' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/><path d='M50 28 V60 M20 42 H80 M25 92 L50 60 L75 92' fill='none' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' stroke-linecap='round' stroke-linejoin='round' vector-effect='non-scaling-stroke'/>",
            sections: [TextSection("name", "Actor", 4, align: StencilTextAlign.Center)],
            defaultData: new() { ["name"] = "Actor" },
            contentPosition: "below",
            preserveAspectRatio: true);

        yield return Node(
            "uml25.use-case",
            "Use Case",
            "DiagramStencil_Uml25UseCase",
            paletteId: "uml25.usecase",
            paletteNameResourceKey: "DiagramStencilPalette_Uml25UseCase",
            paletteOrder: 1,
            order: 1,
            iconSvg: "<ellipse cx='16' cy='16' rx='12' ry='8' fill='none' stroke='currentColor' stroke-width='2'/>",
            width: 145,
            height: 80,
            backgroundShape: "ellipse",
            shapeSvg: "<ellipse cx='50' cy='50' rx='48' ry='32' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>",
            sections: [TextSection("name", "Use Case", 0, align: StencilTextAlign.Center)],
            defaultData: new() { ["name"] = "Use Case" });

        yield return Node(
            "uml25.system-boundary",
            "System Boundary",
            "DiagramStencil_Uml25SystemBoundary",
            paletteId: "uml25.usecase",
            paletteNameResourceKey: "DiagramStencilPalette_Uml25UseCase",
            paletteOrder: 1,
            order: 2,
            iconSvg: "<rect x='4' y='5' width='24' height='23' rx='2' fill='none' stroke='currentColor' stroke-width='2'/><path d='M8 11 H20' stroke='currentColor' stroke-width='2'/>",
            width: 260,
            height: 180,
            shapeSvg: "<rect x='0' y='0' width='100' height='100' rx='3' fill='transparent' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' stroke-dasharray='5 4' vector-effect='non-scaling-stroke'/>",
            sections: [TextSection("name", "System", 8, bold: true, align: StencilTextAlign.Left)],
            defaultData: new() { ["name"] = "System" });

        yield return Note("uml25.note", "DiagramStencil_Uml25Note", "uml25.usecase", "DiagramStencilPalette_Uml25UseCase", 1, 3);
    }

    private static IEnumerable<DiagramStencil> ActivityPalette()
    {
        yield return Node("uml25.activity-initial", "Initial Node", "DiagramStencil_Uml25ActivityInitial", "uml25.activity", "DiagramStencilPalette_Uml25Activity", 2, 0, "<circle cx='16' cy='16' r='9' fill='currentColor'/>", 50, 50, "ellipse", "<circle cx='50' cy='50' r='32' fill='var(--stencil-stroke)'/>", [], []);
        yield return Node("uml25.activity-final", "Final Node", "DiagramStencil_Uml25ActivityFinal", "uml25.activity", "DiagramStencilPalette_Uml25Activity", 2, 1, "<circle cx='16' cy='16' r='10' fill='none' stroke='currentColor' stroke-width='2'/><circle cx='16' cy='16' r='6' fill='currentColor'/>", 54, 54, "ellipse", "<circle cx='50' cy='50' r='36' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/><circle cx='50' cy='50' r='22' fill='var(--stencil-stroke)'/>", [], []);
        yield return Node("uml25.activity-action", "Action", "DiagramStencil_Uml25ActivityAction", "uml25.activity", "DiagramStencilPalette_Uml25Activity", 2, 2, "<rect x='4' y='9' width='24' height='14' rx='5' fill='none' stroke='currentColor' stroke-width='2'/>", 135, 64, "rounded", "<rect x='0' y='0' width='100' height='100' rx='14' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>", [TextSection("label", "Action", 6, align: StencilTextAlign.Center)], new() { ["label"] = "Action" });
        yield return Node("uml25.activity-decision", "Decision", "DiagramStencil_Uml25ActivityDecision", "uml25.activity", "DiagramStencilPalette_Uml25Activity", 2, 3, "<polygon points='16,4 28,16 16,28 4,16' fill='none' stroke='currentColor' stroke-width='2'/>", 90, 90, "diamond", "<polygon points='50,0 100,50 50,100 0,50' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>", [TextSection("guard", "", 2, align: StencilTextAlign.Center)], new() { ["guard"] = "" });
        yield return Node("uml25.activity-fork-join", "Fork / Join", "DiagramStencil_Uml25ActivityForkJoin", "uml25.activity", "DiagramStencilPalette_Uml25Activity", 2, 4, "<rect x='5' y='14' width='22' height='4' rx='1' fill='currentColor'/>", 150, 24, "rectangle", "<rect x='0' y='35' width='100' height='30' rx='2' fill='var(--stencil-stroke)'/>", [], []);
        yield return Node("uml25.activity-object-node", "Object Node", "DiagramStencil_Uml25ActivityObjectNode", "uml25.activity", "DiagramStencilPalette_Uml25Activity", 2, 5, "<rect x='5' y='8' width='22' height='16' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><path d='M10 20 H22' stroke='currentColor' stroke-width='1.5'/>", 130, 60, "rectangle", "<rect x='0' y='0' width='100' height='100' rx='2' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>", [TextSection("name", "Object", 6, align: StencilTextAlign.Center)], new() { ["name"] = "Object" });
    }

    private static IEnumerable<DiagramStencil> SequencePalette()
    {
        yield return Node("uml25.sequence-lifeline", "Lifeline", "DiagramStencil_Uml25SequenceLifeline", "uml25.sequence", "DiagramStencilPalette_Uml25Sequence", 3, 0, "<rect x='5' y='4' width='22' height='9' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><path d='M16 13 V29' stroke='currentColor' stroke-width='2' stroke-dasharray='3 3'/>", 150, 260, "rectangle", "<rect x='12' y='0' width='76' height='18' rx='2' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/><path d='M50 18 V100' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' stroke-dasharray='4 4' vector-effect='non-scaling-stroke'/>", [TextSection("name", "participant", 2, align: StencilTextAlign.Center)], new() { ["name"] = "participant" });
        yield return Node("uml25.sequence-activation", "Activation", "DiagramStencil_Uml25SequenceActivation", "uml25.sequence", "DiagramStencilPalette_Uml25Sequence", 3, 1, "<rect x='13' y='5' width='6' height='24' rx='1' fill='none' stroke='currentColor' stroke-width='2'/>", 22, 100, "rectangle", "<rect x='28' y='0' width='44' height='100' rx='2' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>", [], []);
        yield return Node("uml25.sequence-combined-fragment", "Combined Fragment", "DiagramStencil_Uml25SequenceCombinedFragment", "uml25.sequence", "DiagramStencilPalette_Uml25Sequence", 3, 2, "<rect x='4' y='5' width='24' height='23' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><path d='M4 13 H14 L18 5' stroke='currentColor' stroke-width='1.5' fill='none'/>", 220, 150, "rectangle", "<rect x='0' y='0' width='100' height='100' rx='2' fill='transparent' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/><path d='M0 18 H28 L36 0' fill='none' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>", [TextSection("operator", "alt", 4, bold: true, align: StencilTextAlign.Left, fontFamily: MonoFont)], new() { ["operator"] = "alt" });
        yield return Edge("uml25.sequence-message", "Message", "DiagramStencil_Uml25SequenceMessage", "uml25.sequence", "DiagramStencilPalette_Uml25Sequence", 3, 3, "<path d='M4 16 H27' fill='none' stroke='currentColor' stroke-width='2'/><path d='M23 11 L28 16 L23 21' fill='none' stroke='currentColor' stroke-width='2'/>", "straight", "message", "none", "open", false);
    }

    private static IEnumerable<DiagramStencil> DeploymentPalette()
    {
        yield return ClassLike("uml25.component", "Component", "DiagramStencil_Uml25Component", 0, "<<component>>", "Component", 210, 150, "<rect x='5' y='6' width='22' height='21' rx='1.5' fill='none' stroke='currentColor' stroke-width='2'/><path d='M20 10 H28 M20 16 H28' stroke='currentColor' stroke-width='2'/>", "uml25.deployment", "DiagramStencilPalette_Uml25Deployment", 4);
        yield return Node("uml25.deployment-node", "Node", "DiagramStencil_Uml25DeploymentNode", "uml25.deployment", "DiagramStencilPalette_Uml25Deployment", 4, 1, "<path d='M7 10 L13 4 H28 V22 L22 28 H7 Z M13 4 V10 H7 M22 28 V10 H7' fill='none' stroke='currentColor' stroke-width='2'/>", 160, 100, "cube", "<path d='M0 18 L18 0 H100 V82 L82 100 H0 Z M18 0 V18 H0 M82 100 V18 H0' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>", [TextSection("name", "Node", 8, bold: true, align: StencilTextAlign.Center)], new() { ["name"] = "Node" });
        yield return Note("uml25.artifact", "DiagramStencil_Uml25Artifact", "uml25.deployment", "DiagramStencilPalette_Uml25Deployment", 4, 2, "Artifact");
        yield return Node("uml25.deployment-spec", "Deployment Specification", "DiagramStencil_Uml25DeploymentSpec", "uml25.deployment", "DiagramStencilPalette_Uml25Deployment", 4, 3, "<rect x='6' y='5' width='20' height='24' rx='1' fill='none' stroke='currentColor' stroke-width='2'/><path d='M10 12 H22 M10 17 H22 M10 22 H18' stroke='currentColor' stroke-width='1.5'/>", 150, 90, "rectangle", "<rect x='0' y='0' width='100' height='100' rx='2' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>", [TextSection("stereotype", "<<deploy>>", 4, align: StencilTextAlign.Center, fontSize: 10, fontFamily: MonoFont), TextSection("name", "DeploymentSpec", 4, bold: true, align: StencilTextAlign.Center)], new() { ["stereotype"] = "<<deploy>>", ["name"] = "DeploymentSpec" });
    }

    private static IEnumerable<DiagramStencil> RelationshipPalette()
    {
        yield return Edge("uml25.association", "Association", "DiagramStencil_Uml25Association", "uml25.relationships", "DiagramStencilPalette_Uml25Relationships", 5, 0, "<path d='M4 16 H28' fill='none' stroke='currentColor' stroke-width='2'/>", "straight", "association", "none", "none", null);
        yield return Edge("uml25.directed-association", "Directed Association", "DiagramStencil_Uml25DirectedAssociation", "uml25.relationships", "DiagramStencilPalette_Uml25Relationships", 5, 1, "<path d='M4 16 H27' fill='none' stroke='currentColor' stroke-width='2'/><path d='M23 11 L28 16 L23 21' fill='none' stroke='currentColor' stroke-width='2'/>", "straight", "association", "none", "open", false);
        yield return Edge("uml25.dependency", "Dependency", "DiagramStencil_Uml25Dependency", "uml25.relationships", "DiagramStencilPalette_Uml25Relationships", 5, 2, "<path d='M4 16 H27' fill='none' stroke='currentColor' stroke-width='2' stroke-dasharray='4 3'/><path d='M23 11 L28 16 L23 21' fill='none' stroke='currentColor' stroke-width='2'/>", "orthogonal", "dependency", "none", "open", false, "dashed");
        yield return Edge("uml25.generalization", "Generalization", "DiagramStencil_Uml25Generalization", "uml25.relationships", "DiagramStencilPalette_Uml25Relationships", 5, 3, "<path d='M4 16 H22' fill='none' stroke='currentColor' stroke-width='2'/><path d='M22 10 L29 16 L22 22 Z' fill='white' stroke='currentColor' stroke-width='2'/>", "orthogonal", "generalization", "none", "block", false);
        yield return Edge("uml25.realization", "Realization", "DiagramStencil_Uml25Realization", "uml25.relationships", "DiagramStencilPalette_Uml25Relationships", 5, 4, "<path d='M4 16 H22' fill='none' stroke='currentColor' stroke-width='2' stroke-dasharray='4 3'/><path d='M22 10 L29 16 L22 22 Z' fill='white' stroke='currentColor' stroke-width='2'/>", "orthogonal", "realization", "none", "block", false, "dashed");
        yield return Edge("uml25.aggregation", "Aggregation", "DiagramStencil_Uml25Aggregation", "uml25.relationships", "DiagramStencilPalette_Uml25Relationships", 5, 5, "<path d='M12 16 H28' fill='none' stroke='currentColor' stroke-width='2'/><path d='M4 16 L9 11 L14 16 L9 21 Z' fill='white' stroke='currentColor' stroke-width='2'/>", "orthogonal", "aggregation", "diamond", "none", null, null, false);
        yield return Edge("uml25.composition", "Composition", "DiagramStencil_Uml25Composition", "uml25.relationships", "DiagramStencilPalette_Uml25Relationships", 5, 6, "<path d='M12 16 H28' fill='none' stroke='currentColor' stroke-width='2'/><path d='M4 16 L9 11 L14 16 L9 21 Z' fill='currentColor' stroke='currentColor' stroke-width='2'/>", "orthogonal", "composition", "diamond", "none", null, null, true);
    }

    private static DiagramStencil ClassLike(
        string id,
        string name,
        string nameResourceKey,
        int order,
        string? stereotype,
        string defaultName,
        double width,
        double height,
        string iconSvg,
        string paletteId = "uml25.class",
        string paletteNameResourceKey = "DiagramStencilPalette_Uml25Class",
        int paletteOrder = 0)
    {
        var sections = new List<DiagramStencilSection>();
        var defaultData = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(stereotype))
        {
            sections.Add(TextSection("stereotype", stereotype, 4, align: StencilTextAlign.Center, fontSize: 10, fontFamily: MonoFont));
            defaultData["stereotype"] = stereotype;
        }

        sections.Add(TextSection("name", defaultName, 6, bold: true, align: StencilTextAlign.Center));
        sections.Add(Divider());
        sections.Add(ListSection("attributes", "- attribute: Type", 7));
        sections.Add(Divider());
        sections.Add(ListSection("operations", "+ operation(): Type", 7));

        defaultData["name"] = defaultName;
        defaultData["attributes"] = new[] { "- id: Guid", "- name: string" };
        defaultData["operations"] = new[] { "+ Save(): void", "+ Load(): void" };

        return Node(id, name, nameResourceKey, paletteId, paletteNameResourceKey, paletteOrder, order, iconSvg, width, height, sections: sections, defaultData: defaultData);
    }

    private static DiagramStencil Note(
        string id,
        string nameResourceKey,
        string paletteId,
        string paletteNameResourceKey,
        int paletteOrder,
        int order,
        string defaultName = "Note")
        => Node(
            id,
            defaultName,
            nameResourceKey,
            paletteId,
            paletteNameResourceKey,
            paletteOrder,
            order,
            "<path d='M5 4 H23 L27 8 V28 H5 Z M23 4 V9 H28' fill='none' stroke='currentColor' stroke-width='2' stroke-linejoin='round'/>",
            130,
            90,
            "note",
            "<path d='M0 0 H78 L100 22 V100 H0 Z' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/><path d='M78 0 V22 H100' fill='none' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>",
            [TextSection("label", defaultName, 8, align: StencilTextAlign.Left)],
            new() { ["label"] = defaultName });

    private static DiagramStencil Node(
        string id,
        string name,
        string nameResourceKey,
        string paletteId,
        string paletteNameResourceKey,
        int paletteOrder,
        int order,
        string iconSvg,
        double width,
        double height,
        string backgroundShape = "rectangle",
        string? shapeSvg = null,
        List<DiagramStencilSection>? sections = null,
        Dictionary<string, object>? defaultData = null,
        bool isCollapsible = false,
        string contentPosition = "overlay",
        bool preserveAspectRatio = false)
        => new()
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
            IconSvg = iconSvg,
            DefaultWidth = width,
            DefaultHeight = height,
            IsCollapsible = isCollapsible,
            Tags = ["uml", "uml2", "uml25"],
            Keywords = [name, paletteId, "UML 2.5", "2.5"],
            Ports = CardinalPorts(),
            ConnectionPoints = CardinalConnectionPoints(),
            Layout = new()
            {
                BackgroundShape = backgroundShape,
                ShapeSvg = shapeSvg ?? "<rect x='0' y='0' width='100' height='100' rx='2' fill='var(--stencil-fill)' stroke='var(--stencil-stroke)' stroke-width='var(--stencil-stroke-width)' vector-effect='non-scaling-stroke'/>",
                PreserveAspectRatio = preserveAspectRatio,
                ContentPosition = contentPosition,
                Sections = sections ?? []
            },
            DefaultData = defaultData ?? []
        };

    private static DiagramStencil Edge(
        string id,
        string name,
        string nameResourceKey,
        string paletteId,
        string paletteNameResourceKey,
        int paletteOrder,
        int order,
        string iconSvg,
        string routing,
        string connectorType,
        string startArrow,
        string endArrow,
        bool? endArrowFill,
        string? dashPattern = null,
        bool? startArrowFill = null)
        => new()
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
            Kind = DiagramStencilKind.Edge,
            Origin = DiagramStencilOrigin.TempoOriginal,
            IconSvg = iconSvg,
            Tags = ["uml", "uml2", "uml25", "relationship"],
            Keywords = [name, connectorType, "UML 2.5", "2.5"],
            EdgeDefaults = new()
            {
                Routing = routing,
                ConnectorType = connectorType,
                Shape = "connector",
                StartArrow = startArrow,
                EndArrow = endArrow,
                StartArrowFill = startArrowFill,
                EndArrowFill = endArrowFill,
                Style = dashPattern is null ? null : new DiagramStyle { StrokeDashPattern = dashPattern }
            }
        };

    private static DiagramStencilSection TextSection(
        string dataKey,
        string defaultText,
        double padding,
        bool bold = false,
        StencilTextAlign align = StencilTextAlign.Left,
        double fontSize = 12,
        string? fontFamily = null)
        => new()
        {
            Type = "text",
            DataKey = dataKey,
            DefaultText = defaultText,
            Padding = padding,
            TextStyle = new()
            {
                IsBold = bold,
                TextAlign = align,
                FontSize = fontSize,
                FontFamily = fontFamily
            }
        };

    private static DiagramStencilSection ListSection(string dataKey, string defaultText, double padding)
        => new()
        {
            Type = "list",
            DataKey = dataKey,
            DefaultText = defaultText,
            Padding = padding,
            TextStyle = new()
            {
                TextAlign = StencilTextAlign.Left,
                FontSize = 12,
                FontFamily = MonoFont
            }
        };

    private static DiagramStencilSection Divider()
        => new() { Type = "divider", Padding = 0 };

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
