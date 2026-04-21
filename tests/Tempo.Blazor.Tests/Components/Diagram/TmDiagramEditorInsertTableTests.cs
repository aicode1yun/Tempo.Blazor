using System.Reflection;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class TmDiagramEditorInsertTableTests : LocalizationTestBase
{
    public TmDiagramEditorInsertTableTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task InsertTable_AddsNodeWithTableBasicStencil()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();

        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc)
            .Add(e => e.ReadOnly, false));

        await InvokeInsertTable(cut, 3, 4);

        doc.Nodes.Should().ContainSingle();
        doc.Nodes[0].StencilId.Should().Be("table.basic");
    }

    [Fact]
    public async Task InsertTable_SetsRowCountAndColumnCount()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();

        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc)
            .Add(e => e.ReadOnly, false));

        await InvokeInsertTable(cut, 3, 4);

        var node = doc.Nodes[0];
        node.Data["rowCount"].Should().Be(3);
        node.Data["columnCount"].Should().Be(4);
    }

    [Fact]
    public async Task InsertTable_GeneratesHeaderCells()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();

        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc)
            .Add(e => e.ReadOnly, false));

        await InvokeInsertTable(cut, 3, 4);

        var node = doc.Nodes[0];
        var cells = node.Data["cells"] as System.Collections.Generic.List<DiagramTableCellData>;
        cells.Should().NotBeNull();
        cells!.Count.Should().Be(12); // 3 × 4

        // First row cells (0..3) should have bold header style
        for (int c = 0; c < 4; c++)
        {
            var cell = cells.First(x => x.Row == 0 && x.Column == c);
            cell.Style.Should().NotBeNull();
            cell.Style!.FontWeight.Should().Be("bold");
            cell.Style.BackgroundColor.Should().Be("#f3f4f6");
            cell.Text.Should().Be($"Header {c + 1}");
        }

        // Second row cells should have no style
        for (int c = 0; c < 4; c++)
        {
            var cell = cells.First(x => x.Row == 1 && x.Column == c);
            cell.Style.Should().BeNull();
            cell.Text.Should().Be($"Cell 1,{c}");
        }
    }

    [Fact]
    public async Task InsertTable_AddsNodeAtCenterOfViewport()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();
        var page = doc.ActivePage;
        page.Width = 800;
        page.Height = 600;

        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc)
            .Add(e => e.ReadOnly, false)
            .Add(e => e.GridSize, 8));

        await InvokeInsertTable(cut, 3, 4);

        var node = doc.Nodes[0];
        // w = 4*80+20 = 340, h = 3*30+20 = 110
        // x = round((800/2 - 340/2) / 8) * 8 = round(230/8)*8 = 29*8 = 232
        // y = round((600/2 - 110/2) / 8) * 8 = round(245/8)*8 = 31*8 = 248
        node.X.Should().BeApproximately(232, 1);
        node.Y.Should().BeApproximately(248, 1);
        node.W.Should().Be(340);
        node.H.Should().Be(110);
    }

    private static async Task InvokeInsertTable(IRenderedComponent<TmDiagramEditor> cut, int rows, int columns)
    {
        var method = typeof(TmDiagramEditor).GetMethod("InsertTable", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        await cut.InvokeAsync(async () =>
        {
            await (Task)method!.Invoke(cut.Instance, new object[] { rows, columns })!;
        });
    }
}
