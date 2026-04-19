using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class ArcSizeTests : LocalizationTestBase
{
    public ArcSizeTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void EdgeWithCustomArcSize_RendersLargerRoundedPath()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 100, W = 100, H = 50 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            Routing = "orthogonal",
            Rounded = true,
            ArcSize = 20,
            Waypoints = [new DiagramPoint(100, 25), new DiagramPoint(150, 25), new DiagramPoint(150, 100), new DiagramPoint(200, 100)]
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var path = cut.Find("path.tm-diagram-edge-path");
        var d = path.GetAttribute("d");
        d.Should().Contain("Q"); // quadratic bezier for rounded corners
    }

    [Fact]
    public void PropertiesPanel_ArcSizeInput_ChangesValue()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 0, W = 100, H = 50 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            Routing = "orthogonal",
            Rounded = true,
            ArcSize = 8
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramPropertiesPanel>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.SelectedIds, [edge.Id])
            .Add(c => c.ReadOnly, false));

        var field = cut.FindAll(".tm-diagram-properties__field")
            .FirstOrDefault(f => f.QuerySelector("label")?.TextContent.Contains("Radius") == true
                              || f.QuerySelector("label")?.TextContent.Contains("zaoblení") == true);
        field.Should().NotBeNull();
        var input = field!.QuerySelector("input[type='number']");
        input.Should().NotBeNull();
        input!.Change("15");

        edge.ArcSize.Should().Be(15);
    }
}
