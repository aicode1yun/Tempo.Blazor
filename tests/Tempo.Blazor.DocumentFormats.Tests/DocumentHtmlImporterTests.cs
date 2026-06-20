using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Html;

namespace Tempo.Blazor.DocumentFormats.Tests;

public sealed class DocumentHtmlImporterTests
{
    [Fact]
    public void Import_ReadsParagraphHeadingListAndTable()
    {
        const string html = """
            <main>
              <h1>Imported title</h1>
              <p>Hello <strong>world</strong> <span data-token-key="client.name">Client</span></p>
              <ol><li>First</li></ol>
              <table><tr><td colspan="2">Merged</td></tr></table>
            </main>
            """;

        var document = new DocumentHtmlImporter().Import(html, new DocumentHtmlImportOptions { DocumentId = "html-test" });

        document.DocumentId.Should().Be("html-test");
        document.Blocks.Should().HaveCount(4);
        document.Blocks[0].Content.Should().BeOfType<HeadingBlockContent>();
        var paragraph = document.Blocks[1].Content.Should().BeOfType<ParagraphBlockContent>().Subject;
        paragraph.Inlines.OfType<TextRun>().Should().Contain(run => run.Text == "world" && run.Marks.Any(mark => mark.Type == InlineMarkType.Bold));
        paragraph.Inlines.OfType<TokenRun>().Should().Contain(token => token.Key == "client.name");
        document.Blocks[2].Content.Should().BeOfType<ListBlockContent>().Which.Ordered.Should().BeTrue();
        document.Blocks[3].Content.Should().BeOfType<TableBlockContent>().Which.Rows[0].Cells[0].ColumnSpan.Should().Be(2);
    }

    [Fact]
    public void Import_DropsDangerousElementsAndUnsafeLinks()
    {
        const string html = """
            <p>Safe <script>alert(1)</script><a href="javascript:alert(1)">link</a></p>
            """;

        var document = new DocumentHtmlImporter().Import(html);
        var paragraph = document.Blocks.Single().Content.Should().BeOfType<ParagraphBlockContent>().Subject;

        paragraph.Inlines.OfType<TextRun>().Select(run => run.Text).Should().Contain(["Safe", "link"]);
        paragraph.Inlines.SelectMany(inline => inline.Marks).Should().NotContain(mark => mark.Type == InlineMarkType.Link);
    }

    [Fact]
    public void Import_WordHtml_MapsHeadingBoldItalicAndTable()
    {
        const string html = """
            <body>
              <p class="MsoHeading1">Word heading</p>
              <p><span style="font-weight:700">Bold</span> <span style="font-style:italic">Italic</span></p>
              <table><tr><td>Cell A</td><td>Cell B</td></tr></table>
            </body>
            """;

        var document = new DocumentHtmlImporter().Import(html);

        document.Blocks[0].Content.Should().BeOfType<HeadingBlockContent>().Which.Level.Should().Be(1);
        var paragraph = document.Blocks[1].Content.Should().BeOfType<ParagraphBlockContent>().Subject;
        paragraph.Inlines.OfType<TextRun>().Should().Contain(run => run.Text == "Bold" && run.Marks.Any(mark => mark.Type == InlineMarkType.Bold));
        paragraph.Inlines.OfType<TextRun>().Should().Contain(run => run.Text == "Italic" && run.Marks.Any(mark => mark.Type == InlineMarkType.Italic));
        document.Blocks[2].Content.Should().BeOfType<TableBlockContent>().Which.Rows[0].Cells.Should().HaveCount(2);
    }

    [Fact]
    public void Import_ExcelHtml_MapsMergedTableCells()
    {
        const string html = """
            <table>
              <tr><td colspan="2" rowspan="2">Merged</td><td>Right</td></tr>
              <tr><td>Bottom right</td></tr>
            </table>
            """;

        var document = new DocumentHtmlImporter().Import(html);

        var table = document.Blocks.Single().Content.Should().BeOfType<TableBlockContent>().Subject;
        table.Rows[0].Cells[0].ColumnSpan.Should().Be(2);
        table.Rows[0].Cells[0].RowSpan.Should().Be(2);
    }
}
