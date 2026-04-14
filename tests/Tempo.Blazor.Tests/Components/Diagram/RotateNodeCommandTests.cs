using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class RotateNodeCommandTests
{
    [Fact]
    public void Execute_SetsNewRotation()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { Rotation = 0 };
        doc.Nodes.Add(node);

        var cmd = new RotateNodeCommand(doc, node.Id, 0, 45);
        cmd.Execute();

        node.Rotation.Should().Be(45);
    }

    [Fact]
    public void Undo_RestoresOldRotation()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { Rotation = 30 };
        doc.Nodes.Add(node);

        var cmd = new RotateNodeCommand(doc, node.Id, 30, 90);
        cmd.Execute();
        cmd.Undo();

        node.Rotation.Should().Be(30);
    }
}
