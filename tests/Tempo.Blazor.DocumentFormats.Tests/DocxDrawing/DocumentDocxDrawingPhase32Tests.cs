using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase32Tests
{
    private const string RoundRectPreset = "roundRect";

    private static readonly byte[] PngBytes = Convert.FromBase64String(
        DocumentFormatTestData.TransparentPngDataUrl[(DocumentFormatTestData.TransparentPngDataUrl.IndexOf(',') + 1)..]);

    [Fact]
    public async Task Phase32_ExportAsync_WritesTransformCropStretchAndPreservedEffectExtent()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateTransformDocument());

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var anchor = FindAnchor(word, "Phase 32 transformed image");
        var hostExtent = anchor.GetFirstChild<DW.Extent>()!;
        var transform = anchor.Descendants<A.Transform2D>().Single();

        transform.Rotation!.Value.Should().Be(DocxUnitConverter.DegreeToRotation(17.25));
        transform.HorizontalFlip!.Value.Should().BeTrue();
        transform.VerticalFlip!.Value.Should().BeTrue();
        transform.GetFirstChild<A.Extents>()!.Cx!.Value.Should().Be(hostExtent.Cx!.Value);
        transform.GetFirstChild<A.Extents>()!.Cy!.Value.Should().Be(hostExtent.Cy!.Value);
        anchor.GetFirstChild<DW.EffectExtent>()!.LeftEdge!.Value.Should().Be(111L);
        anchor.GetFirstChild<DW.EffectExtent>()!.RightEdge!.Value.Should().Be(333L);

        var srcRect = anchor.Descendants<A.SourceRectangle>().Single();
        srcRect.Left!.Value.Should().Be(DocxUnitConverter.PercentToCrop(10));
        srcRect.Top!.Value.Should().Be(DocxUnitConverter.PercentToCrop(20));
        srcRect.Right!.Value.Should().Be(DocxUnitConverter.PercentToCrop(30));
        srcRect.Bottom!.Value.Should().Be(DocxUnitConverter.PercentToCrop(40));
        anchor.Descendants<A.Stretch>().Single().GetFirstChild<A.FillRectangle>().Should().NotBeNull();
    }

    [Fact]
    public async Task Phase32_ExportAsync_DoesNotWriteEmptySourceRectangle()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateTransformDocument(includeCrop: false));

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);

        FindAnchor(word, "Phase 32 transformed image").Descendants<A.SourceRectangle>().Should().BeEmpty();
    }

    [Fact]
    public async Task Phase32_ImportAsync_ReadsRotationFlipAndCropFromNativeDrawingMl()
    {
        await using var package = CreateNativeTransformDocx();

        var imported = await new DocumentDocxImporter().ImportAsync(package);
        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        drawing.Layout.Transform.Rotation.Should().BeApproximately(22.5, 0.001);
        drawing.Layout.Transform.Flip.Should().NotBeNull();
        drawing.Layout.Transform.Flip!.Horizontal.Should().BeTrue();
        drawing.Layout.Transform.Flip.Vertical.Should().BeTrue();
        drawing.Layout.Transform.Crop.Left.Should().Be(12);
        drawing.Layout.Transform.Crop.Top.Should().Be(8);
        drawing.Layout.Transform.Crop.Right.Should().Be(6);
        drawing.Layout.Transform.Crop.Bottom.Should().Be(4);
    }

    [Fact]
    public async Task Phase32_ImportAsync_TileFillWarnsAndPreservesMetadata()
    {
        await using var package = CreateNativeTransformDocx(fillMode: DocumentDocxBlipFillMode.Tile);

        var imported = await new DocumentDocxImporter().ImportAsync(package);
        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        imported.Warnings.Should().Contain(warning =>
            warning.Code == "docx.drawingBlipFillTileUnsupported"
            && warning.Severity == DocumentFormatCompatibilitySeverity.Warning);
        drawing.Docx!.BlipFillMode.Should().Be(DocumentDocxBlipFillMode.Tile);
        drawing.Docx.RawBlipFillXml.Should().Contain("tile");
    }

    [Fact]
    public async Task Phase32_ImportAsync_NonRectPresetGeometryWarnsAndPreservesMetadata()
    {
        await using var package = CreateNativeTransformDocx(presetGeometry: RoundRectPreset);
        using (var raw = DocxDrawingTestPackage.Open(package.ToArray()))
        {
            raw.DocumentXml.ToString().Should().Contain(RoundRectPreset);
        }

        package.Position = 0;

        var imported = await new DocumentDocxImporter().ImportAsync(package);
        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        imported.Warnings.Should().Contain(warning =>
            warning.Code == "docx.drawingPresetGeometryFallback"
            && warning.Severity == DocumentFormatCompatibilitySeverity.Warning);
        drawing.Docx!.PresetGeometry.Should().Be(RoundRectPreset);
        drawing.Docx.RawShapePropertiesXml.Should().Contain("prstGeom");
        drawing.Layout.Transform.Width.Should().BeApproximately(140, 0.1);
        drawing.Layout.Transform.Height.Should().BeApproximately(84, 0.1);
    }

    [Fact]
    public void Phase32_CropAndTransformConverters_UseDocumentPercentAndDrawingMlUnits()
    {
        var srcRect = DocxCropConverter.ToSourceRectangle(new DocumentObjectCrop { Left = 1.5, Top = 2.5, Right = 3.5, Bottom = 4.5 })!;
        var crop = DocxCropConverter.FromSourceRectangle(srcRect);
        var transform = DocxTransformConverter.ToTransform2D(
            new DocumentObjectTransform
            {
                Rotation = -7.5,
                Flip = new DocumentObjectFlip { Horizontal = true }
            },
            cx: 123L,
            cy: 456L);

        srcRect.Left!.Value.Should().Be(1500);
        crop.Top.Should().Be(2.5);
        transform.Rotation!.Value.Should().Be(DocxUnitConverter.DegreeToRotation(-7.5));
        transform.HorizontalFlip!.Value.Should().BeTrue();
        transform.GetFirstChild<A.Extents>()!.Cy!.Value.Should().Be(456L);
        DocxCropConverter.ToSourceRectangle(new DocumentObjectCrop()).Should().BeNull();
    }

    private static DW.Anchor FindAnchor(WordprocessingDocument word, string altText)
        => word.MainDocumentPart!.Document.Body!.Descendants<DW.Anchor>()
            .Single(anchor => anchor.Descendants<DW.DocProperties>().Any(properties => properties.Description?.Value == altText));

    private static DocumentEditorDocument CreateTransformDocument(bool includeCrop = true)
    {
        var document = DocumentEditorDocument.Empty("phase32-transform");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new DocumentDrawingRun
                    {
                        Source = DocumentImageSource.Url,
                        Url = DocumentFormatTestData.TransparentPngDataUrl,
                        AltText = "Phase 32 transformed image",
                        Size = new DocumentImageSize { Width = 160, Height = 90 },
                        Layout = new DocumentObjectLayout
                        {
                            Kind = DocumentObjectLayoutKind.Anchored,
                            Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Square },
                            Transform = new DocumentObjectTransform
                            {
                                Width = 160,
                                Height = 90,
                                Rotation = 17.25,
                                Flip = new DocumentObjectFlip { Horizontal = true, Vertical = true },
                                Crop = includeCrop
                                    ? new DocumentObjectCrop { Left = 10, Top = 20, Right = 30, Bottom = 40 }
                                    : new DocumentObjectCrop()
                            }
                        },
                        Docx = new DocumentDocxDrawingMetadata
                        {
                            EffectExtent = new DocumentObjectEffectExtent
                            {
                                Left = 111,
                                Top = 222,
                                Right = 333,
                                Bottom = 444
                            }
                        }
                    }
                ]
            }
        });
        return document;
    }

    private static MemoryStream CreateNativeTransformDocx(
        DocumentDocxBlipFillMode fillMode = DocumentDocxBlipFillMode.Stretch,
        string presetGeometry = "rect")
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Before ")), CreateNativeDrawingRun(main, fillMode, presetGeometry)),
                new W.SectionProperties()));
            main.Document.Save();
        }

        memory.Position = 0;
        return memory;
    }

    private static W.Run CreateNativeDrawingRun(
        MainDocumentPart owner,
        DocumentDocxBlipFillMode fillMode,
        string presetGeometry)
    {
        var imagePart = owner.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(PngBytes))
        {
            imagePart.FeedData(stream);
        }

        var relId = owner.GetIdOfPart(imagePart);
        var cx = DocxUnitConverter.PointToEmu(140);
        var cy = DocxUnitConverter.PointToEmu(84);
        var anchor = new DW.Anchor(
            new DW.SimplePosition { X = 0L, Y = 0L },
            new DW.HorizontalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.HorizontalRelativePositionValues.Margin },
            new DW.VerticalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.VerticalRelativePositionValues.Paragraph },
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.WrapSquare { WrapText = DW.WrapTextValues.BothSides },
            new DW.DocProperties { Id = 1U, Name = "Native transform", Description = "Native transform" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            CreatePictureGraphic(relId, cx, cy, fillMode, presetGeometry))
        {
            SimplePos = false,
            RelativeHeight = 1U,
            BehindDoc = false,
            Locked = false,
            LayoutInCell = true,
            AllowOverlap = true
        };

        return new W.Run(new W.Drawing(anchor));
    }

    private static A.Graphic CreatePictureGraphic(
        string relationshipId,
        long cx,
        long cy,
        DocumentDocxBlipFillMode fillMode,
        string presetGeometry)
        => new(new A.GraphicData(
            new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    new PIC.NonVisualDrawingProperties { Id = 2U, Name = "Native transform", Description = "Native transform" },
                    new PIC.NonVisualPictureDrawingProperties()),
                CreateBlipFill(relationshipId, fillMode),
                new PIC.ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = 0L, Y = 0L },
                        new A.Extents { Cx = cx, Cy = cy })
                    {
                        Rotation = DocxUnitConverter.DegreeToRotation(22.5),
                        HorizontalFlip = true,
                        VerticalFlip = true
                    },
                    CreatePresetGeometry(presetGeometry))))
        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });

    private static A.PresetGeometry CreatePresetGeometry(string preset)
    {
        var geometry = new A.PresetGeometry(new A.AdjustValueList());
        geometry.SetAttribute(new OpenXmlAttribute("prst", string.Empty, preset));
        return geometry;
    }

    private static PIC.BlipFill CreateBlipFill(string relationshipId, DocumentDocxBlipFillMode fillMode)
    {
        var blipFill = new PIC.BlipFill(new A.Blip { Embed = relationshipId });
        if (fillMode == DocumentDocxBlipFillMode.Tile)
        {
            blipFill.Append(new A.Tile());
            return blipFill;
        }

        blipFill.Append(new A.SourceRectangle
        {
            Left = DocxUnitConverter.PercentToCrop(12),
            Top = DocxUnitConverter.PercentToCrop(8),
            Right = DocxUnitConverter.PercentToCrop(6),
            Bottom = DocxUnitConverter.PercentToCrop(4)
        });
        blipFill.Append(new A.Stretch(new A.FillRectangle()));
        return blipFill;
    }
}
