using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using NmTableBlockContent = Tempo.Blazor.NotionEditor.Models.TableBlockContent;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Exportéry musí renderovat tabulku z Table rodiče + jeho child řádků (živý model editoru),
/// a zároveň nadále zvládnout starou flat strukturu po sobě jdoucích TableRow bloků.
/// </summary>
public class NotionExporterTableTests
{
    private static readonly Guid PageId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void MarkdownExporter_RendersTableFromParentAndChildRows()
    {
        var blocks = ParentTable(alignments: []);

        var markdown = NotionMarkdownExporter.Export(blocks);

        markdown.Should().Contain("| Name | Status |");
        markdown.Should().Contain("| --- | --- |");
        markdown.Should().Contain("| CF26 | Ready |");
    }

    [Fact]
    public void MarkdownExporter_RendersColumnAlignments()
    {
        var blocks = ParentTable([TableColumnAlignment.Left, TableColumnAlignment.Right]);

        var markdown = NotionMarkdownExporter.Export(blocks);

        markdown.Should().Contain("| :--- | ---: |");
    }

    [Fact]
    public void MarkdownExporter_StillRendersLegacyFlatRows()
    {
        var blocks = new List<IPageBlock>
        {
            Row(null, 0, "Name", "Status"),
            Row(null, 1, "CF26", "Ready")
        };

        var markdown = NotionMarkdownExporter.Export(blocks);

        markdown.Should().Contain("| Name | Status |");
        markdown.Should().Contain("| --- | --- |");
        markdown.Should().Contain("| CF26 | Ready |");
    }

    [Fact]
    public void MarkdownExporter_TableIsFollowedByLaterBlocks()
    {
        var blocks = ParentTable([]);
        blocks.Add(new PageBlock
        {
            Id = Guid.NewGuid(),
            PageId = PageId,
            Type = BlockType.Paragraph,
            Order = 10,
            Content = new TextBlockContent { Html = "After" }
        });

        var markdown = NotionMarkdownExporter.Export(blocks);

        markdown.Should().Contain("| CF26 | Ready |");
        markdown.Should().Contain("After");
        markdown.IndexOf("After", StringComparison.Ordinal)
            .Should().BeGreaterThan(markdown.IndexOf("| CF26", StringComparison.Ordinal));
    }

    [Fact]
    public void HtmlExporter_RendersTableFromParentAndChildRows()
    {
        var blocks = ParentTable([]);

        var html = NotionHtmlExporter.Export(blocks);

        html.Should().Contain("<table");
        html.Should().Contain("<th>Name</th>");
        html.Should().Contain("<td>CF26</td>");
        html.Should().Contain("</table>");
    }

    [Fact]
    public void HtmlExporter_RendersColumnAlignments()
    {
        var blocks = ParentTable([TableColumnAlignment.Left, TableColumnAlignment.Right]);

        var html = NotionHtmlExporter.Export(blocks);

        html.Should().Contain("<th style=\"text-align:left\">Name</th>");
        html.Should().Contain("<th style=\"text-align:right\">Status</th>");
        html.Should().Contain("<td style=\"text-align:right\">Ready</td>");
    }

    [Fact]
    public void HtmlExporter_OmitsStyleForUnalignedColumns()
    {
        var blocks = ParentTable([TableColumnAlignment.None, TableColumnAlignment.Center]);

        var html = NotionHtmlExporter.Export(blocks);

        html.Should().Contain("<th>Name</th>");
        html.Should().Contain("<th style=\"text-align:center\">Status</th>");
    }

    [Fact]
    public void HtmlExporter_StillRendersLegacyFlatRows()
    {
        var blocks = new List<IPageBlock>
        {
            Row(null, 0, "Name", "Status"),
            Row(null, 1, "CF26", "Ready")
        };

        var html = NotionHtmlExporter.Export(blocks);

        html.Should().Contain("<th>Name</th>");
        html.Should().Contain("<td>CF26</td>");
        html.Should().Contain("</table>");
    }

    [Fact]
    public void MarkdownExporter_RoundTripsThroughImporter()
    {
        const string source = """
            | Name | Status |
            | :--- | ---: |
            | CF26 | Ready |
            """;

        var blocks = NotionMarkdownImporter.Import(source, PageId);
        var markdown = NotionMarkdownExporter.Export(blocks);

        markdown.Should().Contain("| Name | Status |");
        markdown.Should().Contain("| :--- | ---: |");
        markdown.Should().Contain("| CF26 | Ready |");
    }

    private static List<IPageBlock> ParentTable(IReadOnlyList<TableColumnAlignment> alignments)
    {
        var tableId = Guid.NewGuid();
        return
        [
            new PageBlock
            {
                Id = tableId,
                PageId = PageId,
                ParentBlockId = null,
                Type = BlockType.Table,
                Order = 0,
                Content = new NmTableBlockContent
                {
                    ColumnCount = 2,
                    HasHeaderRow = true,
                    ColumnAlignments = alignments
                }
            },
            Row(tableId, 0, "Name", "Status"),
            Row(tableId, 1, "CF26", "Ready")
        ];
    }

    private static IPageBlock Row(Guid? parentId, int order, params string[] cells) => new PageBlock
    {
        Id = Guid.NewGuid(),
        PageId = PageId,
        ParentBlockId = parentId,
        Type = BlockType.TableRow,
        Order = order,
        Content = new TableRowBlockContent { Cells = cells }
    };
}
