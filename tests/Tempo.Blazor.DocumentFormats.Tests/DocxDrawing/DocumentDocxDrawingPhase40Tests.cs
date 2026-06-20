using System.Diagnostics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase40Tests
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    private static readonly byte[] JpegSignatureBytes = [0xFF, 0xD8, 0xFF, 0xD9];

    [Fact]
    public async Task Phase40_ImportAsync_FiftyImagesCompletesUnderLimit()
    {
        await using var package = CreateDocxWithEmbeddedImages(50);
        var stopwatch = Stopwatch.StartNew();

        var imported = await new DocumentDocxImporter().ImportAsync(package);

        stopwatch.Stop();
        DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Should().HaveCount(50);
        imported.Warnings.Should().BeEmpty();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Phase40_ExportAsync_FiftyRepeatedProviderImagesCachesAssetBytesUnderLimit()
    {
        var resolverCalls = 0;
        var document = CreateRepeatedAssetDocument(50);
        var stopwatch = Stopwatch.StartNew();

        var exported = await new DocumentDocxExporter().ExportAsync(document, new DocumentFormatExportOptions
        {
            ImageResolver = (_, _) =>
            {
                resolverCalls++;
                return Task.FromResult<DocumentFormatImageExportResult?>(new DocumentFormatImageExportResult
                {
                    ContentType = "image/png",
                    FileName = "shared.png",
                    Content = PngBytes
                });
            }
        });

        stopwatch.Stop();
        resolverCalls.Should().Be(1);
        exported.Warnings.Should().BeEmpty();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        word.MainDocumentPart!.ImageParts.Should().HaveCount(50);
    }

    [Fact]
    public async Task Phase40_ImportAsync_ImagePartOverLimitWarnsWithPartPathAndObjectId()
    {
        await using var package = CreateDocxWithEmbeddedImages(1);

        var imported = await new DocumentDocxImporter().ImportAsync(package, new DocumentFormatImportOptions
        {
            MaxImagePartBytes = PngBytes.Length - 1
        });

        var warning = imported.Warnings.Should().ContainSingle(item => item.Code == "docx.imagePartTooLarge").Subject;
        warning.Severity.Should().Be(DocumentFormatCompatibilitySeverity.Dropped);
        warning.SourcePath.Should().Contain("/media/");
        warning.ObjectId.Should().NotBeNullOrWhiteSpace();
        DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Should().BeEmpty();
    }

    [Fact]
    public async Task Phase40_ImportAsync_ContentTypeSignatureMismatchDropsImageAndWarns()
    {
        await using var package = CreateDocxWithSingleEmbeddedImage(
            ImagePartType.Png,
            JpegSignatureBytes,
            "Mismatch image");

        var imported = await new DocumentDocxImporter().ImportAsync(package);

        var warning = imported.Warnings.Should().ContainSingle(item => item.Code == "docx.imageContentTypeMismatch").Subject;
        warning.Severity.Should().Be(DocumentFormatCompatibilitySeverity.Dropped);
        warning.SourcePath.Should().Contain("/media/");
        warning.ObjectId.Should().NotBeNullOrWhiteSpace();
        DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Should().BeEmpty();
    }

    [Fact]
    public async Task Phase40_ImportAsync_ExternalLinkDoesNotDownloadOrInvokeImageImporterByDefault()
    {
        await using var package = CreateDocxWithExternalLinkedImage();
        var imageImporterCalled = false;

        var imported = await new DocumentDocxImporter().ImportAsync(package, new DocumentFormatImportOptions
        {
            ImageImporter = (_, _) =>
            {
                imageImporterCalled = true;
                return Task.FromResult(new DocumentFormatImageImportResult { AssetId = "unexpected" });
            }
        });

        imageImporterCalled.Should().BeFalse();
        imported.Warnings.Should().ContainSingle(item =>
            item.Code == "docx.imageExternalReference"
            && item.Severity == DocumentFormatCompatibilitySeverity.Dropped
            && !string.IsNullOrWhiteSpace(item.ObjectId));
        DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Should().BeEmpty();
    }

    [Fact]
    public async Task Phase40_ExportAsync_LargeDataUrlDoesNotCreateImagePartWithoutWarning()
    {
        var document = CreateDataUrlDocument("phase40-large-data-url", $"data:image/png;base64,{Convert.ToBase64String(new byte[64])}");

        var exported = await new DocumentDocxExporter().ExportAsync(document, new DocumentFormatExportOptions
        {
            MaxImagePartBytes = 8
        });

        var warning = exported.Warnings.Should().ContainSingle(item => item.Code == "docx.imagePartTooLarge").Subject;
        warning.Severity.Should().Be(DocumentFormatCompatibilitySeverity.Dropped);
        warning.SourcePath.Should().Be("data-url");
        warning.ObjectId.Should().Be("phase40-large-data-url");

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        word.MainDocumentPart!.ImageParts.Should().BeEmpty();
    }

    [Fact]
    public async Task Phase40_ExportAsync_ExternalUrlDoesNotDownloadByDefaultAndWarnsWithObjectId()
    {
        var document = CreateDataUrlDocument("phase40-external-url", "https://example.test/image.png");

        var exported = await new DocumentDocxExporter().ExportAsync(document);

        var warning = exported.Warnings.Should().ContainSingle(item => item.Code == "docx.imageExternalUrlUnsupported").Subject;
        warning.Severity.Should().Be(DocumentFormatCompatibilitySeverity.Dropped);
        warning.SourcePath.Should().Be("https://example.test/image.png");
        warning.ObjectId.Should().Be("phase40-external-url");
    }

    private static DocumentEditorDocument CreateRepeatedAssetDocument(int count)
    {
        var document = DocumentEditorDocument.Empty("phase40-repeated-assets");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "phase40-paragraph",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = Enumerable.Range(0, count)
                    .Select(index => (InlineContent)CreateDrawingRun($"phase40-asset-{index}", DocumentImageSource.Asset, assetId: "shared-asset"))
                    .ToList()
            }
        });
        return document;
    }

    private static DocumentEditorDocument CreateDataUrlDocument(string objectId, string url)
    {
        var document = DocumentEditorDocument.Empty("phase40-data-url");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "phase40-paragraph",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [CreateDrawingRun(objectId, DocumentImageSource.Url, url: url)]
            }
        });
        return document;
    }

    private static DocumentDrawingRun CreateDrawingRun(
        string objectId,
        DocumentImageSource source,
        string? assetId = null,
        string? url = null)
        => new()
        {
            Id = $"{objectId}-run",
            ObjectId = objectId,
            Source = source,
            AssetId = assetId,
            Url = url,
            AltText = objectId,
            Size = new DocumentImageSize { Width = 24, Height = 24 },
            Layout = new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Inline,
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Inline },
                Transform = new DocumentObjectTransform { Width = 24, Height = 24 }
            }
        };

    private static MemoryStream CreateDocxWithEmbeddedImages(int count)
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            var runs = Enumerable.Range(0, count)
                .SelectMany(index => new OpenXmlElement[]
                {
                    new W.Run(new W.Text($"Before {index} ")),
                    CreateDrawingRun(main, ImagePartType.Png, PngBytes, $"Import image {index}"),
                    new W.Run(new W.Text(" after "))
                })
                .ToArray();

            main.Document = new W.Document(new W.Body(new W.Paragraph(runs), new W.SectionProperties()));
            main.Document.Save();
        }

        memory.Position = 0;
        return memory;
    }

    private static MemoryStream CreateDocxWithSingleEmbeddedImage(
        PartTypeInfo imagePartType,
        byte[] bytes,
        string description)
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(CreateDrawingRun(main, imagePartType, bytes, description)),
                new W.SectionProperties()));
            main.Document.Save();
        }

        memory.Position = 0;
        return memory;
    }

    private static MemoryStream CreateDocxWithExternalLinkedImage()
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Drawing(CreateInlineDrawing("rIdExternalImage", "External image", isExternal: true)))),
                new W.SectionProperties()));
            main.Document.Save();
        }

        memory.Position = 0;
        return memory;
    }

    private static W.Run CreateDrawingRun(
        MainDocumentPart owner,
        PartTypeInfo imagePartType,
        byte[] bytes,
        string description)
    {
        var imagePart = owner.AddImagePart(imagePartType);
        using (var stream = new MemoryStream(bytes))
        {
            imagePart.FeedData(stream);
        }

        return new W.Run(new W.Drawing(CreateInlineDrawing(owner.GetIdOfPart(imagePart), description)));
    }

    private static DW.Inline CreateInlineDrawing(string relationshipId, string description, bool isExternal = false)
        => new(
            new DW.Extent
            {
                Cx = DocxUnitConverter.PointToEmu(24),
                Cy = DocxUnitConverter.PointToEmu(24)
            },
            new DW.DocProperties { Id = 1U, Name = description, Description = description },
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 1U, Name = description, Description = description },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(
                        isExternal ? new A.Blip { Link = relationshipId } : new A.Blip { Embed = relationshipId },
                        new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(
                            new A.Offset { X = 0L, Y = 0L },
                            new A.Extents
                            {
                                Cx = DocxUnitConverter.PointToEmu(24),
                                Cy = DocxUnitConverter.PointToEmu(24)
                            }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }));
}
