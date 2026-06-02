using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using Xunit;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// R.5.21 export/import gate — a document edited through the core engine (its model goes through
/// <see cref="CoreEngineModelConverter"/> on save) must export to DOCX and re-import without losing
/// its tables, images, or text. Combines the converter round-trip (R.5.1) with the real DOCX
/// exporter/importer, the exact path <c>GetCurrentDocumentForProviderExportAsync</c> drives.
/// </summary>
public class CoreEngineExportRoundTripTests
{
    // Simulates "save/export through the core engine": document → JS model → back to document.
    private static DocumentEditorDocument ThroughCoreEngine(DocumentEditorDocument doc)
    {
        var core = CoreEngineModelConverter.ToCoreModel(doc);
        var json = JsonSerializer.Serialize(core);
        using var parsed = JsonDocument.Parse(json);
        return CoreEngineModelConverter.FromCoreModel(parsed.RootElement, new DocumentEditorDocument { DocumentId = doc.DocumentId });
    }

    private static DocumentBlock Para(string id, string text) => new()
    {
        Id = id,
        Type = DocumentBlockType.Paragraph,
        Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] },
    };

    private static string AllText(DocumentEditorDocument doc)
    {
        string FromBlocks(System.Collections.Generic.IEnumerable<DocumentBlock> blocks) => string.Concat(blocks.Select(b => b.Content switch
        {
            ParagraphBlockContent p => string.Concat(p.Inlines.OfType<TextRun>().Select(r => r.Text)) + " ",
            HeadingBlockContent h => string.Concat(h.Inlines.OfType<TextRun>().Select(r => r.Text)) + " ",
            TableBlockContent t => string.Concat(t.Rows.SelectMany(row => row.Cells).Select(c => FromBlocks(c.Blocks))),
            _ => string.Empty,
        }));
        return FromBlocks(doc.Blocks);
    }

    private static bool HasImage(DocumentEditorDocument doc) => doc.Blocks.Any(b =>
        b.Type == DocumentBlockType.Image
        || (b.Content is ParagraphBlockContent p && p.Inlines.OfType<DocumentDrawingRun>().Any()));

    [Fact]
    public async Task CoreEngineRoundTrip_ThenDocxExportImport_PreservesTableImageAndText()
    {
        var table = new TableBlockContent();
        var row0 = new TableRowContent();
        row0.Cells.Add(new TableCellContent { Id = "c00", Blocks = [Para("c00p", "Cell A1")] });
        row0.Cells.Add(new TableCellContent { Id = "c01", Blocks = [Para("c01p", "Cell A2")] });
        table.Rows.Add(row0);
        var row1 = new TableRowContent();
        row1.Cells.Add(new TableCellContent { Id = "c10", Blocks = [Para("c10p", "Cell B1")] });
        row1.Cells.Add(new TableCellContent { Id = "c11", Blocks = [Para("c11p", "Cell B2")] });
        table.Rows.Add(row1);

        // A real (1×1 PNG) data URL so the DOCX exporter can embed the image bytes.
        const string pngDataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
        var image = new ImageBlockContent
        {
            Source = DocumentImageSource.Url,
            Url = pngDataUrl,
            AltText = "A picture",
            Layout = new DocumentObjectLayout { Transform = { Width = 200, Height = 150 } },
        };

        var doc = new DocumentEditorDocument { DocumentId = "export-rt" };
        doc.Blocks =
        [
            new DocumentBlock { Id = "h", Type = DocumentBlockType.Heading, Content = new HeadingBlockContent { Level = 1, Inlines = [new TextRun { Text = "Report Title" }] } },
            Para("p1", "Intro body text here."),
            new DocumentBlock { Id = "t", Type = DocumentBlockType.Table, Content = table },
            new DocumentBlock { Id = "img", Type = DocumentBlockType.Image, Content = image },
            Para("p2", "Closing paragraph."),
        ];

        // (1) Round-trip through the core engine (save path).
        var coreDoc = ThroughCoreEngine(doc);
        Assert.Contains(coreDoc.Blocks, b => b.Type == DocumentBlockType.Table);   // converter kept the table
        Assert.True(HasImage(coreDoc), "converter kept the image");

        // (2) Export the core-engine document to DOCX, then re-import it.
        var exported = await new DocumentDocxExporter().ExportAsync(coreDoc);
        Assert.NotNull(exported.Content);
        Assert.True(exported.Content.Length > 0, "DOCX bytes were produced");

        using var ms = new MemoryStream(exported.Content);
        var imported = (await new DocumentDocxImporter().ImportAsync(ms)).Document;

        // (3) No data loss: table, image, and text all survive the full core→DOCX→import path.
        Assert.Contains(imported.Blocks, b => b.Type == DocumentBlockType.Table);
        Assert.True(HasImage(imported), "the image survives the DOCX round-trip");
        var text = AllText(imported);
        Assert.Contains("Report Title", text);
        Assert.Contains("Intro body text", text);
        Assert.Contains("Cell B2", text);
        Assert.Contains("Closing paragraph", text);
    }
}
