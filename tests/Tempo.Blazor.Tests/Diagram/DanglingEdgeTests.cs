using FluentAssertions;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class DanglingEdgeTests
{
    [Fact]
    public void GetEdgePoints_WithDanglingSource_ReturnsSourcePoint()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { Id = "n1", X = 100, Y = 100, W = 50, H = 50 };
        doc.Nodes.Add(node);

        var edge = new DiagramEdge
        {
            SourceNodeId = null,
            SourcePoint = new DiagramPoint(10, 20),
            TargetNodeId = "n1",
        };
        doc.Edges.Add(edge);

        var pts = DiagramGeometryHelper.GetEdgePoints(doc, edge);
        pts.Should().HaveCount(2);
        pts[0].X.Should().Be(10);
        pts[0].Y.Should().Be(20);
    }

    [Fact]
    public void GetEdgePoints_WithDanglingTarget_ReturnsTargetPoint()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { Id = "n1", X = 100, Y = 100, W = 50, H = 50 };
        doc.Nodes.Add(node);

        var edge = new DiagramEdge
        {
            SourceNodeId = "n1",
            TargetNodeId = null,
            TargetPoint = new DiagramPoint(200, 250),
        };
        doc.Edges.Add(edge);

        var pts = DiagramGeometryHelper.GetEdgePoints(doc, edge);
        pts.Should().HaveCount(2);
        pts[^1].X.Should().Be(200);
        pts[^1].Y.Should().Be(250);
    }

    [Fact]
    public void GetEdgePoints_WithBothEndsDangling_ReturnsBothPoints()
    {
        var doc = new DiagramDocument();

        var edge = new DiagramEdge
        {
            SourceNodeId = null,
            SourcePoint = new DiagramPoint(10, 20),
            TargetNodeId = null,
            TargetPoint = new DiagramPoint(200, 250),
        };
        doc.Edges.Add(edge);

        var pts = DiagramGeometryHelper.GetEdgePoints(doc, edge);
        pts.Should().HaveCount(2);
        pts[0].X.Should().Be(10);
        pts[0].Y.Should().Be(20);
        pts[1].X.Should().Be(200);
        pts[1].Y.Should().Be(250);
    }

    [Fact]
    public void GetEdgePoints_WithDanglingSource_WithWaypoints_IncludesWaypoints()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { Id = "n1", X = 100, Y = 100, W = 50, H = 50 };
        doc.Nodes.Add(node);

        var edge = new DiagramEdge
        {
            SourceNodeId = null,
            SourcePoint = new DiagramPoint(10, 20),
            TargetNodeId = "n1",
            Waypoints = { new DiagramPoint(50, 50), new DiagramPoint(80, 80) }
        };
        doc.Edges.Add(edge);

        var pts = DiagramGeometryHelper.GetEdgePoints(doc, edge);
        pts.Should().HaveCount(4);
        pts[0].X.Should().Be(10);
        pts[0].Y.Should().Be(20);
        pts[1].X.Should().Be(50);
        pts[1].Y.Should().Be(50);
        pts[2].X.Should().Be(80);
        pts[2].Y.Should().Be(80);
    }

    [Fact]
    public void UpdateEdgeTerminalCommand_UpdatesAndUndoesSourcePoint()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = null,
            SourcePoint = new DiagramPoint(10, 20),
            TargetNodeId = null,
            TargetPoint = new DiagramPoint(100, 100),
        };
        doc.Edges.Add(edge);

        var cmd = new UpdateEdgeTerminalCommand(
            doc, "e1", isSource: true,
            null, null, new DiagramPoint(10, 20), null,
            null, null, new DiagramPoint(30, 40), null);

        cmd.Execute();
        edge.SourcePoint!.X.Should().Be(30);
        edge.SourcePoint!.Y.Should().Be(40);

        cmd.Undo();
        edge.SourcePoint!.X.Should().Be(10);
        edge.SourcePoint!.Y.Should().Be(20);
    }

    [Fact]
    public void UpdateEdgeTerminalCommand_UpdatesAndUndoesTargetPoint()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = null,
            SourcePoint = new DiagramPoint(10, 20),
            TargetNodeId = null,
            TargetPoint = new DiagramPoint(100, 100),
        };
        doc.Edges.Add(edge);

        var cmd = new UpdateEdgeTerminalCommand(
            doc, "e1", isSource: false,
            null, null, new DiagramPoint(100, 100), null,
            null, null, new DiagramPoint(150, 160), null);

        cmd.Execute();
        edge.TargetPoint!.X.Should().Be(150);
        edge.TargetPoint!.Y.Should().Be(160);

        cmd.Undo();
        edge.TargetPoint!.X.Should().Be(100);
        edge.TargetPoint!.Y.Should().Be(100);
    }

    [Fact]
    public void UpdateEdgeTerminalCommand_ClearsSourcePoint_WhenOldWasNull()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = null,
            TargetNodeId = "n1",
        };
        doc.Edges.Add(edge);

        var cmd = new UpdateEdgeTerminalCommand(
            doc, "e1", isSource: true,
            null, null, null, null,
            null, null, new DiagramPoint(30, 40), null);

        cmd.Execute();
        edge.SourcePoint.Should().NotBeNull();
        edge.SourcePoint!.X.Should().Be(30);

        cmd.Undo();
        edge.SourcePoint.Should().BeNull();
    }
}
