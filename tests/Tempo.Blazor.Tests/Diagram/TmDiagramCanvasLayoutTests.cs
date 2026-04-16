using FluentAssertions;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class TmDiagramCanvasLayoutTests : DiagramTestBase
{
    [Fact]
    public async Task OnLayoutApplied_UpdatesNodePositions()
    {
        var doc = new DiagramDocument
        {
            Title = "Test",
            Width = 1000,
            Height = 1000,
            Nodes =
            {
                new DiagramNode { Id = "n1", StencilId = "rect", X = 10, Y = 20, W = 100, H = 100 }
            }
        };

        var cut = RenderComponent<TmDiagramCanvas>(parameters => parameters
            .Add(p => p.Document, doc));

        var field = typeof(TmDiagramCanvas).GetField("_jsInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(cut.Instance, true);

        var moves = new[]
        {
            new Tempo.Blazor.Components.Diagram.TmDiagramCanvas.ElementMove { Id = "n1", X = 500, Y = 600 }
        };

        await cut.Instance.OnLayoutApplied(moves);

        doc.Nodes[0].X.Should().Be(500);
        doc.Nodes[0].Y.Should().Be(600);
    }
}
