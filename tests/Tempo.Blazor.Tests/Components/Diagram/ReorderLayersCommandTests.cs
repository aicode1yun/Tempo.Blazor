using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class ReorderLayersCommandTests
{
    [Fact]
    public void Execute_ChangesLayerOrder()
    {
        var doc = new DiagramDocument();
        var layer1 = new DiagramLayer { Name = "A", Order = 0 };
        var layer2 = new DiagramLayer { Name = "B", Order = 1 };
        var layer3 = new DiagramLayer { Name = "C", Order = 2 };
        doc.Layers.Add(layer1);
        doc.Layers.Add(layer2);
        doc.Layers.Add(layer3);

        var newOrders = new Dictionary<string, int>
        {
            [layer1.Id] = 2,
            [layer2.Id] = 0,
            [layer3.Id] = 1
        };

        var cmd = new ReorderLayersCommand(doc, newOrders);
        cmd.Execute();

        layer1.Order.Should().Be(2);
        layer2.Order.Should().Be(0);
        layer3.Order.Should().Be(1);
    }

    [Fact]
    public void Undo_RestoresOriginalOrder()
    {
        var doc = new DiagramDocument();
        var layer1 = new DiagramLayer { Name = "A", Order = 0 };
        var layer2 = new DiagramLayer { Name = "B", Order = 1 };
        doc.Layers.Add(layer1);
        doc.Layers.Add(layer2);

        var cmd = new ReorderLayersCommand(doc, new Dictionary<string, int>
        {
            [layer1.Id] = 1,
            [layer2.Id] = 0
        });

        cmd.Execute();
        cmd.Undo();

        layer1.Order.Should().Be(0);
        layer2.Order.Should().Be(1);
    }
}
