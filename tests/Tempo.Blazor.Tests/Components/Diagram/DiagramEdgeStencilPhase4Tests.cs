using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class DiagramEdgeStencilPhase4Tests : LocalizationTestBase
{
    public DiagramEdgeStencilPhase4Tests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["TmDiagramToolbox_Toggle"] = "Toggle toolbox",
            ["TmDiagramToolbox_Title"] = "Toolbox",
            ["TmDiagramToolbox_SearchPlaceholder"] = "Search stencils",
            ["TmDiagramToolbox_NoResults"] = "No matching stencils",
            ["TmDiagramToolbox_DragStencil"] = "Drag {0} onto the canvas",
            ["TmDiagramProperties_Title"] = "Properties",
            ["TmDiagramProperties_Style"] = "Style",
            ["TmDiagramProperties_Label"] = "Label",
            ["TmDiagramProperties_LabelOffsetX"] = "Label Offset X",
            ["TmDiagramProperties_LabelOffsetY"] = "Label Offset Y",
            ["TmDiagramProperties_EdgePreset"] = "Preset",
            ["TmDiagramProperties_EdgeShape"] = "Connection",
            ["TmDiagramProperties_Routing"] = "Routing",
            ["TmDiagramProperties_ConnectorType"] = "Connector type",
            ["TmDiagramProperties_StartArrow"] = "Start Arrow",
            ["TmDiagramProperties_EndArrow"] = "End Arrow",
            ["TmDiagramProperties_EdgeTemplate"] = "Relationship template",
            ["TmDiagramProperties_EdgeTemplate_Custom"] = "Custom",
            ["DiagramStencil_Dependency"] = "Dependency",
            ["DiagramStencil_Association"] = "Association"
        });

        var registry = new DiagramStencilRegistry();
        registry.RegisterStencil(CreateDependencyStencil());
        registry.RegisterStencil(CreateAssociationStencil());
        Services.AddSingleton(registry);
    }

    [Fact]
    public void Edge_Defaults_Create_Valid_DiagramEdge()
    {
        var stencil = CreateDependencyStencil();

        var edge = DiagramEdgeStencilFactory.CreateEdge(
            stencil,
            sourceNodeId: "source",
            sourcePortId: "out",
            targetNodeId: "target",
            targetPortId: "in");

        edge.SourceNodeId.Should().Be("source");
        edge.SourcePortId.Should().Be("out");
        edge.TargetNodeId.Should().Be("target");
        edge.TargetPortId.Should().Be("in");
        edge.Routing.Should().Be("orthogonal");
        edge.ConnectorType.Should().Be("dependency");
        edge.Shape.Should().Be("connector");
        edge.StartArrow.Should().Be("none");
        edge.EndArrow.Should().Be("open");
        edge.EndArrowFill.Should().BeFalse();
        edge.Style.StrokeDashPattern.Should().Be("dashed");
        edge.Style.StrokeWidth.Should().Be(1.25);
        edge.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Apply_Edge_Stencil_Command_Updates_Edge_And_Supports_Undo()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            SourceNodeId = "a",
            TargetNodeId = "b",
            Routing = "straight",
            ConnectorType = "association",
            EndArrow = "classic",
            Style = new DiagramStyle { StrokeDashPattern = "", StrokeWidth = 2 }
        };
        doc.Edges.Add(edge);

        var command = new ApplyEdgeStencilCommand(doc, edge.Id, CreateDependencyStencil());

        command.Execute();

        edge.Routing.Should().Be("orthogonal");
        edge.ConnectorType.Should().Be("dependency");
        edge.EndArrow.Should().Be("open");
        edge.Style.StrokeDashPattern.Should().Be("dashed");
        edge.Style.StrokeWidth.Should().Be(1.25);

        command.Undo();

        edge.Routing.Should().Be("straight");
        edge.ConnectorType.Should().Be("association");
        edge.EndArrow.Should().Be("classic");
        edge.Style.StrokeDashPattern.Should().Be("");
        edge.Style.StrokeWidth.Should().Be(2);
    }

    [Fact]
    public void Toolbox_Renders_Edge_Stencil_With_Distinct_Class()
    {
        var cut = RenderComponent<TmDiagramToolbox>();

        var edgeItem = cut.Find("[data-stencil-id='edge.dependency']");

        edgeItem.ClassList.Should().Contain("tm-diagram-toolbox__item--edge");
        edgeItem.GetAttribute("data-stencil-kind").Should().Be("edge");
        edgeItem.QuerySelector(".tm-diagram-toolbox__edge-preview").Should().NotBeNull();
    }

    [Fact]
    public void BuiltIn_Provider_Exposes_Generic_Edge_Stencils()
    {
        var stencils = new BuiltInDiagramStencilProvider()
            .GetStencilSets()
            .SelectMany(set => set.Stencils)
            .Where(stencil => stencil.Kind == DiagramStencilKind.Edge)
            .ToList();

        stencils.Should().Contain(stencil => stencil.Id == "relationships.dependency");
        stencils.All(stencil => stencil.Origin == DiagramStencilOrigin.TempoOriginal).Should().BeTrue();
        stencils.All(stencil => !string.IsNullOrWhiteSpace(stencil.NameResourceKey)).Should().BeTrue();
        stencils.All(stencil => stencil.EdgeDefaults is not null).Should().BeTrue();
        stencils.All(stencil => stencil.Ports.Count == 0).Should().BeTrue();
        stencils.All(stencil => stencil.ConnectionPoints.Count == 0).Should().BeTrue();
    }

    [Fact]
    public void BuiltIn_Edge_Stencil_Creates_Dependency_Edge()
    {
        var stencil = new BuiltInDiagramStencilProvider()
            .GetStencilSets()
            .SelectMany(set => set.Stencils)
            .Single(stencil => stencil.Id == "relationships.dependency");

        var edge = DiagramEdgeStencilFactory.CreateEdge(stencil, "a", null, "b", null);

        edge.ConnectorType.Should().Be("dependency");
        edge.Routing.Should().Be("orthogonal");
        edge.EndArrow.Should().Be("open");
        edge.EndArrowFill.Should().BeFalse();
        edge.Style.StrokeDashPattern.Should().Be("dashed");
    }

    [Fact]
    public void PropertiesPanel_Renders_Localized_Relationship_Template_Selector()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            SourceNodeId = "a",
            TargetNodeId = "b",
            ConnectorType = "association"
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramPropertiesPanel>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.SelectedIds, [edge.Id])
            .Add(c => c.ReadOnly, false));

        var field = cut.FindAll(".tm-diagram-properties__field")
            .Single(f => f.QuerySelector("label")?.TextContent.Trim() == "Relationship template");

        field.TextContent.Should().Contain("Dependency");
        field.TextContent.Should().Contain("Association");
        field.TextContent.Should().Contain("Custom");
    }

    [Fact]
    public void PropertiesPanel_Relationship_Template_Applies_Stencil_Defaults()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            SourceNodeId = "a",
            TargetNodeId = "b",
            Routing = "straight",
            ConnectorType = "association",
            EndArrow = "classic"
        };
        doc.Edges.Add(edge);
        var stack = new DiagramCommandStack();

        var cut = RenderComponent<CascadingValue<DiagramCommandStack>>(p => p
            .Add(c => c.Value, stack)
            .AddChildContent<TmDiagramPropertiesPanel>(child => child
                .Add(c => c.Document, doc)
                .Add(c => c.SelectedIds, [edge.Id])
                .Add(c => c.ReadOnly, false)));

        var select = cut.Find("[data-edge-template-selector] select");
        select.Change("edge.dependency");

        edge.Routing.Should().Be("orthogonal");
        edge.ConnectorType.Should().Be("dependency");
        edge.EndArrow.Should().Be("open");
        edge.Style.StrokeDashPattern.Should().Be("dashed");
        stack.CanUndo.Should().BeTrue();
    }

    [Fact]
    public async Task Dragging_Edge_Stencil_Sets_Active_Template_For_New_Edges()
    {
        var doc = new DiagramDocument();
        var source = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 80, H = 40 };
        var target = new DiagramNode { StencilId = "general.rectangle", X = 160, Y = 0, W = 80, H = 40 };
        doc.Nodes.Add(source);
        doc.Nodes.Add(target);

        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Find("[data-stencil-id='edge.dependency']").TriggerEvent("ondragstart", new DragEventArgs());

        var canvas = cut.FindComponent<TmDiagramCanvas>();
        await cut.InvokeAsync(async () => await canvas.Instance.JsOnEdgeCreated(
            source.Id,
            null,
            target.Id,
            null,
            "right",
            0.5,
            "left",
            0.5));

        doc.Edges.Should().ContainSingle();
        doc.Edges[0].ConnectorType.Should().Be("dependency");
        doc.Edges[0].Routing.Should().Be("orthogonal");
        doc.Edges[0].EndArrow.Should().Be("open");
        doc.Edges[0].Style.StrokeDashPattern.Should().Be("dashed");
    }

    private static DiagramStencil CreateDependencyStencil() => new()
    {
        Id = "edge.dependency",
        Name = "Dependency",
        NameResourceKey = "DiagramStencil_Dependency",
        Category = "Relationships",
        SetId = "phase4",
        PaletteId = "phase4.relationships",
        PaletteNameResourceKey = "TmDiagramProperties_EdgeTemplate",
        Origin = DiagramStencilOrigin.TempoOriginal,
        Kind = DiagramStencilKind.Edge,
        IconSvg = """<path d="M4 16 H28" fill="none" stroke="currentColor" stroke-width="2" stroke-dasharray="4 2"/><path d="M24 12 L28 16 L24 20" fill="none" stroke="currentColor" stroke-width="2"/>""",
        EdgeDefaults = new()
        {
            Routing = "orthogonal",
            ConnectorType = "dependency",
            Shape = "connector",
            StartArrow = "none",
            EndArrow = "open",
            EndArrowFill = false,
            Style = new DiagramStyle
            {
                StrokeDashPattern = "dashed",
                StrokeWidth = 1.25
            }
        }
    };

    private static DiagramStencil CreateAssociationStencil() => new()
    {
        Id = "edge.association",
        Name = "Association",
        NameResourceKey = "DiagramStencil_Association",
        Category = "Relationships",
        SetId = "phase4",
        PaletteId = "phase4.relationships",
        PaletteNameResourceKey = "TmDiagramProperties_EdgeTemplate",
        Origin = DiagramStencilOrigin.TempoOriginal,
        Kind = DiagramStencilKind.Edge,
        IconSvg = """<path d="M4 16 H28" fill="none" stroke="currentColor" stroke-width="2"/>""",
        EdgeDefaults = new()
        {
            Routing = "straight",
            ConnectorType = "association",
            Shape = "connector",
            StartArrow = "none",
            EndArrow = "none"
        }
    };
}
