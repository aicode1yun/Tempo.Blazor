using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase27Tests
{
    private const string TinyJpegDataUrl = "data:image/jpeg;base64,/9j/2Q==";

    [Fact]
    public void Phase27_UnitConverter_UsesDocxDrawingUnits()
    {
        DocxUnitConverter.PointToEmu(1).Should().Be(12700);
        DocxUnitConverter.EmuToPoint(12700).Should().Be(1);
        DocxUnitConverter.InchToEmu(1).Should().Be(914400);
        DocxUnitConverter.PixelToEmu(96, dpi: 96).Should().Be(914400);
        DocxUnitConverter.DegreeToRotation(15).Should().Be(900000);
        DocxUnitConverter.PercentToCrop(10).Should().Be(10000);
        DocxUnitConverter.CropToPercent(25000).Should().Be(25);
    }

    [Fact]
    public void Phase27_ImageContentTypeMapper_UsesMimeExtensionAndSignatures()
    {
        DocxImageContentTypeMapper.TryResolve("image/jpeg", null, [], out var jpeg).Should().BeTrue();
        jpeg.ImagePartType.Should().Be(ImagePartType.Jpeg);
        jpeg.ContentType.Should().Be("image/jpeg");

        DocxImageContentTypeMapper.TryResolve(null, "diagram.png", [], out var pngByExtension).Should().BeTrue();
        pngByExtension.ImagePartType.Should().Be(ImagePartType.Png);

        DocxImageContentTypeMapper.TryResolve(null, null, [0xFF, 0xD8, 0xFF, 0xD9], out var jpegBySignature).Should().BeTrue();
        jpegBySignature.ImagePartType.Should().Be(ImagePartType.Jpeg);

        DocxImageContentTypeMapper.TryParseDataUrl(TinyJpegDataUrl, out var data).Should().BeTrue();
        data.ContentType.Should().Be("image/jpeg");
        data.Content.Should().StartWith([0xFF, 0xD8, 0xFF]);
    }

    [Theory]
    [InlineData(DocumentFormatTestData.TransparentPngDataUrl, "image/png")]
    [InlineData(TinyJpegDataUrl, "image/jpeg")]
    public async Task Phase27_ExportAsync_UsesActualImagePartTypeForDataUrls(string dataUrl, string expectedContentType)
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateImageDocument(dataUrl));

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);

        exported.Warnings.Should().BeEmpty();
        word.MainDocumentPart!.ImageParts.Should().ContainSingle();
        word.MainDocumentPart.ImageParts.Single().ContentType.Should().Be(expectedContentType);
    }

    [Fact]
    public async Task Phase27_ExportAsync_UnsupportedImageTypeWarnsAndDoesNotCreateImplicitPngPlaceholder()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateImageDocument("data:image/avif;base64,AAAA"));

        exported.Warnings.Should().ContainSingle(warning =>
            warning.Code == "docx.imageUnsupportedContentType"
            && warning.Severity == DocumentFormatCompatibilitySeverity.Dropped);

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        word.MainDocumentPart!.ImageParts.Should().BeEmpty();
    }

    [Fact]
    public async Task Phase27_ExportAsync_UnsupportedImageTypeUsesPlaceholderOnlyWhenAllowed()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(
            CreateImageDocument("data:image/avif;base64,AAAA"),
            new DocumentFormatExportOptions { AllowImagePlaceholders = true });

        exported.Warnings.Should().ContainSingle(warning =>
            warning.Code == "docx.imageUnsupportedContentType"
            && warning.Severity == DocumentFormatCompatibilitySeverity.Warning
            && warning.Message.Contains("placeholder", StringComparison.OrdinalIgnoreCase));

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        word.MainDocumentPart!.ImageParts.Should().ContainSingle();
        word.MainDocumentPart.ImageParts.Single().ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task Phase27_ExportAsync_ExternalUrlWarnsAndRequiresExplicitPlaceholder()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateImageDocument("https://example.test/image.png"));

        exported.Warnings.Should().ContainSingle(warning =>
            warning.Code == "docx.imageExternalUrlUnsupported"
            && warning.Severity == DocumentFormatCompatibilitySeverity.Dropped);

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        word.MainDocumentPart!.ImageParts.Should().BeEmpty();
    }

    [Fact]
    public async Task Phase27_ImportAsync_SourceRectangleImportsAsPercentCrop()
    {
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(DocxDrawingFixtureBuilder.CreateCroppedInline()));

        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        drawing.Layout.Transform.Crop.Left.Should().Be(10);
        drawing.Layout.Transform.Crop.Top.Should().Be(20);
        drawing.Layout.Transform.Crop.Right.Should().Be(30);
        drawing.Layout.Transform.Crop.Bottom.Should().Be(40);
    }

    [Fact]
    public async Task Phase27_ImportAsync_MissingImagePartEmitsWarning()
    {
        await using var package = CreateDocxWithMissingImageRelationship();

        var imported = await new DocumentDocxImporter().ImportAsync(package);

        imported.Warnings.Should().ContainSingle(warning =>
            warning.Code == "docx.imageMissingPart"
            && warning.Severity == DocumentFormatCompatibilitySeverity.Dropped);
        DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Should().BeEmpty();
    }

    private static DocumentEditorDocument CreateImageDocument(string imageUrl)
    {
        var document = DocumentEditorDocument.Empty("phase27-image");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "image-1",
            Type = DocumentBlockType.Image,
            Order = 0,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = imageUrl,
                AltText = "Phase 27 image",
                Size = new DocumentImageSize { Width = 120, Height = 80 }
            }
        });
        return document;
    }

    private static MemoryStream CreateDocxWithMissingImageRelationship()
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Drawing(CreateInlineWithMissingRelationship()))),
                new W.SectionProperties()));
            main.Document.Save();
        }

        memory.Position = 0;
        return memory;
    }

    private static DW.Inline CreateInlineWithMissingRelationship()
        => new(
            new DW.Extent
            {
                Cx = DocxUnitConverter.PointToEmu(120),
                Cy = DocxUnitConverter.PointToEmu(80)
            },
            new DW.DocProperties { Id = 1U, Name = "Missing image", Description = "Missing image" },
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 1U, Name = "Missing image", Description = "Missing image" },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = "rIdMissingImage" }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(
                            new A.Offset { X = 0L, Y = 0L },
                            new A.Extents
                            {
                                Cx = DocxUnitConverter.PointToEmu(120),
                                Cy = DocxUnitConverter.PointToEmu(80)
                            }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }));
}
