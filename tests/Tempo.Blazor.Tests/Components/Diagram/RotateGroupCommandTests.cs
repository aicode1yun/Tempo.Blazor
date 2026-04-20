using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class RotateGroupCommandTests
{
    [Fact]
    public void Execute_RotatesGroupAndMembers()
    {
        var doc = new DiagramDocument();
        var group = new DiagramNode { StencilId = "general.group", Rotation = 0 };
        var child1 = new DiagramNode { ParentGroupId = group.Id, GroupId = group.Id, Rotation = 10 };
        var child2 = new DiagramNode { ParentGroupId = group.Id, GroupId = group.Id, Rotation = 20 };
        doc.Nodes.Add(group);
        doc.Nodes.Add(child1);
        doc.Nodes.Add(child2);

        var oldRotations = new Dictionary<string, double>
        {
            [group.Id] = 0,
            [child1.Id] = 10,
            [child2.Id] = 20
        };
        var newRotations = new Dictionary<string, double>
        {
            [group.Id] = 30,
            [child1.Id] = 40,
            [child2.Id] = 50
        };

        var cmd = new RotateGroupCommand(doc, oldRotations, newRotations);
        cmd.Execute();

        group.Rotation.Should().Be(30);
        child1.Rotation.Should().Be(40);
        child2.Rotation.Should().Be(50);
    }

    [Fact]
    public void Undo_RestoresOldRotations()
    {
        var doc = new DiagramDocument();
        var group = new DiagramNode { StencilId = "general.group", Rotation = 30 };
        var child1 = new DiagramNode { ParentGroupId = group.Id, GroupId = group.Id, Rotation = 40 };
        doc.Nodes.Add(group);
        doc.Nodes.Add(child1);

        var oldRotations = new Dictionary<string, double>
        {
            [group.Id] = 0,
            [child1.Id] = 10
        };
        var newRotations = new Dictionary<string, double>
        {
            [group.Id] = 30,
            [child1.Id] = 40
        };

        var cmd = new RotateGroupCommand(doc, oldRotations, newRotations);
        cmd.Undo();

        group.Rotation.Should().Be(0);
        child1.Rotation.Should().Be(10);
    }
}
