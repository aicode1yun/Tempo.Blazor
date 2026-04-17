using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class EdgeSystemTests : LocalizationTestBase
{
    public EdgeSystemTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void ComputeEdgePath_WithRounded_ContainsQuadraticBezier()
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
            Routing = "orthogonal",
            Rounded = true,
            Waypoints = [new DiagramPoint(100, 25), new DiagramPoint(150, 25), new DiagramPoint(200, 25)]
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var path = cut.Find("path.tm-diagram-edge-path");
        var d = path.GetAttribute("d");
        d.Should().Contain("Q");
    }

    [Fact]
    public void ComputeEdgePath_WithEndArrowClassic_HasMarkerEndAttribute()
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
            EndArrow = "classic",
            EndArrowSize = 10,
            Style = new DiagramStyle { Stroke = "#ff0000" }
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var path = cut.Find("path.tm-diagram-edge-path");
        var markerEnd = path.GetAttribute("marker-end");
        markerEnd.Should().Contain("arrow-end-");
        markerEnd.Should().Contain(edge.Id);
    }

    [Fact]
    public void ComputeEdgePath_WithDashPattern_DashArrayIsRendered()
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
            Style = new DiagramStyle { StrokeDashPattern = "dashed" }
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var path = cut.Find("path.tm-diagram-edge-path");
        path.GetAttribute("stroke-dasharray").Should().Be("5,5");
    }

    [Fact]
    public void ComputeEdgePath_WithJumpStyleArc_ContainsArcCommand()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 100, W = 100, H = 50 };
        var n3 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 200, W = 100, H = 50 };
        var n4 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 300, W = 100, H = 50 };
        doc.Nodes.AddRange([n1, n2, n3, n4]);

        // Horizontal edge crossing vertical edge
        var edgeH = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            Routing = "orthogonal",
            JumpStyle = "arc",
            Waypoints = [new DiagramPoint(100, 50), new DiagramPoint(200, 50), new DiagramPoint(200, 100)]
        };
        var edgeV = new DiagramEdge
        {
            SourceNodeId = n3.Id,
            TargetNodeId = n4.Id,
            Routing = "orthogonal",
            JumpStyle = "arc",
            Waypoints = [new DiagramPoint(100, 200), new DiagramPoint(100, 250), new DiagramPoint(200, 250)]
        };
        // Adjust so they cross at (100,250) and (100,50)? Let's make simpler crossing:
        // Edge H: (50,150) -> (250,150) horizontal
        // Edge V: (150,50) -> (150,250) vertical
        edgeH.Waypoints = [new DiagramPoint(50, 150), new DiagramPoint(250, 150)];
        edgeV.Waypoints = [new DiagramPoint(150, 50), new DiagramPoint(150, 250)];
        doc.Edges.Add(edgeH);
        doc.Edges.Add(edgeV);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var paths = cut.FindAll("path.tm-diagram-edge-path");
        var d = paths[0].GetAttribute("d");
        d.Should().Contain("A");
    }

    [Fact]
    public void GetEdgePoints_WithSourceAndTargetSpacing_AppliesSpacing()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 0, Y = 0, W = 100, H = 50,
            Ports = [new DiagramPort { Side = PortSide.Right, Offset = 0.5 }]
        };
        var n2 = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 200, Y = 0, W = 100, H = 50,
            Ports = [new DiagramPort { Side = PortSide.Left, Offset = 0.5 }]
        };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            SourceSpacing = 20,
            TargetSpacing = 15
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var path = cut.Find("path.tm-diagram-edge-path");
        var d = path.GetAttribute("d");
        // Source port is at (100,25), spacing 20 to right => (120,25)
        // Target port is at (200,25), spacing 15 to left => (185,25)
        d.Should().Contain("M 120 25");
        d.Should().Contain("L 180 25"); // 185 - 5px arrowhead inset for default classic arrow
    }

    [Fact]
    public void InsertEdgeWaypointCommand_AddsAndRemovesWaypoint()
    {
        var doc = new DiagramDocument();
        var stack = new DiagramCommandStack();
        var edge = new DiagramEdge { SourceNodeId = "a", TargetNodeId = "b" };
        edge.Waypoints.Add(new DiagramPoint(10, 10));
        doc.Edges.Add(edge);

        var cmd = new InsertEdgeWaypointCommand(doc, edge.Id, 1, new DiagramPoint(20, 20));
        cmd.Execute();
        edge.Waypoints.Count.Should().Be(2);
        edge.Waypoints[1].X.Should().Be(20);

        cmd.Undo();
        edge.Waypoints.Count.Should().Be(1);
    }

    [Fact]
    public void DeleteEdgeWaypointCommand_RemovesAndRestoresWaypoint()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge { SourceNodeId = "a", TargetNodeId = "b" };
        edge.Waypoints.Add(new DiagramPoint(10, 10));
        edge.Waypoints.Add(new DiagramPoint(20, 20));
        doc.Edges.Add(edge);

        var cmd = new DeleteEdgeWaypointCommand(doc, edge.Id, 1, new DiagramPoint(20, 20));
        cmd.Execute();
        edge.Waypoints.Count.Should().Be(1);

        cmd.Undo();
        edge.Waypoints.Count.Should().Be(2);
        edge.Waypoints[1].X.Should().Be(20);
    }

    [Fact]
    public void UpdateEdgeStyleCommand_ApplyAndUndo()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            SourceNodeId = "a",
            TargetNodeId = "b",
            StartArrow = "none",
            EndArrow = "classic",
            Style = new DiagramStyle { Stroke = "#000000", StrokeWidth = 1 }
        };
        doc.Edges.Add(edge);

        var before = DiagramEdgeStyleSnapshot.FromEdge(edge);
        edge.EndArrow = "block";
        edge.Style.Stroke = "#ff0000";
        edge.Style.StrokeWidth = 2;
        var after = DiagramEdgeStyleSnapshot.FromEdge(edge);

        // reset to before for command test
        before.ApplyTo(edge);

        var cmd = new UpdateEdgeStyleCommand(doc, edge.Id, before, after);
        cmd.Execute();
        edge.EndArrow.Should().Be("block");
        edge.Style.Stroke.Should().Be("#ff0000");
        edge.Style.StrokeWidth.Should().Be(2);

        cmd.Undo();
        edge.EndArrow.Should().Be("classic");
        edge.Style.Stroke.Should().Be("#000000");
        edge.Style.StrokeWidth.Should().Be(1);
    }

    [Fact]
    public void UpdateEdgeRoutingCommand_ChangesRoutingAndWaypoints()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            SourceNodeId = "a",
            TargetNodeId = "b",
            Routing = "straight",
            Waypoints = [new DiagramPoint(10, 10)]
        };
        doc.Edges.Add(edge);

        var oldWps = edge.Waypoints.Select(p => new DiagramPoint(p.X, p.Y)).ToList();
        var newWps = new List<DiagramPoint> { new(20, 20), new(30, 30) };

        var cmd = new UpdateEdgeRoutingCommand(doc, edge.Id, "straight", "orthogonal", oldWps, newWps);
        cmd.Execute();
        edge.Routing.Should().Be("orthogonal");
        edge.Waypoints.Count.Should().Be(2);

        cmd.Undo();
        edge.Routing.Should().Be("straight");
        edge.Waypoints.Count.Should().Be(1);
        edge.Waypoints[0].X.Should().Be(10);
    }

    [Fact]
    public async Task ComputeOrthogonalWaypointsAsync_PassesObstaclesExcludingSourceAndTarget()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 300, Y = 0, W = 100, H = 50 };
        var n3 = new DiagramNode { StencilId = "general.rectangle", X = 150, Y = 0, W = 50, H = 50 }; // obstacle
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);
        doc.Nodes.Add(n3);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            Routing = "orthogonal"
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        var _ = await cut.Instance.ComputeOrthogonalWaypointsAsync(edge);

        var invocation = JSInterop.Invocations
            .Where(i => i.Identifier == "tmDiagramEditor.computeOrthogonalWaypoints")
            .LastOrDefault();

        invocation.Should().NotBeNull();
        var obstacles = invocation.Arguments.Last() as System.Collections.Generic.IEnumerable<object>;
        obstacles.Should().NotBeNull();
        var obstacleList = obstacles!.ToList();
        obstacleList.Count.Should().Be(1);
    }

    [Fact]
    public void StencilShape_GetSectionContent_WithMathJaxEnabled_WrapsMathInSpan()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 0, Y = 0, W = 100, H = 50,
            Style = new DiagramStyle { EnableMathJax = true },
            Data = { ["label"] = "$$x^2$$" }
        };
        doc.Nodes.Add(node);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var mathSpan = cut.Find(".tm-diagram-math");
        mathSpan.Should().NotBeNull();
        mathSpan.TextContent.Should().Contain("x^2");
    }

    [Fact]
    public void StencilShape_GetSectionContent_WithMathJaxDisabled_ReturnsPlainText()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 0, Y = 0, W = 100, H = 50,
            Style = new DiagramStyle { EnableMathJax = false },
            Data = { ["label"] = "$$x^2$$" }
        };
        doc.Nodes.Add(node);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var nodeText = cut.Find(".tm-diagram-node__text");
        nodeText.InnerHtml.Should().NotContain("tm-diagram-math");
        nodeText.TextContent.Should().Contain("$$x^2$$");
    }

    [Fact]
    public void EdgeLabel_WithMathJaxEnabled_RendersForeignObject()
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
            Label = "$$x^2$$",
            Style = new DiagramStyle { EnableMathJax = true }
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var fo = cut.Find("foreignObject");
        fo.Should().NotBeNull();
        fo.InnerHtml.Should().Contain("tm-diagram-math");
    }

    [Fact]
    public void OnMathSvgCached_StoresSvgInNodeData()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        doc.Nodes.Add(n1);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Instance.JsOnMathSvgCached(n1.Id, "<svg></svg>");
        n1.Data["__mathSvg"].Should().Be("<svg></svg>");
    }
}
