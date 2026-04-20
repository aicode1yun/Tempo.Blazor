using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class ResizeGroupCommandTests
{
    [Fact]
    public void Execute_ResizesGroupAndMembers()
    {
        var doc = new DiagramDocument();
        var group = new DiagramNode { StencilId = "general.group", X = 0, Y = 0, W = 200, H = 200 };
        var child = new DiagramNode { ParentGroupId = group.Id, GroupId = group.Id, X = 20, Y = 20, W = 50, H = 50 };
        doc.Nodes.Add(group);
        doc.Nodes.Add(child);

        var oldRects = new Dictionary<string, NodeRect>
        {
            [group.Id] = new NodeRect(0, 0, 200, 200),
            [child.Id] = new NodeRect(20, 20, 50, 50)
        };
        var newRects = new Dictionary<string, NodeRect>
        {
            [group.Id] = new NodeRect(0, 0, 400, 300),
            [child.Id] = new NodeRect(40, 30, 100, 75)
        };

        var cmd = new ResizeGroupCommand(doc, oldRects, newRects);
        cmd.Execute();

        group.W.Should().Be(400);
        group.H.Should().Be(300);
        child.X.Should().Be(40);
        child.Y.Should().Be(30);
        child.W.Should().Be(100);
        child.H.Should().Be(75);
    }

    [Fact]
    public void Undo_RestoresOldRects()
    {
        var doc = new DiagramDocument();
        var group = new DiagramNode { StencilId = "general.group", X = 0, Y = 0, W = 400, H = 300 };
        var child = new DiagramNode { ParentGroupId = group.Id, GroupId = group.Id, X = 40, Y = 30, W = 100, H = 75 };
        doc.Nodes.Add(group);
        doc.Nodes.Add(child);

        var oldRects = new Dictionary<string, NodeRect>
        {
            [group.Id] = new NodeRect(0, 0, 200, 200),
            [child.Id] = new NodeRect(20, 20, 50, 50)
        };
        var newRects = new Dictionary<string, NodeRect>
        {
            [group.Id] = new NodeRect(0, 0, 400, 300),
            [child.Id] = new NodeRect(40, 30, 100, 75)
        };

        var cmd = new ResizeGroupCommand(doc, oldRects, newRects);
        cmd.Undo();

        group.W.Should().Be(200);
        group.H.Should().Be(200);
        child.X.Should().Be(20);
        child.Y.Should().Be(20);
        child.W.Should().Be(50);
        child.H.Should().Be(50);
    }
}
