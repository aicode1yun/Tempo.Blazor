using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Html;
using Tempo.Blazor.DocumentFormats.Markdown;
using Tempo.Blazor.DocumentFormats.Odt;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Inline signing fields are an editor concept the document formats do not model. Export must not fail
/// and must degrade to a readable placeholder (plan S2.25/S2.26, O3) — in the body and the footer.
/// The canonical JSON remains the source of truth, so the placeholder is intentionally one-way.
/// </summary>
public sealed class SigningFieldExportDegradationTests
{
    private static DocumentEditorDocument DocumentWithSigningFields()
    {
        var document = DocumentEditorDocument.Empty("signing-export");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Signed: " },
                    new DocumentSigningFieldRun { Uuid = "f1", FieldType = "signature", SubmitterUuid = "signer", Label = "Signature", BoxWidth = 180, BoxHeight = 44 },
                ],
            },
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "footer-1",
            Type = DocumentHeaderFooterType.Footer,
            Blocks =
            [
                new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent { Inlines = [new DocumentSigningFieldRun { Uuid = "f2", FieldType = "initials", SubmitterUuid = "signer", Label = "Initials" }] },
                },
            ],
        });
        return document;
    }

    [Fact]
    public void HtmlExport_RendersSigningFieldPlaceholder()
    {
        var html = new DocumentHtmlExporter().Export(DocumentWithSigningFields());

        html.Should().Contain("Signature");
        html.Should().Contain("⟦Pole:");
    }

    [Fact]
    public void MarkdownExport_RendersSigningFieldPlaceholder()
    {
        var markdown = new DocumentMarkdownExporter().Export(DocumentWithSigningFields());

        markdown.Should().Contain("⟦Pole: Signature");
    }

    [Fact]
    public async Task OdtExport_DoesNotFailAndContainsPlaceholder()
    {
        var result = await new DocumentOdtExporter().ExportAsync(DocumentWithSigningFields());

        var content = ReadZipEntry(result.Content, "content.xml");
        content.Should().Contain("Signature");
        content.Should().Contain("⟦Pole:");
    }

    [Fact]
    public async Task DocxExport_DoesNotFailAndContainsPlaceholder()
    {
        var result = await new DocumentDocxExporter().ExportAsync(DocumentWithSigningFields());

        var document = ReadZipEntry(result.Content, "word/document.xml");
        document.Should().Contain("Signature");
        document.Should().Contain("⟦Pole:");
    }

    private static string ReadZipEntry(byte[] content, string entryName)
    {
        using var stream = new MemoryStream(content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException($"Entry '{entryName}' not found.");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
