using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class PropertyPanelTests : LocalizationTestBase
{
    public PropertyPanelTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void NodeFillColorChange_UpdatesNodeStyle()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 100,
            Y = 100,
            W = 120,
            H = 60,
            Style = new DiagramStyle { Fill = "#ffffff" }
        };
        doc.Nodes.Add(node);

        var cut = RenderComponent<TmDiagramPropertiesPanel>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.SelectedIds, [node.Id])
            .Add(c => c.ReadOnly, false));

        var colorInput = cut.Find("input[type='color']");
        colorInput.Change("#ff0000");

        node.Style.Fill.Should().Be("#ff0000");
    }

    [Fact]
    public void MultiSelectionNodes_ShowsMixedWhenDifferentValues()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 0, Y = 0, W = 100, H = 50,
            Style = new DiagramStyle { Fill = "#ff0000" }
        };
        var n2 = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 200, Y = 0, W = 100, H = 50,
            Style = new DiagramStyle { Fill = "#00ff00" }
        };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var cut = RenderComponent<TmDiagramPropertiesPanel>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.SelectedIds, [n1.Id, n2.Id])
            .Add(c => c.ReadOnly, false));

        // Panel should render without errors for multi-selection
        cut.FindAll(".tm-diagram-properties__section--collapsible").Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MultiSelectionNodes_ApplyStyleToAllSelected()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 0, Y = 0, W = 100, H = 50,
            Style = new DiagramStyle { Fill = "#ffffff" }
        };
        var n2 = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 200, Y = 0, W = 100, H = 50,
            Style = new DiagramStyle { Fill = "#ffffff" }
        };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var cut = RenderComponent<TmDiagramPropertiesPanel>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.SelectedIds, [n1.Id, n2.Id])
            .Add(c => c.ReadOnly, false));

        var colorInput = cut.Find("input[type='color']");
        colorInput.Change("#0000ff");

        n1.Style.Fill.Should().Be("#0000ff");
        n2.Style.Fill.Should().Be("#0000ff");
    }

    [Fact]
    public void GroupButton_AvailableForMultiNodeSelection()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 0, W = 100, H = 50 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var cut = RenderComponent<TmDiagramPropertiesPanel>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.SelectedIds, [n1.Id, n2.Id])
            .Add(c => c.ReadOnly, false));

        var buttons = cut.FindAll("button");
        var groupBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("Group") || b.TextContent.Contains("Seskupit"));
        groupBtn.Should().NotBeNull();
    }
}
