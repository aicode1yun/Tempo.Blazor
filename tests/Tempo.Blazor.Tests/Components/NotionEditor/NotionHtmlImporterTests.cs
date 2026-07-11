using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// HTML &lt;table&gt; musí vzniknout jako jeden Table blok s child TableRow bloky,
/// stejně jako u markdown importu.
/// </summary>
public class NotionHtmlImporterTests
{
    private static readonly Guid PageId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Import_TableProducesSingleTableParentWithChildRows()
    {
        const string html = """
            <table>
              <thead><tr><th>Name</th><th>Status</th></tr></thead>
              <tbody>
                <tr><td>CF26</td><td>Ready</td></tr>
                <tr><td>CF27</td><td>Draft</td></tr>
              </tbody>
            </table>
            """;

        var blocks = NotionHtmlImporter.Import(html, PageId);

        var table = blocks.Should().ContainSingle(block => block.Type == BlockType.Table).Subject;
        table.ParentBlockId.Should().BeNull();
        table.PageId.Should().Be(PageId);

        var content = table.Content.Should().BeAssignableTo<ITableBlockContent>().Subject;
        content.ColumnCount.Should().Be(2);
        content.HasHeaderRow.Should().BeTrue();

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
        const string html = "<table><tr><td>1</td><td>2</td></tr></table>";

        var blocks = NotionHtmlImporter.Import(html, PageId);

        blocks.Where(block => block.Type == BlockType.TableRow)
            .Should().OnlyContain(row => row.ParentBlockId != null);
    }

    [Fact]
    public void Import_TableWithoutHeaderCellsHasNoHeaderRow()
    {
        const string html = "<table><tr><td>1</td><td>2</td></tr><tr><td>3</td><td>4</td></tr></table>";

        var blocks = NotionHtmlImporter.Import(html, PageId);

        var content = (ITableBlockContent)blocks.Single(block => block.Type == BlockType.Table).Content;
        content.HasHeaderRow.Should().BeFalse();
        Rows(blocks).Should().HaveCount(2);
    }

    [Fact]
    public void Import_TwoAdjacentTablesDoNotShareRows()
    {
        const string html = """
            <table><tr><td>1</td><td>2</td></tr></table>
            <table><tr><td>3</td><td>4</td></tr></table>
            """;

        var blocks = NotionHtmlImporter.Import(html, PageId);

        var tables = blocks.Where(block => block.Type == BlockType.Table).ToList();
        tables.Should().HaveCount(2);
        foreach (var table in tables)
        {
            Rows(blocks).Count(row => row.ParentBlockId == table.Id).Should().Be(1);
        }
    }

    [Fact]
    public void Import_TableFollowedByParagraphKeepsBlockOrder()
    {
        const string html = "<table><tr><td>1</td></tr></table><p>After the table.</p>";

        var blocks = NotionHtmlImporter.Import(html, PageId);

        var table = blocks.Single(block => block.Type == BlockType.Table);
        var paragraph = blocks.Single(block => block.Type == BlockType.Paragraph);
        paragraph.ParentBlockId.Should().BeNull();
        paragraph.Order.Should().BeGreaterThan(table.Order);
    }

    private static List<IPageBlock> Rows(IEnumerable<IPageBlock> blocks)
        => blocks.Where(block => block.Type == BlockType.TableRow).OrderBy(block => block.Order).ToList();

    private static IReadOnlyList<string> Cells(IPageBlock row)
        => ((ITableRowBlockContent)row.Content).Cells;
}
