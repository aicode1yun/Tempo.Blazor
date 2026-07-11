using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Markdown;

namespace Tempo.Blazor.DocumentFormats.Tests;

public sealed class DocumentMarkdownExporterTests
{
    [Fact]
    public void Export_RendersParagraphsHeadingsListsAndQuotes()
    {
        var document = DocumentEditorDocument.Empty("markdown-test");
        document.Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Heading,
                Content = new HeadingBlockContent { Level = 2, Inlines = [new TextRun { Text = "Title" }] }
            },
            new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Order = 1,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Text = "Bold", Marks = [new InlineMark { Type = InlineMarkType.Bold }] },
                        new TextRun { Text = " text" }
                    ]
                }
            },
            new DocumentBlock
            {
                Type = DocumentBlockType.List,
                Order = 2,
                Content = new ListBlockContent { Inlines = [new TextRun { Text = "Item" }] }
            },
            new DocumentBlock
            {
                Type = DocumentBlockType.Quote,
                Order = 3,
                Content = new QuoteBlockContent { Inlines = [new TextRun { Text = "Quoted" }] }
            }
        ];

        var markdown = new DocumentMarkdownExporter().Export(document);

        markdown.Should().Contain("## Title");
        markdown.Should().Contain("**Bold** text");
        markdown.Should().Contain("- Item");
        markdown.Should().Contain("> Quoted");
    }

    [Fact]
    public void ImportThenExport_PreservesSemanticMarkdownBlocks()
    {
        const string source = """
            ## Phase 19

            Canvas **export** bridge

            | Kind | Status |
            | --- | --- |
            | Markdown | Ready |
            """;

        var document = new DocumentMarkdownImporter().Import(source);
        var markdown = new DocumentMarkdownExporter().Export(document);

        markdown.Should().Contain("## Phase 19");
        markdown.Should().Contain("Canvas **export** bridge");
        markdown.Should().Contain("| Kind | Status |");
        markdown.Should().Contain("| Markdown | Ready |");
    }

    [Fact]
    public void Export_RendersColumnAlignmentsInSeparatorRow()
    {
        var document = DocumentEditorDocument.Empty("markdown-alignment");
        document.Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Table,
                Content = new TableBlockContent
                {
                    ColumnAlignments =
                    [
                        TableColumnAlignment.None,
                        TableColumnAlignment.Left,
                        TableColumnAlignment.Center,
                        TableColumnAlignment.Right
                    ],
                    Rows =
                    [
                        new TableRowContent { Cells = [Cell("Plain", true), Cell("Left", true), Cell("Center", true), Cell("Right", true)] },
                        new TableRowContent { Cells = [Cell("a", false), Cell("b", false), Cell("c", false), Cell("d", false)] }
                    ]
                }
            }
        ];

        var markdown = new DocumentMarkdownExporter().Export(document);

        markdown.Should().Contain("| --- | :--- | :---: | ---: |");
    }

    [Fact]
    public void ImportThenExport_RoundTripsTableAlignmentsAndEmptyCells()
    {
        const string source = """
            | Plain | Left | Center | Right |
            | --- | :--- | :---: | ---: |
            | a |  | c | d |
            |  | b |  |  |
            """;

        var document = new DocumentMarkdownImporter().Import(source);
        var markdown = new DocumentMarkdownExporter().Export(document);
        var reimported = new DocumentMarkdownImporter().Import(markdown);

        var original = (TableBlockContent)document.Blocks.Single(block => block.Content is TableBlockContent).Content;
        var roundTripped = (TableBlockContent)reimported.Blocks.Single(block => block.Content is TableBlockContent).Content;

        TableColumnAlignment[] expectedAlignments =
        [
            TableColumnAlignment.None,
            TableColumnAlignment.Left,
            TableColumnAlignment.Center,
            TableColumnAlignment.Right
        ];
        original.ColumnAlignments.Should().Equal(expectedAlignments);
        roundTripped.ColumnAlignments.Should().Equal(expectedAlignments);
        roundTripped.Rows.Should().HaveCount(original.Rows.Count);
        for (var row = 0; row < original.Rows.Count; row++)
        {
            roundTripped.Rows[row].Cells.Should().HaveCount(original.Rows[row].Cells.Count);
            for (var column = 0; column < original.Rows[row].Cells.Count; column++)
            {
                CellText(roundTripped, row, column).Should().Be(CellText(original, row, column));
                roundTripped.Rows[row].Cells[column].IsHeader.Should().Be(original.Rows[row].Cells[column].IsHeader);
            }
        }
    }

    [Fact]
    public void Export_HeaderOnlyTableStillRendersSeparatorRow()
    {
        var markdown = ExportTable(new TableBlockContent
        {
            ColumnAlignments = [TableColumnAlignment.Left, TableColumnAlignment.Right],
            Rows = [new TableRowContent { Cells = [Cell("A", true), Cell("B", true)] }]
        });

        markdown.Should().Contain("| A | B |");
        markdown.Should().Contain("| :--- | ---: |");
    }

    [Fact]
    public void Export_SingleColumnTableRendersValidSeparator()
    {
        var markdown = ExportTable(new TableBlockContent
        {
            ColumnAlignments = [TableColumnAlignment.Center],
            Rows =
            [
                new TableRowContent { Cells = [Cell("Only", true)] },
                new TableRowContent { Cells = [Cell("value", false)] }
            ]
        });

        markdown.Should().Contain("| Only |");
        markdown.Should().Contain("| :---: |");
        markdown.Should().Contain("| value |");
    }

    [Fact]
    public void ImportThenExport_RoundTripsSingleColumnTable()
    {
        const string source = """
            | Single |
            | :---: |
            | only |
            """;

        var document = new DocumentMarkdownImporter().Import(source);
        var markdown = new DocumentMarkdownExporter().Export(document);
        var reimported = new DocumentMarkdownImporter().Import(markdown);

        var table = (TableBlockContent)reimported.Blocks.Single(block => block.Content is TableBlockContent).Content;
        table.ColumnAlignments.Should().Equal(TableColumnAlignment.Center);
        table.Rows.Should().HaveCount(2);
        CellText(table, 1, 0).Should().Be("only");
    }

    [Fact]
    public void Export_RaggedRowsArePaddedToWidestRow()
    {
        var markdown = ExportTable(new TableBlockContent
        {
            Rows =
            [
                new TableRowContent { Cells = [Cell("A", true), Cell("B", true)] },
                new TableRowContent { Cells = [Cell("one", false)] },
                new TableRowContent { Cells = [Cell("x", false), Cell("y", false), Cell("z", false)] }
            ]
        });

        markdown.Should().Contain("| --- | --- | --- |");

        var reimported = new DocumentMarkdownImporter().Import(markdown);
        var table = (TableBlockContent)reimported.Blocks.Single(block => block.Content is TableBlockContent).Content;
        table.Rows.Should().HaveCount(3);
        table.Rows.Should().OnlyContain(row => row.Cells.Count == 3);
        CellText(table, 0, 2).Should().BeEmpty();
        CellText(table, 1, 0).Should().Be("one");
        CellText(table, 1, 1).Should().BeEmpty();
        CellText(table, 2, 2).Should().Be("z");
    }

    [Fact]
    public void Export_EscapesPipeInsideCellText()
    {
        var markdown = ExportTable(new TableBlockContent
        {
            Rows =
            [
                new TableRowContent { Cells = [Cell("Expression", true)] },
                new TableRowContent { Cells = [Cell("a | b", false)] }
            ]
        });

        markdown.Should().Contain(@"| a \| b |");
    }

    [Fact]
    public void ImportThenExport_RoundTripsEscapedPipeInsideCell()
    {
        const string source = """
            | Expression | Meaning |
            | --- | --- |
            | a \| b | union |
            """;

        var document = new DocumentMarkdownImporter().Import(source);
        var markdown = new DocumentMarkdownExporter().Export(document);
        var reimported = new DocumentMarkdownImporter().Import(markdown);

        var table = (TableBlockContent)reimported.Blocks.Single(block => block.Content is TableBlockContent).Content;
        table.Rows[1].Cells.Should().HaveCount(2);
        CellText(table, 1, 0).Should().Be("a | b");
        CellText(table, 1, 1).Should().Be("union");
    }

    [Fact]
    public void Export_TableWithNoRowsEmitsNothing()
    {
        var markdown = ExportTable(new TableBlockContent());

        markdown.Should().BeEmpty();
    }

    private static string ExportTable(TableBlockContent table)
    {
        var document = DocumentEditorDocument.Empty("markdown-table");
        document.Blocks = [new DocumentBlock { Type = DocumentBlockType.Table, Content = table }];
        return new DocumentMarkdownExporter().Export(document);
    }

    private static TableCellContent Cell(string text, bool isHeader) => new()
    {
        IsHeader = isHeader,
        Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] }
            }
        ]
    };

    private static string CellText(TableBlockContent table, int row, int column)
        => string.Concat(table.Rows[row].Cells[column].Blocks
            .SelectMany(block => ((ParagraphBlockContent)block.Content).Inlines)
            .OfType<TextRun>()
            .Select(run => run.Text));
}
