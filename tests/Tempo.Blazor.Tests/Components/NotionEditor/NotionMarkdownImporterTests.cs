using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Markdown tabulky musí vzniknout jako jeden Table blok s child TableRow bloky
/// (stejný tvar, jaký zapisuje TmNotionTableBlock.AddRowAsync a čte LoadRowsAsync).
/// </summary>
public class NotionMarkdownImporterTests
{
    private static readonly Guid PageId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Import_TableProducesSingleTableParentWithChildRows()
    {
        const string markdown = """
            | Name | Status |
            | --- | --- |
            | CF26 | Ready |
            | CF27 | Draft |
            """;

        var blocks = NotionMarkdownImporter.Import(markdown, PageId);

        var table = blocks.Should().ContainSingle(block => block.Type == BlockType.Table).Subject;
        table.ParentBlockId.Should().BeNull();
        table.PageId.Should().Be(PageId);

        var content = table.Content.Should().BeAssignableTo<ITableBlockContent>().Subject;
        content.HasHeaderRow.Should().BeTrue();
        content.ColumnCount.Should().Be(2);

        var rows = Rows(blocks);
        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(row => row.ParentBlockId == table.Id);
        rows.Select(row => row.Order).Should().Equal(0, 1, 2);
        Cells(rows[0]).Should().Equal("Name", "Status");
        Cells(rows[2]).Should().Equal("CF27", "Draft");
    }

    [Fact]
    public void Import_TableProducesNoOrphanRows()
    {
        const string markdown = """
            | A | B |
            | --- | --- |
            | 1 | 2 |
            """;

        var blocks = NotionMarkdownImporter.Import(markdown, PageId);

        blocks.Where(block => block.Type == BlockType.TableRow)
            .Should().OnlyContain(row => row.ParentBlockId != null);
    }

    [Fact]
    public void Import_TableReadsColumnAlignments()
    {
        const string markdown = """
            | Plain | Left | Center | Right |
            | --- | :--- | :---: | ---: |
            | a | b | c | d |
            """;

        var blocks = NotionMarkdownImporter.Import(markdown, PageId);

        var content = (ITableBlockContent)blocks.Single(block => block.Type == BlockType.Table).Content;
        content.ColumnAlignments.Should().Equal(
            TableColumnAlignment.None,
            TableColumnAlignment.Left,
            TableColumnAlignment.Center,
            TableColumnAlignment.Right);
    }

    [Fact]
    public void Import_TableWithoutOuterPipesIsRecognized()
    {
        const string markdown = """
            Col A | Col B
            --- | ---
            One | Two
            """;

        var blocks = NotionMarkdownImporter.Import(markdown, PageId);

        blocks.Should().ContainSingle(block => block.Type == BlockType.Table);
        Rows(blocks).Should().HaveCount(2);
    }

    [Fact]
    public void Import_SingleColumnTableIsRecognized()
    {
        const string markdown = """
            | Single |
            | :---: |
            | only |
            """;

        var blocks = NotionMarkdownImporter.Import(markdown, PageId);

        var table = blocks.Should().ContainSingle(block => block.Type == BlockType.Table).Subject;
        ((ITableBlockContent)table.Content).ColumnCount.Should().Be(1);
        ((ITableBlockContent)table.Content).ColumnAlignments.Should().Equal(TableColumnAlignment.Center);
        Rows(blocks).Should().HaveCount(2);
    }

    [Fact]
    public void Import_ThematicBreakStaysDivider()
    {
        var blocks = NotionMarkdownImporter.Import("before\n\n---\n\nafter", PageId);

        blocks.Should().ContainSingle(block => block.Type == BlockType.Divider);
        blocks.Should().NotContain(block => block.Type == BlockType.Table);
    }

    [Fact]
    public void Import_TableFollowedByParagraphKeepsBlockOrder()
    {
        const string markdown = """
            | A | B |
            | --- | --- |
            | 1 | 2 |

            After the table.
            """;

        var blocks = NotionMarkdownImporter.Import(markdown, PageId);

        var table = blocks.Single(block => block.Type == BlockType.Table);
        var paragraph = blocks.Single(block => block.Type == BlockType.Paragraph);
        paragraph.ParentBlockId.Should().BeNull();
        paragraph.Order.Should().BeGreaterThan(table.Order);
    }

    [Fact]
    public void Import_TwoAdjacentTablesDoNotShareRows()
    {
        const string markdown = """
            | A | B |
            | --- | --- |
            | 1 | 2 |

            | C | D |
            | --- | --- |
            | 3 | 4 |
            """;

        var blocks = NotionMarkdownImporter.Import(markdown, PageId);

        var tables = blocks.Where(block => block.Type == BlockType.Table).ToList();
        tables.Should().HaveCount(2);
        foreach (var table in tables)
        {
            Rows(blocks).Count(row => row.ParentBlockId == table.Id).Should().Be(2);
        }
    }

    private static List<IPageBlock> Rows(IEnumerable<IPageBlock> blocks)
        => blocks.Where(block => block.Type == BlockType.TableRow).OrderBy(block => block.Order).ToList();

    private static IReadOnlyList<string> Cells(IPageBlock row)
        => ((ITableRowBlockContent)row.Content).Cells;
}
