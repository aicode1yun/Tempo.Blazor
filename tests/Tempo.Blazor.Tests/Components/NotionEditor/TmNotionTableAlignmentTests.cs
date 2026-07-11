using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Blocks.Table;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Per-column alignment imported from a GFM separator row must reach the rendered cells.
/// </summary>
public sealed class TmNotionTableAlignmentTests : LocalizationTestBase
{
    public TmNotionTableAlignmentTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["TmNotionTableBlock_Loading"] = "Loading",
            ["TmNotionTableBlock_TableLabel"] = "Table",
            ["TmNotionTableBlock_AddRow"] = "New row",
            ["TmNotionTableBlock_AddColumn"] = "Add column",
            ["TmNotionTableBlock_DeleteColumn"] = "Delete column",
            ["TmNotionTableBlock_DeleteRow"] = "Delete row",
            ["TmNotionTableBlock_DragRow"] = "Drag row",
            ["TmNotionTableBlock_ToggleHeaderRow"] = "Header row",
            ["TmNotionTableBlock_ToggleHeaderColumn"] = "Header column",
            ["TmNotionTableBlock_SelectionTools"] = "Table selection tools",
            ["TmNotionTableBlock_Merge"] = "Merge cells",
            ["TmNotionTableBlock_Split"] = "Split",
            ["TmNotionTableBlock_Undo"] = "Undo table change",
            ["TmNotionTableBlock_ClearColor"] = "Clear color",
            ["TmNotionTableBlock_ColorYellow"] = "Yellow",
            ["TmNotionTableBlock_ColorGreen"] = "Green",
            ["TmNotionTableBlock_ColorBlue"] = "Blue",
            ["TmNotionTableBlock_ColorRed"] = "Red",
            ["TmNotionTableBlock_Sort"] = "Sort column"
        });
    }

    [Fact]
    public void RowBlock_AppliesAlignmentModifierPerColumn()
    {
        var cut = RenderRow(
            ["plain", "left", "center", "right"],
            [
                TableColumnAlignment.None,
                TableColumnAlignment.Left,
                TableColumnAlignment.Center,
                TableColumnAlignment.Right
            ]);

        var cells = cut.FindAll("td.tm-notion-table__cell-td");
        cells.Should().HaveCount(4);
        cells[0].ClassList.Should().NotContain(name => name.StartsWith("tm-notion-table__cell-td--align-"));
        cells[1].ClassList.Should().Contain("tm-notion-table__cell-td--align-left");
        cells[2].ClassList.Should().Contain("tm-notion-table__cell-td--align-center");
        cells[3].ClassList.Should().Contain("tm-notion-table__cell-td--align-right");
    }

    [Fact]
    public void RowBlock_WithoutAlignmentsRendersNoModifier()
    {
        var cut = RenderRow(["a", "b"], []);

        cut.FindAll("td.tm-notion-table__cell-td")
            .Should().OnlyContain(cell => !cell.ClassName!.Contains("--align-"));
    }

    [Fact]
    public void RowBlock_AlignmentListShorterThanRowLeavesRemainingCellsUnaligned()
    {
        var cut = RenderRow(["a", "b", "c"], [TableColumnAlignment.Right]);

        var cells = cut.FindAll("td.tm-notion-table__cell-td");
        cells[0].ClassList.Should().Contain("tm-notion-table__cell-td--align-right");
        cells[1].ClassName.Should().NotContain("--align-");
        cells[2].ClassName.Should().NotContain("--align-");
    }

    private IRenderedComponent<TmNotionTableRowBlock> RenderRow(
        string[] cells,
        TableColumnAlignment[] alignments)
    {
        var row = new PageBlock
        {
            Id = Guid.NewGuid(),
            PageId = Guid.NewGuid(),
            ParentBlockId = Guid.NewGuid(),
            Type = BlockType.TableRow,
            Order = 0,
            Content = new TableRowBlockContent { Cells = cells }
        };

        return RenderComponent<TmNotionTableRowBlock>(parameters => parameters
            .Add(p => p.Row, (IPageBlock)row)
            .Add(p => p.RowIndex, 0)
            .Add(p => p.ColumnCount, cells.Length)
            .Add(p => p.ColumnAlignments, alignments)
            .Add(p => p.ReadOnly, true));
    }
}
