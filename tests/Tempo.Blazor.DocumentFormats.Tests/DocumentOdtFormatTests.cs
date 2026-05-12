using System.IO.Compression;
using System.Xml.Linq;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Odt;

namespace Tempo.Blazor.DocumentFormats.Tests;

public class DocumentOdtFormatTests
{
    [Fact]
    public async Task ExportAsync_CreatesOpenableOdtZipPackage()
    {
        var document = DocumentFormatTestData.CreateDocument();

        var result = await new DocumentOdtExporter().ExportAsync(document);

        result.ContentType.Should().Be("application/vnd.oasis.opendocument.text");
        result.FileName.Should().EndWith(".odt");
        using var archive = new ZipArchive(new MemoryStream(result.Content), ZipArchiveMode.Read);
        archive.GetEntry("mimetype").Should().NotBeNull();
        archive.GetEntry("content.xml").Should().NotBeNull();
        archive.GetEntry("styles.xml").Should().NotBeNull();
        archive.GetEntry("META-INF/manifest.xml").Should().NotBeNull();

        using var contentStream = archive.GetEntry("content.xml")!.Open();
        var xml = await XDocument.LoadAsync(contentStream, LoadOptions.None, CancellationToken.None);
        xml.ToString().Should().Contain("Agreement");
        xml.ToString().Should().Contain("Numbered item");
        archive.Entries.Should().Contain(entry => entry.FullName.StartsWith("Pictures/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_ReadsOdtParagraphsHeadingsListsTablesMergedCellsAndImages()
    {
        var exported = await new DocumentOdtExporter().ExportAsync(DocumentFormatTestData.CreateDocument());

        var result = await new DocumentOdtImporter().ImportAsync(new MemoryStream(exported.Content), new DocumentFormatImportOptions
        {
            DocumentId = "imported-odt",
            FileName = "sample.odt"
        });

        result.Format.Should().Be(DocumentFormatKind.Odt);
        result.Document.DocumentId.Should().Be("imported-odt");
        result.Document.Blocks.Any(block => block.Content is HeadingBlockContent).Should().BeTrue();
        result.Document.Blocks.Any(block => block.Content is ListBlockContent { Ordered: true }).Should().BeTrue();
        result.Document.Blocks.Any(block => block.Content is TableBlockContent).Should().BeTrue();
        result.Document.Blocks.Any(block => block.Content is ImageBlockContent).Should().BeTrue();
        var table = result.Document.Blocks.Select(block => block.Content).OfType<TableBlockContent>().Single();
        table.Rows[0].Cells[0].ColumnSpan.Should().Be(2);
    }

    [Fact]
    public async Task RoundTrip_OdtModelOdt_PreservesVisibleText()
    {
        var source = DocumentFormatTestData.CreateDocument();
        var exported = await new DocumentOdtExporter().ExportAsync(source);
        var imported = await new DocumentOdtImporter().ImportAsync(new MemoryStream(exported.Content));

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
