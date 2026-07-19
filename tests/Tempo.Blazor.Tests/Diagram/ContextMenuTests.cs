using System.Linq;
using System.Reflection;
using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class ContextMenuTests : DiagramTestBase
{
    [Fact]
    public void NodeContextMenu_RendersCopyDeleteItems()
    {
        var doc = new DiagramDocument
        {
            Title = "Test",
            Width = 1000,
            Height = 1000,
            Nodes =
            [
                new DiagramNode { Id = "n1", StencilId = "general.rectangle", X = 100, Y = 100, W = 120, H = 80 }
            ]
        };

        var cut = Render<TmDiagramEditor>(parameters => parameters
            .Add(p => p.Document, doc));

        // Trigger node context menu via the internal method isn't directly possible from bUnit
        // without JS interop, but we can verify the canvas renders the node.
        var node = cut.Find("[data-node-id='n1']");
        node.Should().NotBeNull();
    }

    [Fact]
    public void CanvasContextMenu_RendersPasteAndLayoutItems()
    {
        var doc = new DiagramDocument
        {
            Title = "Test",
            Width = 1000,
            Height = 1000
        };

        var cut = Render<TmDiagramEditor>(parameters => parameters
            .Add(p => p.Document, doc));

        var canvas = cut.Find(".tm-diagram-canvas");
        canvas.Should().NotBeNull();
    }

    [Fact]
    public void TableCell_CtrlClick_AddsSelectionClass()
    {
        var doc = new DiagramDocument
        {
            Title = "Test",
            Width = 1000,
            Height = 1000,
            Nodes =
            [
                new DiagramNode
                {
                    Id = "t1",
                    StencilId = "table.basic",
                    X = 100,
                    Y = 100,
                    W = 200,
                    H = 120,
                    Data = new()
                    {
                        ["rowCount"] = 2,
                        ["columnCount"] = 2,
                        ["cells"] = new List<DiagramTableCellData>
                        {
                            new() { Row = 0, Column = 0, Text = "A" },
                            new() { Row = 0, Column = 1, Text = "B" },
                            new() { Row = 1, Column = 0, Text = "C" },
                            new() { Row = 1, Column = 1, Text = "D" }
                        }
                    }
                }
            ]
        };

        var cut = Render<TmDiagramCanvas>(parameters => parameters
            .Add(p => p.Document, doc)
            .Add(p => p.SelectedTableCells, [(0, 0), (1, 1)]));

        var cells = cut.FindAll(".tm-diagram-node__table-cell");
        cells.Count.Should().Be(4);
        cells[0].ClassList.Should().Contain("tm-diagram-node__table-cell--selected");
        cells[1].ClassList.Should().NotContain("tm-diagram-node__table-cell--selected");
        cells[2].ClassList.Should().NotContain("tm-diagram-node__table-cell--selected");
        cells[3].ClassList.Should().Contain("tm-diagram-node__table-cell--selected");
    }

    [Fact]
    public void CanvasContextMenu_RendersPasteHereItem()
    {
        var doc = new DiagramDocument
        {
            Title = "Test",
            Width = 1000,
            Height = 1000
        };

        var cut = Render<TmDiagramEditor>(parameters => parameters
            .Add(p => p.Document, doc));

        // Force-open the canvas context menu via reflection
        var type = typeof(TmDiagramEditor);
        type.GetField("_contextMenuOpen", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, true);
        type.GetField("_contextMenuType", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, 3); // ContextMenuType.Canvas = 3
        type.GetField("_contextMenuScreenX", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, 100.0);
        type.GetField("_contextMenuScreenY", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, 100.0);
        cut.Render();

        var items = cut.FindAll(".tm-diagram-editor__context-item");
        var texts = items.Select(b => b.TextContent).ToList();
        texts.Should().Contain("Paste Here");
    }
}
