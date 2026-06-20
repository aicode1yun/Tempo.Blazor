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
}
