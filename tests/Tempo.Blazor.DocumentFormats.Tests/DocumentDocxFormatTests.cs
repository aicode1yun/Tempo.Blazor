using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;

namespace Tempo.Blazor.DocumentFormats.Tests;

public class DocumentDocxFormatTests
{
    [Fact]
    public async Task ExportAsync_CreatesOpenableDocxPackage()
    {
        var document = DocumentFormatTestData.CreateDocument();

        var result = await new DocumentDocxExporter().ExportAsync(document);

        result.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        result.FileName.Should().EndWith(".docx");
        result.Content.Should().NotBeEmpty();

        using var stream = new MemoryStream(result.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        word.MainDocumentPart.Should().NotBeNull();
        word.MainDocumentPart!.Document.Body!.InnerText.Should().Contain("Agreement");
        word.MainDocumentPart.Document.Body!.InnerText.Should().Contain("Numbered item");
        word.MainDocumentPart.Document.Body!.Descendants<DocumentFormat.OpenXml.Wordprocessing.Table>().Should().NotBeEmpty();
        word.MainDocumentPart.ImageParts.Should().NotBeEmpty();
        word.MainDocumentPart.HeaderParts.Should().NotBeEmpty();
        word.MainDocumentPart.FootnotesPart.Should().NotBeNull();
        word.MainDocumentPart.WordprocessingCommentsPart.Should().NotBeNull();
    }

    [Fact]
    public async Task ImportAsync_ReadsDocxParagraphsHeadingsStylesLinksListsTablesImagesAndNotes()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(DocumentFormatTestData.CreateDocument());

        await using var stream = new MemoryStream(exported.Content);
        var result = await new DocumentDocxImporter().ImportAsync(stream, new DocumentFormatImportOptions
        {
            DocumentId = "imported-docx",
            FileName = "sample.docx"
        });

        result.Format.Should().Be(DocumentFormatKind.Docx);
        result.Document.DocumentId.Should().Be("imported-docx");
        result.Document.Blocks.OfType<DocumentBlock>().Any(b => b.Content is HeadingBlockContent).Should().BeTrue();
        result.Document.Blocks.OfType<DocumentBlock>().Any(b => b.Content is ListBlockContent { Ordered: true }).Should().BeTrue();
        result.Document.Blocks.OfType<DocumentBlock>().Any(b => b.Content is TableBlockContent).Should().BeTrue();
        result.Document.Blocks.OfType<DocumentBlock>().Any(b => b.Content is ImageBlockContent).Should().BeTrue();
        result.Document.Blocks.OfType<DocumentBlock>().Any(b => b.Content is PageBreakBlockContent).Should().BeTrue();
        result.Document.HeadersFooters.Should().NotBeEmpty();
        result.Document.Notes.Should().Contain(note => note.Type == DocumentNoteType.Footnote);
        result.Document.Comments.Should().NotBeEmpty();
        result.Document.Sections[0].Properties.PageSettings.Landscape.Should().BeTrue();

        var paragraph = result.Document.Blocks.Select(b => b.Content).OfType<ParagraphBlockContent>().First();
        paragraph.Inlines.OfType<TextRun>().Any(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Bold)).Should().BeTrue();
        paragraph.Inlines.OfType<TextRun>().Any(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Link && mark.Link is not null && mark.Link.Href == "https://example.test/")).Should().BeTrue();
    }

    [Fact]
    public async Task RoundTrip_DocxModelDocx_PreservesVisibleText()
    {
        var source = DocumentFormatTestData.CreateDocument();
        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        FlattenText(imported.Document).Should().Contain("Agreement");
        FlattenText(imported.Document).Should().Contain("Bold and link");
        FlattenText(imported.Document).Should().Contain("Merged");
    }

    private static string FlattenText(DocumentEditorDocument document)
    {
        return string.Join("\n", document.Blocks.Select(block => block.Content switch
        {
            ParagraphBlockContent paragraph => string.Concat(paragraph.Inlines.OfType<TextRun>().Select(run => run.Text)),
            HeadingBlockContent heading => string.Concat(heading.Inlines.OfType<TextRun>().Select(run => run.Text)),
            ListBlockContent list => string.Concat(list.Inlines.OfType<TextRun>().Select(run => run.Text)),
            TableBlockContent table => string.Join("\n", table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks).Select(block => block.Content is ParagraphBlockContent paragraph ? string.Concat(paragraph.Inlines.OfType<TextRun>().Select(run => run.Text)) : string.Empty)),
            _ => string.Empty
        }));
    }
}
