using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase29Tests
{
    private const string TempoNamespace = "urn:tempo-blazor:document-editor:1.0";

    [Fact]
    public async Task Phase29_ExportAsync_DrawingRunWritesNativeInlineDrawingWithoutImageBlockShim()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateInlineDrawingDocument());

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var paragraph = word.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().First();
        var drawingRun = paragraph.Elements<W.Run>().Single(run => run.Descendants<W.Drawing>().Any());
        var drawing = drawingRun.GetFirstChild<W.Drawing>()!;
        var inline = drawing.GetFirstChild<DW.Inline>();

        inline.Should().NotBeNull();
        drawing.Descendants<DW.Anchor>().Should().BeEmpty();
        inline!.Extent!.Cx!.Value.Should().Be(DocxUnitConverter.PointToEmu(240));
        inline.Extent.Cy!.Value.Should().Be(DocxUnitConverter.PointToEmu(120));
        var nativeInlineLayoutAttributes = new[] { "layout-kind", "wrap-mode", "width", "height" };
        inline.GetAttributes().Should().NotContain(attribute =>
            attribute.NamespaceUri == TempoNamespace
            && nativeInlineLayoutAttributes.Contains(attribute.LocalName));

        var docProperties = inline.GetFirstChild<DW.DocProperties>()!;
        docProperties.Id!.Value.Should().Be(11U);
        docProperties.Name!.Value.Should().Be("Inline docPr");
        docProperties.Description!.Value.Should().Be("Inline alt text");

        var pictureProperties = drawing.Descendants<PIC.NonVisualDrawingProperties>().Single();
        pictureProperties.Id!.Value.Should().Be(22U);
        pictureProperties.Name!.Value.Should().Be("Inline picture name");
        pictureProperties.Description!.Value.Should().Be("Inline alt text");

        var blip = drawing.Descendants<A.Blip>().Single();
        blip.Embed!.Value.Should().NotBeNullOrWhiteSpace();
        word.MainDocumentPart.GetPartById(blip.Embed!.Value!).Should().BeOfType<ImagePart>();
    }

    [Fact]
    public async Task Phase29_ImportAsync_InlineDrawingImportsNativeDocxMetadata()
    {
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(DocxDrawingFixtureBuilder.CreateInlinePng()));

        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();
        var metadata = drawing.Docx;

        drawing.AltText.Should().Be("Inline PNG picture");
        drawing.Layout.Kind.Should().Be(DocumentObjectLayoutKind.Inline);
        drawing.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Inline);
        drawing.Layout.Transform.Width.Should().Be(120);
        drawing.Layout.Transform.Height.Should().Be(80);
        metadata.Should().NotBeNull();
        metadata!.DocPrId.Should().Be(1U);
        metadata.DocPrName.Should().Be("Picture 1");
        metadata.DocPrTitle.Should().Be("DrawingML fixture picture");
        metadata.DocPrDescription.Should().Be("Inline PNG picture");
        metadata.PictureNonVisualId.Should().Be(2U);
        metadata.PictureName.Should().Be("Picture 1");
        metadata.PictureDescription.Should().Be("Inline PNG picture");
        metadata.RelationshipId.Should().NotBeNullOrWhiteSpace();
        metadata.ImageReferenceMode.Should().Be(DocumentDocxImageReferenceMode.Embedded);
        metadata.Media.SourcePartUri.Should().Be("/word/document.xml");
        metadata.Media.ImagePartUri.Should().EndWith(".png");
        metadata.Media.ContentType.Should().Be("image/png");
        metadata.Media.Extension.Should().Be(".png");
    }

    private static DocumentEditorDocument CreateInlineDrawingDocument()
    {
        var layout = DocumentObjectLayout.Inline();
        layout.Transform.Width = 240;
        layout.Transform.Height = 120;
        layout.Transform.NaturalWidth = 320;
        layout.Transform.NaturalHeight = 180;

        var document = DocumentEditorDocument.Empty("phase29-inline-drawing");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-1",
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Before " },
                    new DocumentDrawingRun
                    {
                        Source = DocumentImageSource.Url,
                        Url = DocumentFormatTestData.TransparentPngDataUrl,
                        AltText = "Inline alt text",
                        Size = new DocumentImageSize { Width = 320, Height = 180 },
                        Layout = layout,
                        Docx = new DocumentDocxDrawingMetadata
                        {
                            DocPrId = 11U,
                            DocPrName = "Inline docPr",
                            PictureNonVisualId = 22U,
                            PictureName = "Inline picture name"
                        }
                    },
                    new TextRun { Text = " after" }
                ]
            }
        });
        return document;
    }
}
