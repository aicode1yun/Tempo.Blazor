using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class ToggleCollapseCommandTests
{
    private static DiagramDocument EmptyDoc() => new()
    {
        Title = "Test", Width = 3000, Height = 2000, Nodes = [], Edges = []
    };

    [Fact]
    public void Execute_Collapse_SetsCollapsedTrueAndStoresHeight()
    {
        var doc = EmptyDoc();
        var node = new DiagramNode { Id = "n1", StencilId = "uml.package", W = 200, H = 160, IsCollapsible = true };
        doc.Nodes.Add(node);

        var cmd = new ToggleCollapseCommand(doc, "n1");
        cmd.Execute();

        node.Collapsed.Should().BeTrue();
        node.ExpandedHeight.Should().Be(160);
        node.H.Should().Be(40);
    }

    [Fact]
    public void Execute_Expand_RestoresHeight()
    {
        var doc = EmptyDoc();
        var node = new DiagramNode { Id = "n1", StencilId = "uml.package", W = 200, H = 40, IsCollapsible = true, Collapsed = true, ExpandedHeight = 160 };
        doc.Nodes.Add(node);

        var cmd = new ToggleCollapseCommand(doc, "n1");
        cmd.Execute();

        node.Collapsed.Should().BeFalse();
        node.H.Should().Be(160);
    }

    [Fact]
    public void Undo_AfterCollapse_RestoresOriginalState()
    {
        var doc = EmptyDoc();
        var node = new DiagramNode { Id = "n1", StencilId = "uml.package", W = 200, H = 160, IsCollapsible = true };
        doc.Nodes.Add(node);

        var cmd = new ToggleCollapseCommand(doc, "n1");
        cmd.Execute();
        cmd.Undo();

        node.Collapsed.Should().BeFalse();
        node.H.Should().Be(160);
        node.ExpandedHeight.Should().Be(0);
    }

    [Fact]
    public void Undo_AfterExpand_RestoresOriginalCollapsedState()
    {
        var doc = EmptyDoc();
        var node = new DiagramNode { Id = "n1", StencilId = "uml.package", W = 200, H = 40, IsCollapsible = true, Collapsed = true, ExpandedHeight = 180 };
        doc.Nodes.Add(node);

        var cmd = new ToggleCollapseCommand(doc, "n1");
        cmd.Execute();
        cmd.Undo();

        node.Collapsed.Should().BeTrue();
        node.H.Should().Be(40);
        node.ExpandedHeight.Should().Be(180);
    }
}
