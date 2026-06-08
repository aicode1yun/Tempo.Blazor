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
}
