using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class DiagramCommandTests
{
    [Fact]
    public void CutNodesCommand_CopiesAndRemovesNodes()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var node = new DiagramNode { Id = "n1", StencilId = "general.rectangle" };
        doc.Nodes.Add(node);

        var cmd = new CutNodesCommand(doc, ["n1"]);
        cmd.Execute();

        doc.Nodes.Should().BeEmpty();
        DiagramClipboard.HasNodes.Should().BeTrue();

        cmd.Undo();
        doc.Nodes.Should().ContainSingle(n => n.Id == "n1");
    }

    [Fact]
    public void DuplicateNodesCommand_CreatesCopyWithOffset()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var node = new DiagramNode { Id = "n1", StencilId = "general.rectangle", X = 10, Y = 20 };
        doc.Nodes.Add(node);

        var cmd = new DuplicateNodesCommand(doc, ["n1"], 30, 40);
        cmd.Execute();

        doc.Nodes.Count.Should().Be(2);
        var copy = doc.Nodes.First(n => n.Id != "n1");
        copy.X.Should().Be(40);
        copy.Y.Should().Be(60);

        cmd.Undo();
        doc.Nodes.Count.Should().Be(1);
    }

    [Fact]
    public void LockNodesCommand_SetsIsLocked()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var node = new DiagramNode { Id = "n1", StencilId = "general.rectangle" };
        doc.Nodes.Add(node);

        var cmd = new LockNodesCommand(doc, ["n1"]);
        cmd.Execute();

        node.IsLocked.Should().BeTrue();

        cmd.Undo();
        node.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void UnlockNodesCommand_ClearsIsLocked()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var node = new DiagramNode { Id = "n1", StencilId = "general.rectangle", IsLocked = true };
        doc.Nodes.Add(node);

        var cmd = new UnlockNodesCommand(doc, ["n1"]);
        cmd.Execute();

        node.IsLocked.Should().BeFalse();

        cmd.Undo();
        node.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void UpdateEdgeArrowheadsCommand_ChangesArrowheads()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var edge = new DiagramEdge { Id = "e1", SourceNodeId = "a", TargetNodeId = "b", StartArrow = "none", EndArrow = "classic" };
        doc.Edges.Add(edge);

        var cmd = new UpdateEdgeArrowheadsCommand(doc, ["e1"], newStartArrow: "block", newEndArrow: "crow");
        cmd.Execute();

        edge.StartArrow.Should().Be("block");
        edge.EndArrow.Should().Be("crow");

        cmd.Undo();
        edge.StartArrow.Should().Be("none");
        edge.EndArrow.Should().Be("classic");
    }

    [Fact]
    public void UpdateEdgeLineStyleCommand_ChangesDasharray()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var edge = new DiagramEdge { Id = "e1", SourceNodeId = "a", TargetNodeId = "b" };
        edge.Style.StrokeDasharray = null;
        doc.Edges.Add(edge);

        var cmd = new UpdateEdgeLineStyleCommand(doc, ["e1"], "5,5");
        cmd.Execute();

        edge.Style.StrokeDasharray.Should().Be("5,5");

        cmd.Undo();
        edge.Style.StrokeDasharray.Should().BeNull();
    }

    [Fact]
    public void PasteNodesCommand_InternalClipboard_PastesWithoutJsRuntime()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var node = new DiagramNode { Id = "n1", StencilId = "general.rectangle", X = 10, Y = 20 };
        doc.Nodes.Add(node);
        new CopyNodesCommand(doc, ["n1"]).Execute();

        var pasteCmd = new PasteNodesCommand(doc, 5, 10, useInternalClipboard: true);
        pasteCmd.Execute();

        doc.Nodes.Count.Should().Be(2);
        pasteCmd.PastedNodes.Count.Should().Be(1);
        var pasted = pasteCmd.PastedNodes[0];
        pasted.X.Should().Be(15);
        pasted.Y.Should().Be(30);

        pasteCmd.Undo();
        doc.Nodes.Count.Should().Be(1);
    }

    [Fact]
    public void PasteNodesCommand_PasteHere_PlacesCenterAtTarget()
    {
        var doc = new DiagramDocument { Title = "Test" };
        var node = new DiagramNode { Id = "n1", StencilId = "general.rectangle", X = 10, Y = 20, W = 160, H = 120 };
        doc.Nodes.Add(node);
        new CopyNodesCommand(doc, ["n1"]).Execute();

        var pasteCmd = new PasteNodesCommand(doc, 200, 200, useInternalClipboard: true, pasteHere: true);
        pasteCmd.Execute();

        doc.Nodes.Count.Should().Be(2);
        pasteCmd.PastedNodes.Count.Should().Be(1);
        var pasted = pasteCmd.PastedNodes[0];
        // original center = (10 + 80, 20 + 60) = (90, 80)
        // offset = (200 - 90, 200 - 80) = (110, 120)
        // new position = (10 + 110, 20 + 120) = (120, 140)
        pasted.X.Should().Be(120);
        pasted.Y.Should().Be(140);

        pasteCmd.Undo();
        doc.Nodes.Count.Should().Be(1);
    }
}
