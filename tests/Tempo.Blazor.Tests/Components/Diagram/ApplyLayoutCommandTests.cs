using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class ApplyLayoutCommandTests
{
    [Fact]
    public void Execute_SetsNewPositions()
    {
        var doc = new DiagramDocument();
        var node1 = new DiagramNode { X = 10, Y = 20 };
        var node2 = new DiagramNode { X = 30, Y = 40 };
        doc.Nodes.Add(node1);
        doc.Nodes.Add(node2);

        var newPositions = new Dictionary<string, (double X, double Y)>
        {
            [node1.Id] = (100, 200),
            [node2.Id] = (300, 400),
        };

        var cmd = new ApplyLayoutCommand(doc, newPositions);
        cmd.Execute();

        node1.X.Should().Be(100);
        node1.Y.Should().Be(200);
        node2.X.Should().Be(300);
        node2.Y.Should().Be(400);
    }

    [Fact]
    public void Undo_RestoresOldPositions()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { X = 50, Y = 60 };
        doc.Nodes.Add(node);

        var newPositions = new Dictionary<string, (double X, double Y)>
        {
            [node.Id] = (500, 600),
        };

        var cmd = new ApplyLayoutCommand(doc, newPositions);
        cmd.Execute();
        cmd.Undo();

        node.X.Should().Be(50);
        node.Y.Should().Be(60);
    }

    [Fact]
    public void Execute_IgnoresMissingNodes()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { X = 0, Y = 0 };
        doc.Nodes.Add(node);

        var newPositions = new Dictionary<string, (double X, double Y)>
        {
            [node.Id] = (10, 20),
            ["missing"] = (99, 99),
        };

        var cmd = new ApplyLayoutCommand(doc, newPositions);
        cmd.Execute();

        node.X.Should().Be(10);
        node.Y.Should().Be(20);
    }
}
