using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Markdown;

namespace Tempo.Blazor.DocumentFormats.Tests;

public sealed class DocumentMarkdownImporterTests
{
    [Fact]
    public void Import_ReadsHeadingsParagraphsListsQuotesTablesImagesAndInlineMarks()
    {
        const string markdown = """
            # Imported title

            Hello **bold** *italic* ~~removed~~ [link](https://example.test)

            - First item
            3. Third item
            > Quoted text

            | Name | Value |
            | --- | --- |
            | Phase | 19 |

            ![Chart](https://example.test/chart.png)
            """;

        var document = new DocumentMarkdownImporter().Import(markdown, new DocumentMarkdownImportOptions
        {
            DocumentId = "markdown-import"
        });

        document.DocumentId.Should().Be("markdown-import");
        document.Blocks[0].Content.Should().BeOfType<HeadingBlockContent>().Which.Level.Should().Be(1);
        var paragraph = document.Blocks[1].Content.Should().BeOfType<ParagraphBlockContent>().Subject;
        paragraph.Inlines.OfType<TextRun>().Should().Contain(run => run.Text == "bold" && run.Marks.Any(mark => mark.Type == InlineMarkType.Bold));
        paragraph.Inlines.OfType<TextRun>().Should().Contain(run => run.Text == "italic" && run.Marks.Any(mark => mark.Type == InlineMarkType.Italic));
        paragraph.Inlines.OfType<TextRun>().Should().Contain(run => run.Text == "removed" && run.Marks.Any(mark => mark.Type == InlineMarkType.Strikethrough));
        paragraph.Inlines.OfType<TextRun>().Should().Contain(run => run.Text == "link" && run.Marks.Any(mark => mark.Type == InlineMarkType.Link));
        document.Blocks.Any(block => block.Content is ListBlockContent { Ordered: false }).Should().BeTrue();
        document.Blocks.Any(block => block.Content is ListBlockContent { Ordered: true, StartNumber: 3 }).Should().BeTrue();
        document.Blocks.Any(block => block.Content is QuoteBlockContent).Should().BeTrue();
        document.Blocks.Any(block => block.Content is TableBlockContent table && table.Rows.Count == 2).Should().BeTrue();
        document.Blocks.Any(block => block.Content is ImageBlockContent image && image.Url == "https://example.test/chart.png").Should().BeTrue();
    }

    [Fact]
    public void Import_ReadsGfmTableWithoutOuterPipes()
    {
        const string markdown = """
            Col A | Col B
            --- | ---
            One | Two
            Three | Four
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var table = document.Blocks.Should().ContainSingle().Which.Content
            .Should().BeOfType<TableBlockContent>().Subject;
        table.Rows.Should().HaveCount(3);
        table.Rows.Should().OnlyContain(row => row.Cells.Count == 2);
        CellText(table, 0, 0).Should().Be("Col A");
        CellText(table, 0, 1).Should().Be("Col B");
        table.Rows[0].Cells.Should().OnlyContain(cell => cell.IsHeader);
        CellText(table, 2, 1).Should().Be("Four");
    }

    [Fact]
    public void Import_ReadsColumnAlignmentsFromSeparatorRow()
    {
        const string markdown = """
            | Plain | Left | Center | Right |
            | --- | :--- | :---: | ---: |
            | a | b | c | d |
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var table = document.Blocks.Should().ContainSingle().Which.Content
            .Should().BeOfType<TableBlockContent>().Subject;
        table.ColumnAlignments.Should().Equal(
            TableColumnAlignment.None,
            TableColumnAlignment.Left,
            TableColumnAlignment.Center,
            TableColumnAlignment.Right);
    }

    [Fact]
    public void Import_ReadsColumnAlignmentsFromSeparatorRowWithoutOuterPipes()
    {
        const string markdown = """
            Left | Center | Right
            :--- | :---: | ---:
            a | b | c
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var table = document.Blocks.Should().ContainSingle().Which.Content
            .Should().BeOfType<TableBlockContent>().Subject;
        table.ColumnAlignments.Should().Equal(
            TableColumnAlignment.Left,
            TableColumnAlignment.Center,
            TableColumnAlignment.Right);
    }

    [Fact]
    public void Import_KeepsEscapedPipeAsCellContent()
    {
        const string markdown = """
            | Expression | Meaning |
            | --- | --- |
            | a \| b | union |
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var table = document.Blocks.Should().ContainSingle().Which.Content
            .Should().BeOfType<TableBlockContent>().Subject;
        table.Rows[1].Cells.Should().HaveCount(2);
        CellText(table, 1, 0).Should().Be("a | b");
        CellText(table, 1, 1).Should().Be("union");
    }

    [Fact]
    public void Import_KeepsEscapedPipeAsCellContentWithoutOuterPipes()
    {
        const string markdown = """
            Expression | Meaning
            --- | ---
            a \| b | union
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var table = document.Blocks.Should().ContainSingle().Which.Content
            .Should().BeOfType<TableBlockContent>().Subject;
        table.Rows[1].Cells.Should().HaveCount(2);
        CellText(table, 1, 0).Should().Be("a | b");
        CellText(table, 1, 1).Should().Be("union");
    }

    [Fact]
    public void Import_ReadsCompactSeparatorRow()
    {
        const string markdown = """
            | Left | Center | Right |
            |:-|:-:|-:|
            | a | b | c |
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var table = document.Blocks.Should().ContainSingle().Which.Content
            .Should().BeOfType<TableBlockContent>().Subject;
        table.ColumnAlignments.Should().Equal(
            TableColumnAlignment.Left,
            TableColumnAlignment.Center,
            TableColumnAlignment.Right);
        table.Rows.Should().HaveCount(2);
    }

    [Fact]
    public void Import_PadsShortRowsAndTruncatesOverlongRowsToHeaderWidth()
    {
        const string markdown = """
            | A | B | C |
            | --- | --- | --- |
            | only one |
            | 1 | 2 | 3 | 4 |
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var table = document.Blocks.Should().ContainSingle().Which.Content
            .Should().BeOfType<TableBlockContent>().Subject;
        table.Rows.Should().OnlyContain(row => row.Cells.Count == 3);
        CellText(table, 1, 0).Should().Be("only one");
        CellText(table, 1, 2).Should().BeEmpty();
        CellText(table, 2, 2).Should().Be("3");
    }

    [Fact]
    public void Import_KeepsInlineFormattingInsideCells()
    {
        const string markdown = """
            | Name | Note |
            | --- | --- |
            | **bold** | [link](https://example.test) |
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var table = document.Blocks.Should().ContainSingle().Which.Content
            .Should().BeOfType<TableBlockContent>().Subject;
        var boldCell = (ParagraphBlockContent)table.Rows[1].Cells[0].Blocks[0].Content;
        boldCell.Inlines.OfType<TextRun>().Should()
            .Contain(run => run.Text == "bold" && run.Marks.Any(mark => mark.Type == InlineMarkType.Bold));
        var linkCell = (ParagraphBlockContent)table.Rows[1].Cells[1].Blocks[0].Content;
        linkCell.Inlines.OfType<TextRun>().Should()
            .Contain(run => run.Text == "link" && run.Marks.Any(mark => mark.Type == InlineMarkType.Link));
    }

    [Fact]
    public void Import_ReadsSingleColumnTable()
    {
        const string markdown = """
            | Single |
            | :---: |
            | only |
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var table = document.Blocks.Should().ContainSingle().Which.Content
            .Should().BeOfType<TableBlockContent>().Subject;
        table.ColumnAlignments.Should().Equal(TableColumnAlignment.Center);
        table.Rows.Should().HaveCount(2);
        CellText(table, 0, 0).Should().Be("Single");
        CellText(table, 1, 0).Should().Be("only");
    }

    [Fact]
    public void Import_ThematicBreakIsNotTreatedAsSingleColumnSeparator()
    {
        const string markdown = """
            Intro paragraph

            ---

            After the rule
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        document.Blocks.Should().NotContain(block => block.Content is TableBlockContent);
    }

    [Fact]
    public void Import_LineWithPipeButNoSeparatorStaysParagraph()
    {
        const string markdown = """
            Cost is 10 | 20 depending on plan.
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        document.Blocks.Should().ContainSingle().Which.Content.Should().BeOfType<ParagraphBlockContent>();
    }

    private static string CellText(TableBlockContent table, int row, int column)
        => string.Concat(table.Rows[row].Cells[column].Blocks
            .SelectMany(block => ((ParagraphBlockContent)block.Content).Inlines)
            .OfType<TextRun>()
            .Select(run => run.Text));
}
