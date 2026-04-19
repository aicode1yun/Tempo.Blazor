using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class UpdateEdgeTerminalCommandTests
{
    [Fact]
    public void Execute_DisconnectSource_SetsPointAndClearsNode()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = "n1",
            TargetNodeId = "n2",
        };
        doc.Edges.Add(edge);

        var cmd = new UpdateEdgeTerminalCommand(
            doc, "e1", isSource: true,
            "n1", null, null, null,
            null, null, new DiagramPoint(10, 20), null);

        cmd.Execute();
        edge.SourceNodeId.Should().BeNull();
        edge.SourcePoint.Should().NotBeNull();
        edge.SourcePoint!.X.Should().Be(10);
        edge.SourcePoint!.Y.Should().Be(20);
    }

    [Fact]
    public void Undo_ReconnectsSourceBackToNode()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = "n1",
            TargetNodeId = "n2",
        };
        doc.Edges.Add(edge);

        var cmd = new UpdateEdgeTerminalCommand(
            doc, "e1", isSource: true,
            "n1", null, null, null,
            null, null, new DiagramPoint(10, 20), null);

        cmd.Execute();
        cmd.Undo();
        edge.SourceNodeId.Should().Be("n1");
        edge.SourcePoint.Should().BeNull();
    }

    [Fact]
    public void Execute_ReconnectTarget_ToNewNode()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = "n1",
            TargetNodeId = "n2",
            TargetPortId = "p1",
        };
        doc.Edges.Add(edge);

        var cmd = new UpdateEdgeTerminalCommand(
            doc, "e1", isSource: false,
            "n2", "p1", null, null,
            "n3", "p2", null, null);

        cmd.Execute();
        edge.TargetNodeId.Should().Be("n3");
        edge.TargetPortId.Should().Be("p2");
    }

    [Fact]
    public void Execute_ConnectDanglingToNode_ClearsPoint()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = null,
            SourcePoint = new DiagramPoint(10, 20),
            TargetNodeId = "n2",
        };
        doc.Edges.Add(edge);

        var cmd = new UpdateEdgeTerminalCommand(
            doc, "e1", isSource: true,
            null, null, new DiagramPoint(10, 20), null,
            "n1", "p1", null, null);

        cmd.Execute();
        edge.SourceNodeId.Should().Be("n1");
        edge.SourcePortId.Should().Be("p1");
        edge.SourcePoint.Should().BeNull();
    }

    [Fact]
    public void Execute_WithConstraint_SetsConstraint()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = "n1",
            TargetNodeId = "n2",
        };
        doc.Edges.Add(edge);

        var constraint = new DiagramConnectionConstraint { RelativeX = 0.5, RelativeY = 1.0, Perimeter = true };
        var cmd = new UpdateEdgeTerminalCommand(
            doc, "e1", isSource: true,
            "n1", null, null, null,
            null, null, new DiagramPoint(10, 20), constraint);

        cmd.Execute();
        edge.SourceConstraint.Should().NotBeNull();
        edge.SourceConstraint!.RelativeX.Should().Be(0.5);
        edge.SourceConstraint!.Perimeter.Should().BeTrue();
    }

    [Fact]
    public void Undo_RestoresConstraint()
    {
        var doc = new DiagramDocument();
        var edge = new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = "n1",
            TargetNodeId = "n2",
            SourceConstraint = new DiagramConnectionConstraint { RelativeX = 0.25, RelativeY = 0.25 },
        };
        doc.Edges.Add(edge);

        var cmd = new UpdateEdgeTerminalCommand(
            doc, "e1", isSource: true,
            "n1", null, null, edge.SourceConstraint.Clone(),
            null, null, new DiagramPoint(10, 20), null);

        cmd.Execute();
        edge.SourceConstraint.Should().BeNull();

        cmd.Undo();
        edge.SourceConstraint.Should().NotBeNull();
        edge.SourceConstraint!.RelativeX.Should().Be(0.25);
    }
}
