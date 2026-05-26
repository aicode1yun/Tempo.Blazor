using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase41Tests
{
    private const string TinyJpegDataUrl = "data:image/jpeg;base64,/9j/2Q==";

    [Fact]
    public void Phase41_Source_DocxDrawingRunWriterDoesNotUseImageBlockShim()
    {
        var source = ReadSource("src/Tempo.Blazor.DocumentFormats/Docx/DocumentDocxExporter.cs");

        source.Should().NotContain("ToImageBlockContent");
        source.Should().NotContain("CreateDrawingRun(ImageBlockContent");
        MethodBody(source, "WriteDrawingRunAsync").Should().NotContain("ImageBlockContent");
    }

    [Fact]
    public void Phase41_Source_DocxDrawingImporterReturnsDocumentDrawingRunDirectly()
    {
        var source = ReadSource("src/Tempo.Blazor.DocumentFormats/Docx/DocumentDocxImporter.cs");

        source.Should().NotContain("ImportedDocxImage");
        source.Should().NotContain("ToDrawingRun(ImageBlockContent");
        MethodBody(source, "ReadDrawingRunAsync").Should().NotContain("ImageBlockContent");
    }

    [Fact]
    public async Task Phase41_ImportAsync_DrawingParagraphDoesNotBecomeTopLevelImageBlock()
    {
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(DocxDrawingFixtureBuilder.CreateInlinePng()));

        imported.Document.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        var paragraph = imported.Document.Blocks
            .Select(block => block.Content)
            .OfType<ParagraphBlockContent>()
            .Single();
        paragraph.Inlines.Should().ContainSingle(inline => inline is DocumentDrawingRun);
    }

    [Fact]
    public async Task Phase41_ExportAsync_HeaderDrawingRunIsNativeDrawingNotImagePlaceholderText()
    {
        var document = DocumentEditorDocument.Empty("phase41-header-drawing");
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "phase41-header",
            Type = DocumentHeaderFooterType.Header,
            Blocks = [Paragraph(new TextRun { Text = "Header " }, Drawing("Header native drawing"))]
        });
        document.Blocks.Add(Paragraph(new TextRun { Text = "Body" }));

        var exported = await new DocumentDocxExporter().ExportAsync(document);

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var headerXml = package.ReadXml(package.HeaderPartPaths.Single());
        headerXml.Descendants(DocxDrawingTestPackage.W + "drawing").Should().ContainSingle();
        headerXml.Root!.Value.Should().NotContain("[Image]");
        package.AssertHasInlinePicture(headerXml, "Header native drawing");
    }

    [Fact]
    public async Task Phase41_ImportAsync_TableCellDrawingIsReadAsInlineDrawingRun()
    {
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(DocxDrawingFixtureBuilder.CreateHeaderFooterAndTableCell()));
        var table = imported.Document.Blocks
            .Select(block => block.Content)
            .OfType<TableBlockContent>()
            .Single();
        var cell = table.Rows.Single().Cells.Single();

        cell.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        cell.Blocks.SelectMany(GetInlines)
            .OfType<DocumentDrawingRun>()
            .Should()
            .ContainSingle(drawing => drawing.AltText == "Table cell picture");
    }

    [Fact]
    public async Task Phase41_ExportAsync_DrawingRunJpegDataUrlWritesJpegPart()
    {
        var document = DocumentEditorDocument.Empty("phase41-jpeg-drawing");
        document.Blocks.Add(Paragraph(
            new TextRun { Text = "JPEG " },
            Drawing("JPEG drawing", TinyJpegDataUrl)));

        var exported = await new DocumentDocxExporter().ExportAsync(document);

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        word.MainDocumentPart!.ImageParts.Should().ContainSingle();
        word.MainDocumentPart.ImageParts.Single().ContentType.Should().Be("image/jpeg");
        exported.Warnings.Should().BeEmpty();
    }

    private static DocumentBlock Paragraph(params InlineContent[] inlines)
        => new()
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = inlines.ToList() }
        };

    private static DocumentDrawingRun Drawing(string altText, string? url = null)
        => new()
        {
            Source = DocumentImageSource.Url,
            Url = url ?? DocumentFormatTestData.TransparentPngDataUrl,
            AltText = altText,
            Size = new DocumentImageSize { Width = 24, Height = 24 },
            Layout = new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Inline,
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Inline },
                Transform = new DocumentObjectTransform { Width = 24, Height = 24 }
            }
        };

    private static IEnumerable<InlineContent> GetInlines(DocumentBlock block)
        => block.Content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => []
        };

    private static string MethodBody(string source, string methodName)
    {
        var marker = source.IndexOf(methodName, StringComparison.Ordinal);
        marker.Should().BeGreaterThanOrEqualTo(0, $"method {methodName} must exist");
        var bodyStart = source.IndexOf('{', marker);
        bodyStart.Should().BeGreaterThanOrEqualTo(0, $"method {methodName} must have a body");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[bodyStart..(index + 1)];
                }
            }
        }

        return source[bodyStart..];
    }

    private static string ReadSource(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Tempo.Blazor.DocumentFormats"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "Tempo.Blazor.DocumentFormats.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Tempo.Blazor repository root.");
    }
}
