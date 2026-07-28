using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class ReplaceShapeCommandTests
{
    private static DiagramDocument EmptyDoc() => new()
    {
        Title = "Test", Width = 3000, Height = 2000, Nodes = [], Edges = []
    };

    private static DiagramNode MakeNode(string stencilId = "rect", double x = 0, double y = 0)
    {
        var node = new DiagramNode
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            StencilId = stencilId,
            X = x,
            Y = y,
            W = 160,
            H = 120,
            Data = new Dictionary<string, object> { ["name"] = "Test" },
            Style = new DiagramStyle { Fill = "#ff0000" }
        };
        node.Ports.Add(new DiagramPort { Id = "p1", Name = "Top", Side = PortSide.Top, Offset = 0.5, IsInput = true, IsOutput = true });
        node.Ports.Add(new DiagramPort { Id = "p2", Name = "Right", Side = PortSide.Right, Offset = 0.5, IsInput = true, IsOutput = true });
        return node;
    }

    private static DiagramEdge MakeEdge(string sourceId, string targetId, string? sourcePortId = null, string? targetPortId = null)
        => new()
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            SourceNodeId = sourceId,
            TargetNodeId = targetId,
            SourcePortId = sourcePortId,
            TargetPortId = targetPortId
        };

    [Fact]
    public void Execute_ChangesStencilAndRegeneratesPorts()
    {
        var doc = EmptyDoc();
        var node = MakeNode("old");
        doc.Nodes.Add(node);

        var newPorts = new List<DiagramPort>
        {
            new() { Id = "np1", Name = "Left", Side = PortSide.Left, Offset = 0.3, IsInput = true, IsOutput = false },
            new() { Id = "np2", Name = "Bottom", Side = PortSide.Bottom, Offset = 0.7, IsInput = false, IsOutput = true }
        };

        var cmd = new ReplaceShapeCommand(doc, node.Id, "new", newPorts, 200, 150);
        cmd.Execute();

        node.StencilId.Should().Be("new");
        node.W.Should().Be(200);
        node.H.Should().Be(150);
        node.Ports.Should().HaveCount(2);
        node.Ports.Select(p => p.Side).Should().ContainInOrder(PortSide.Left, PortSide.Bottom);
    }

    [Fact]
    public void Undo_RestoresOriginalStencilAndPorts()
    {
        var doc = EmptyDoc();
        var node = MakeNode("old");
        doc.Nodes.Add(node);

        var newPorts = new List<DiagramPort>
        {
            new() { Id = "np1", Name = "Left", Side = PortSide.Left, Offset = 0.3, IsInput = true, IsOutput = false }
        };

        var cmd = new ReplaceShapeCommand(doc, node.Id, "new", newPorts, 200, 150);
        cmd.Execute();
        cmd.Undo();

        node.StencilId.Should().Be("old");
        node.W.Should().Be(160);
        node.H.Should().Be(120);
        node.Ports.Should().HaveCount(2);
        // ONE collection, not loose arguments. FluentAssertions 8.4.0 (the version this is measured against —
        // re-check if it is upgraded) has no `params` overload of Contain for a COLLECTION subject, so
        // `Contain("Top", "Right")` binds to Contain(expected, because, becauseArgs): only "Top" would be
        // asserted and "Right" would silently become failure-message text. Measured, not assumed: replacing
        // "Right" with a nonsense string left this test GREEN, while doing the same to "Top" turned it RED.
        // The subject's TYPE is what decides this — over a Dictionary the same shape binds Contain(key, value)
        // and does assert both — so no grep or regex over the call shape can tell the two apart.
        node.Ports.Select(p => p.Name).Should().Contain(new[] { "Top", "Right" });
    }

    [Fact]
    public void Execute_RemapsConnectedEdges_ToNearestPorts()
    {
        var doc = EmptyDoc();
        var nodeA = MakeNode("old");
        var nodeB = MakeNode("other");
        doc.Nodes.Add(nodeA);
        doc.Nodes.Add(nodeB);

        var edge1 = MakeEdge(nodeA.Id, nodeB.Id, "p1", null); // A -> B using Top port of A
        var edge2 = MakeEdge(nodeB.Id, nodeA.Id, null, "p2"); // B -> A using Right port of A
        doc.Edges.Add(edge1);
        doc.Edges.Add(edge2);

        var newPorts = new List<DiagramPort>
        {
            new() { Id = "np1", Name = "Top", Side = PortSide.Top, Offset = 0.5, IsInput = true, IsOutput = true },
            new() { Id = "np2", Name = "Left", Side = PortSide.Left, Offset = 0.5, IsInput = true, IsOutput = true }
        };

        var cmd = new ReplaceShapeCommand(doc, nodeA.Id, "new", newPorts, 200, 150);
        cmd.Execute();

        edge1.SourcePortId.Should().Be("np1"); // same side Top
        edge2.TargetPortId.Should().Be("np1"); // old Right -> fallback to first available (np1 because np1 is IsInput)

        cmd.Undo();

        edge1.SourcePortId.Should().Be("p1");
        edge2.TargetPortId.Should().Be("p2");
    }

    [Fact]
    public void Execute_PreservesDataStyleAndPosition()
    {
        var doc = EmptyDoc();
        var node = MakeNode("old", x: 50, y: 100);
        node.ZIndex = 5;
        node.LayerId = "layer1";
        node.GroupId = "group1";
        doc.Nodes.Add(node);

        var newPorts = new List<DiagramPort> { new() { Id = "np1", Name = "Top", Side = PortSide.Top } };
        var cmd = new ReplaceShapeCommand(doc, node.Id, "new", newPorts, 200, 150);
        cmd.Execute();

        node.X.Should().Be(50);
        node.Y.Should().Be(100);
        node.ZIndex.Should().Be(5);
        node.LayerId.Should().Be("layer1");
        node.GroupId.Should().Be("group1");
        node.Data["name"].Should().Be("Test");
        node.Style.Fill.Should().Be("#ff0000");
    }
}
