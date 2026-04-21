using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class TmDiagramTableInserterTests : LocalizationTestBase
{
    [Fact]
    public void Render_DisplaysTitle()
    {
        var cut = RenderComponent<TmDiagramTableInserter>();

        cut.Find(".tm-diagram-table-inserter__title").TextContent.Should().Contain("Insert table");
    }

    [Fact]
    public void Render_GridHasCorrectNumberOfCells()
    {
        var cut = RenderComponent<TmDiagramTableInserter>();
        var cells = cut.FindAll(".tm-diagram-table-inserter__cell");

        cells.Count.Should().Be(80); // 10 cols × 8 rows
    }

    [Fact]
    public void HoverCell_HighlightsUpToThatCell()
    {
        var cut = RenderComponent<TmDiagramTableInserter>();
        var cells = cut.FindAll(".tm-diagram-table-inserter__cell");

        // Hover on cell at col=3, row=2 (0-based index 3 + 2*10 = 23)
        cells[23].MouseOver();

        // Cells from [0,0] to [3,2] should be highlighted: 4 cols × 3 rows = 12 cells
        var highlighted = cut.FindAll(".tm-diagram-table-inserter__cell--highlighted");
        highlighted.Count.Should().Be(12);
    }

    [Fact]
    public void ClickCell_InvokesOnInsertWithCorrectDimensions()
    {
        var callbackArgs = (-1, -1);
        var cut = RenderComponent<TmDiagramTableInserter>(
            parameters => parameters.Add(p => p.OnInsert, new EventCallback<(int Rows, int Columns)>(null, (Action<(int Rows, int Columns)>)(args => callbackArgs = (args.Rows, args.Columns)))));
        var cells = cut.FindAll(".tm-diagram-table-inserter__cell");

        // Click on cell at col=3, row=2 (0-based) → 3 rows × 4 cols (1-based)
        cells[23].Click();

        callbackArgs.Should().Be((3, 4));
    }

    [Fact]
    public void DefaultState_Highlights3x3()
    {
        var cut = RenderComponent<TmDiagramTableInserter>();

        // Default highlight is 3×3 (0,0 to 2,2)
        var highlighted = cut.FindAll(".tm-diagram-table-inserter__cell--highlighted");
        highlighted.Count.Should().Be(9);
    }

    [Fact]
    public void HoverCell_UpdatesDimensionsLabel()
    {
        var cut = RenderComponent<TmDiagramTableInserter>();
        var cells = cut.FindAll(".tm-diagram-table-inserter__cell");

        cells[23].MouseOver();

        cut.Find(".tm-diagram-table-inserter__dims").TextContent.Trim().Should().Be("3 × 4");
    }
}
