using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.NotionEditor.Models;
using TableBlockContent = Tempo.Blazor.NotionEditor.Models.TableBlockContent;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Adding or removing a table column must carry canonical rich cells (merges, background colours)
/// and per-column metadata without reading the removed legacy plain-cell list.
/// </summary>
public sealed class NotionTableEditTests
{
    [Fact]
    public void AddColumn_KeepsTheRichCellsAndAppendsOne()
    {
        var row = Row(["a", "b"], [Rich("a", "yellow"), Rich("b")]);

        var updated = NotionTableEdit.AddColumn(row);

        updated.Cells.Should().BeEmpty("canonical table edits never reconstruct legacy plain cells");
        updated.RichCells.Should().HaveCount(3);
        updated.RichCells[0].BackgroundColor.Should().Be("yellow", "the colour must survive the edit");
        updated.RichCells[2].Html.Should().BeEmpty();
    }

    [Fact]
    public void AddColumn_OnAnEmptyCanonicalRow_AppendsOneEmptyRichCell()
    {
        var updated = NotionTableEdit.AddColumn(Row(["a"], []));

        updated.Cells.Should().BeEmpty();
        updated.RichCells.Should().ContainSingle();
        updated.RichCells[0].Html.Should().BeEmpty();
    }

    [Fact]
    public void RemoveColumn_DropsThatColumnFromCanonicalRichCells()
    {
        var row = Row(["a", "b", "c"], [Rich("a"), Rich("b", "green"), Rich("c")]);

        var updated = NotionTableEdit.RemoveColumn(row, 1);

        updated.Cells.Should().BeEmpty("canonical table edits never write legacy plain cells");
        updated.RichCells.Should().HaveCount(2);
        updated.RichCells.Select(cell => cell.Html).Should().Equal("a", "c");
    }

    [Fact]
    public void RemoveColumn_KeepsAMergeOnASurvivingCell()
    {
        var merged = Rich("a");
        merged.ColSpan = 2;
        var row = Row(["a", "b", "c"], [merged, Rich("b"), Rich("c")]);

        var updated = NotionTableEdit.RemoveColumn(row, 2);

        updated.RichCells[0].ColSpan.Should().Be(2);
    }

    [Fact]
    public void AddColumn_KeepsHeaderFlagsAndAlignments()
    {
        var table = Table(2, [TableColumnAlignment.Left, TableColumnAlignment.Right], headerRow: true);

        var updated = NotionTableEdit.AddColumn(table);

        updated.ColumnCount.Should().Be(3);
        updated.HasHeaderRow.Should().BeTrue();
        updated.ColumnAlignments.Should().Equal(
            TableColumnAlignment.Left, TableColumnAlignment.Right, TableColumnAlignment.None);
    }

    [Fact]
    public void AddColumn_OnATableWithoutAlignments_LeavesTheListEmpty()
    {
        var updated = NotionTableEdit.AddColumn(Table(2, [], headerRow: false));

        updated.ColumnCount.Should().Be(3);
        updated.ColumnAlignments.Should().BeEmpty("an empty alignment list means 'the renderer decides'");
    }

    [Fact]
    public void RemoveColumn_DropsThatColumnsAlignment()
    {
        var table = Table(3,
            [TableColumnAlignment.Left, TableColumnAlignment.Center, TableColumnAlignment.Right],
            headerRow: false);

        var updated = NotionTableEdit.RemoveColumn(table, 1);

        updated.ColumnCount.Should().Be(2);
        updated.ColumnAlignments.Should().Equal(TableColumnAlignment.Left, TableColumnAlignment.Right);
    }

    [Fact]
    public void RemoveColumn_NeverDropsBelowOneColumn()
    {
        NotionTableEdit.RemoveColumn(Table(1, [], headerRow: false), 0).ColumnCount.Should().Be(1);
    }

    [Fact]
    public void RemoveColumn_KeepsTheHeaderColumnFlag()
    {
        var table = new TableBlockContent { ColumnCount = 2, HasHeaderColumn = true };

        NotionTableEdit.RemoveColumn(table, 0).HasHeaderColumn.Should().BeTrue();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static TableRowBlockContent Row(string[] cells, NotionTableCell[] rich) =>
        new() { Cells = cells, RichCells = rich };

    private static NotionTableCell Rich(string html, string? background = null) =>
        new() { Html = html, BackgroundColor = background };

    private static TableBlockContent Table(int columns, TableColumnAlignment[] alignments, bool headerRow) =>
        new() { ColumnCount = columns, ColumnAlignments = alignments, HasHeaderRow = headerRow };
}
