using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.Redline;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Redline DOCX contract: exporting a compare result must produce a Word document whose changes are
/// real w:ins / w:del tracked changes (with author and date), so Word and the DOCX importer both
/// see them as reviewable revisions.
/// </summary>
public class DocumentRedlineDocxExporterTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    [Fact]
    public async Task Export_ChangedText_WritesInsAndDelTrackedChanges()
    {
        var result = new DocumentCompareResult
        {
            Success = true,
            BaseDocument = Document("v1", "Cena je 100 Kč"),
            CompareDocument = Document("v2", "Cena je 200 Kč"),
            Changes =
            [
                new DocumentCompareBlockChange
                {
                    Kind = DocumentCompareChangeKind.Changed,
                    BlockId = "b1",
                    OldText = "Cena je 100 Kč",
                    NewText = "Cena je 200 Kč",
                    TextDiff = new DocumentTextDiffResult
                    {
                        Segments =
                        [
                            new DocumentTextDiffSegment { Kind = DocumentTextDiffSegmentKind.Unchanged, Text = "Cena je " },
                            new DocumentTextDiffSegment { Kind = DocumentTextDiffSegmentKind.Removed, Text = "100" },
                            new DocumentTextDiffSegment { Kind = DocumentTextDiffSegmentKind.Added, Text = "200" },
                            new DocumentTextDiffSegment { Kind = DocumentTextDiffSegmentKind.Unchanged, Text = " Kč" },
                        ],
                    },
                },
            ],
        };

        var export = await new DocumentRedlineDocxExporter().ExportAsync(result, new DocumentRedlineOptions
        {
            Author = new DocumentEditorAuthor { Id = "compare", DisplayName = "Porovnání" },
            Timestamp = new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero),
        });

        export.Content.Length.Should().BeGreaterThan(500);
        var body = ReadDocumentXml(export.Content);

        var inserted = body.Descendants(W + "ins").ToList();
        var deleted = body.Descendants(W + "del").ToList();
        inserted.Should().NotBeEmpty("added diff text must become w:ins");
        deleted.Should().NotBeEmpty("removed diff text must become w:del");
        inserted.SelectMany(ins => ins.Descendants(W + "t")).Select(t => t.Value).Should().Contain("200");
        // Deleted runs use w:delText inside w:del.
        deleted.SelectMany(del => del.Descendants()).Where(node => node.Name.LocalName is "delText" or "t")
            .Select(node => node.Value).Should().Contain("100");
        inserted[0].Attribute(W + "author")!.Value.Should().Be("Porovnání");
    }

    private static DocumentEditorDocument Document(string suffix, string text)
    {
        var document = DocumentEditorDocument.Empty();
        document.DocumentId = $"redline-docx-{suffix}";
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "b1",
                Type = DocumentBlockType.Paragraph,
                Order = 1,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] },
            },
        ];
        return document;
    }

    private static XElement ReadDocumentXml(byte[] docxBytes)
    {
        using var archive = new ZipArchive(new MemoryStream(docxBytes), ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml")!;
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return XElement.Parse(reader.ReadToEnd());
    }
}
