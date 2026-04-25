using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class ReverseEdgeTests
{
    [Fact]
    public void Execute_ReversesSourceAndTarget()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = "src",
            TargetNodeId = "tgt",
            SourcePortId = "sport",
            TargetPortId = "tport",
            StartArrow = "none",
            EndArrow = "classic",
            StartArrowSize = 8,
            EndArrowSize = 12,
            StartArrowFill = false,
            EndArrowFill = true,
        };

        var cmd = new ReverseEdgeCommand(edge);
        cmd.Execute();

        edge.SourceNodeId.Should().Be("tgt");
        edge.TargetNodeId.Should().Be("src");
        edge.SourcePortId.Should().Be("tport");
        edge.TargetPortId.Should().Be("sport");
        // StartArrow/EndArrow are NOT swapped – they describe the physical start/end
        // of the edge line, which is already reversed by swapping source/target.
        edge.StartArrow.Should().Be("none");
        edge.EndArrow.Should().Be("classic");
        edge.StartArrowSize.Should().Be(8);
        edge.EndArrowSize.Should().Be(12);
        edge.StartArrowFill.Should().BeFalse();
        edge.EndArrowFill.Should().BeTrue();
    }

    [Fact]
    public void Execute_ReversesWaypoints()
    {
        var edge = new DiagramEdge
        {
            Waypoints =
            [
                new DiagramPoint(0, 0),
                new DiagramPoint(50, 50),
                new DiagramPoint(100, 100),
            ]
        };

        var cmd = new ReverseEdgeCommand(edge);
        cmd.Execute();

        edge.Waypoints.Should().HaveCount(3);
        edge.Waypoints[0].X.Should().Be(100);
        edge.Waypoints[0].Y.Should().Be(100);
        edge.Waypoints[2].X.Should().Be(0);
        edge.Waypoints[2].Y.Should().Be(0);
    }

    [Fact]
    public void Undo_RestoresOriginalState()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = "src",
            TargetNodeId = "tgt",
            SourcePortId = "sport",
            TargetPortId = "tport",
            SourceEdgeId = "esrc",
            TargetEdgeId = "etgt",
            SourceEdgeT = 0.2,
            TargetEdgeT = 0.8,
            SourcePoint = new DiagramPoint(10, 20),
            TargetPoint = new DiagramPoint(30, 40),
            SourceConstraint = new DiagramConnectionConstraint { RelativeX = 0, RelativeY = 0.5 },
            TargetConstraint = new DiagramConnectionConstraint { RelativeX = 1, RelativeY = 0.5 },
            SourceSpacing = 5,
            TargetSpacing = 10,
            SourceCardinality = "1",
            TargetCardinality = "*",
            StartArrow = "none",
            EndArrow = "block",
            StartArrowSize = 6,
            EndArrowSize = 10,
            StartArrowFill = false,
            EndArrowFill = true,
            Waypoints =
            [
                new DiagramPoint(0, 0),
                new DiagramPoint(100, 100),
            ]
        };

        var cmd = new ReverseEdgeCommand(edge);
        cmd.Execute();
        cmd.Undo();

        edge.SourceNodeId.Should().Be("src");
        edge.TargetNodeId.Should().Be("tgt");
        edge.SourcePortId.Should().Be("sport");
        edge.TargetPortId.Should().Be("tport");
        edge.SourceEdgeId.Should().Be("esrc");
        edge.TargetEdgeId.Should().Be("etgt");
        edge.SourceEdgeT.Should().Be(0.2);
        edge.TargetEdgeT.Should().Be(0.8);
        edge.SourcePoint!.X.Should().Be(10);
        edge.SourcePoint!.Y.Should().Be(20);
        edge.TargetPoint!.X.Should().Be(30);
        edge.TargetPoint!.Y.Should().Be(40);
        edge.SourceConstraint!.RelativeX.Should().Be(0);
        edge.TargetConstraint!.RelativeX.Should().Be(1);
        edge.SourceSpacing.Should().Be(5);
        edge.TargetSpacing.Should().Be(10);
        edge.SourceCardinality.Should().Be("1");
        edge.TargetCardinality.Should().Be("*");
        edge.StartArrow.Should().Be("none");
        edge.EndArrow.Should().Be("block");
        edge.StartArrowSize.Should().Be(6);
        edge.EndArrowSize.Should().Be(10);
        edge.StartArrowFill.Should().BeFalse();
        edge.EndArrowFill.Should().BeTrue();
        edge.Waypoints[0].X.Should().Be(0);
        edge.Waypoints[1].X.Should().Be(100);
    }

    [Fact]
    public void Execute_WithCustomAfterWaypoints_UsesProvidedWaypoints()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = "a",
            TargetNodeId = "b",
            Waypoints =
            [
                new DiagramPoint(0, 0),
                new DiagramPoint(100, 100),
            ]
        };

        var afterWaypoints = new List<DiagramPoint>
        {
            new(10, 10),
            new(20, 20),
            new(30, 30),
        };

        var cmd = new ReverseEdgeCommand(edge, afterWaypoints);
        cmd.Execute();

        edge.Waypoints.Should().HaveCount(3);
        edge.Waypoints[0].X.Should().Be(10);
        edge.Waypoints[2].X.Should().Be(30);
    }

    [Fact]
    public void Undo_AfterCustomWaypoints_RestoresOriginalWaypoints()
    {
        var edge = new DiagramEdge
        {
            SourceNodeId = "a",
            TargetNodeId = "b",
            Waypoints =
            [
                new DiagramPoint(0, 0),
                new DiagramPoint(100, 100),
            ]
        };

        var afterWaypoints = new List<DiagramPoint> { new(5, 5) };
        var cmd = new ReverseEdgeCommand(edge, afterWaypoints);
        cmd.Execute();
        cmd.Undo();

        edge.Waypoints.Should().HaveCount(2);
        edge.Waypoints[0].X.Should().Be(0);
        edge.Waypoints[1].X.Should().Be(100);
    }
}
