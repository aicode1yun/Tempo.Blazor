using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class ElbowFlipTests : LocalizationTestBase
{
    public ElbowFlipTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void FlipEdgeCommand_ExecuteAndUndo()
    {
        var edge = new DiagramEdge
        {
            Routing = "elbow",
            ElbowOrientation = "auto",
            Waypoints = [new DiagramPoint(100, 50), new DiagramPoint(100, 150)]
        };

        var newWaypoints = new List<DiagramPoint> { new(200, 50), new(200, 150) };
        var cmd = new FlipEdgeCommand(edge, "horizontal", newWaypoints);

        cmd.Execute();
        edge.ElbowOrientation.Should().Be("horizontal");
        edge.Waypoints.Should().HaveCount(2);
        edge.Waypoints[0].X.Should().Be(200);

        cmd.Undo();
        edge.ElbowOrientation.Should().Be("auto");
        edge.Waypoints[0].X.Should().Be(100);
    }

    [Fact]
    public void ElbowRouting_HorizontalOrientation_GeneratesHorizontalFirstWaypoints()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50, Ports = [new DiagramPort { Side = PortSide.Right, Offset = 0.5 }] };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 100, W = 100, H = 50, Ports = [new DiagramPort { Side = PortSide.Left, Offset = 0.5 }] };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            Routing = "elbow",
            ElbowOrientation = "horizontal",
            SourcePortId = n1.Ports[0].Id,
            TargetPortId = n2.Ports[0].Id
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var path = cut.Find("path.tm-diagram-edge-path");
        var d = path.GetAttribute("d");
        // Horizontal-first elbow from right side of n1 to left side of n2 should have an intermediate waypoint
        d.Should().Contain("L");
    }

    [Fact]
    public void ElbowRouting_VerticalOrientation_GeneratesVerticalFirstWaypoints()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50, Ports = [new DiagramPort { Side = PortSide.Right, Offset = 0.5 }] };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 100, W = 100, H = 50, Ports = [new DiagramPort { Side = PortSide.Left, Offset = 0.5 }] };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            Routing = "elbow",
            ElbowOrientation = "vertical",
            SourcePortId = n1.Ports[0].Id,
            TargetPortId = n2.Ports[0].Id
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var path = cut.Find("path.tm-diagram-edge-path");
        var d = path.GetAttribute("d");
        d.Should().Contain("L");
    }

    [Fact]
    public void PropertiesPanel_ElbowOrientationSelect_ChangesOrientation()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 0, W = 100, H = 50 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            Routing = "elbow",
            ElbowOrientation = "auto"
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramPropertiesPanel>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.SelectedIds, [edge.Id])
            .Add(c => c.ReadOnly, false));

        var field = cut.FindAll(".tm-diagram-properties__field")
            .FirstOrDefault(f => f.QuerySelector("label")?.TextContent.Contains("Orientation") == true
                              || f.QuerySelector("label")?.TextContent.Contains("Orientace") == true);
        field.Should().NotBeNull();
        var select = field!.QuerySelector("select");
        select.Should().NotBeNull();
        select!.Change("horizontal");

        edge.ElbowOrientation.Should().Be("horizontal");
    }
}
