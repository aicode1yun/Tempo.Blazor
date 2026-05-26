using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.Docx;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase13Tests
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        DocumentFormatTestData.TransparentPngDataUrl[(DocumentFormatTestData.TransparentPngDataUrl.IndexOf(',') + 1)..]);

    [Theory]
    [InlineData("bothSides", DocumentObjectWrapSide.BothSides)]
    [InlineData("left", DocumentObjectWrapSide.Left)]
    [InlineData("right", DocumentObjectWrapSide.Right)]
    [InlineData("largest", DocumentObjectWrapSide.Largest)]
    public async Task Phase13_ImportAsync_WrapSquareReadsNativeWrapTextSide(
        string nativeWrapText,
        DocumentObjectWrapSide expectedSide)
    {
        await using var package = CreateNativeAnchorDocx(new DW.WrapSquare
        {
            WrapText = nativeWrapText switch
            {
                "left" => DW.WrapTextValues.Left,
                "right" => DW.WrapTextValues.Right,
                "largest" => DW.WrapTextValues.Largest,
                _ => DW.WrapTextValues.BothSides
            },
            DistanceFromLeft = (UInt32Value)(uint)DocxUnitConverter.PointToEmu(2),
            DistanceFromRight = (UInt32Value)(uint)DocxUnitConverter.PointToEmu(3),
            DistanceFromTop = (UInt32Value)(uint)DocxUnitConverter.PointToEmu(4),
            DistanceFromBottom = (UInt32Value)(uint)DocxUnitConverter.PointToEmu(5)
        });

        var imported = await new DocumentDocxImporter().ImportAsync(package);
        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        drawing.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        drawing.Layout.Wrap.Side.Should().Be(expectedSide);
        drawing.Layout.Wrap.DistanceLeft.Should().BeApproximately(2, 0.1);
        drawing.Layout.Wrap.DistanceRight.Should().BeApproximately(3, 0.1);
        drawing.Layout.Wrap.DistanceTop.Should().BeApproximately(4, 0.1);
        drawing.Layout.Wrap.DistanceBottom.Should().BeApproximately(5, 0.1);
    }

    [Fact]
    public async Task Phase13_ImportAsync_WrapTopBottomReadsNativeTopAndBottomDistances()
    {
        await using var package = CreateNativeAnchorDocx(new DW.WrapTopBottom
        {
            DistanceFromTop = (UInt32Value)(uint)DocxUnitConverter.PointToEmu(7),
            DistanceFromBottom = (UInt32Value)(uint)DocxUnitConverter.PointToEmu(9)
        });

        var imported = await new DocumentDocxImporter().ImportAsync(package);
        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        drawing.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.TopBottom);
        drawing.Layout.Wrap.DistanceTop.Should().BeApproximately(7, 0.1);
        drawing.Layout.Wrap.DistanceBottom.Should().BeApproximately(9, 0.1);
    }

    [Fact]
    public async Task Phase13_ExportAsync_WritesNativeWrapModesDistancesSidesAndPolygons()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreatePhase13Document());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var square = package.AssertWrapMode(package.AssertHasAnchorPicture(altText: "Phase13 square"), "wrapSquare");
        ((string?)square.Attribute("wrapText")).Should().Be("right");
        ((uint?)square.Attribute("distL")).Should().Be((uint)DocxUnitConverter.PointToEmu(2));
        ((uint?)square.Attribute("distR")).Should().Be((uint)DocxUnitConverter.PointToEmu(3));
        ((uint?)square.Attribute("distT")).Should().Be((uint)DocxUnitConverter.PointToEmu(4));
        ((uint?)square.Attribute("distB")).Should().Be((uint)DocxUnitConverter.PointToEmu(5));

        var tight = package.AssertWrapMode(package.AssertHasAnchorPicture(altText: "Phase13 tight"), "wrapTight");
        ((string?)tight.Attribute("wrapText")).Should().Be("largest");
        tight.Element(DocxDrawingTestPackage.Wp + "wrapPolygon").Should().NotBeNull();
        tight.Element(DocxDrawingTestPackage.Wp + "wrapPolygon")!.Elements(DocxDrawingTestPackage.Wp + "lineTo").Should().HaveCount(3);

        var through = package.AssertWrapMode(package.AssertHasAnchorPicture(altText: "Phase13 through"), "wrapThrough");
        ((string?)through.Attribute("wrapText")).Should().Be("left");
        through.Element(DocxDrawingTestPackage.Wp + "wrapPolygon").Should().NotBeNull();

        var topBottom = package.AssertWrapMode(package.AssertHasAnchorPicture(altText: "Phase13 top bottom"), "wrapTopAndBottom");
        ((uint?)topBottom.Attribute("distT")).Should().Be((uint)DocxUnitConverter.PointToEmu(7));
        ((uint?)topBottom.Attribute("distB")).Should().Be((uint)DocxUnitConverter.PointToEmu(9));
    }

    [Fact]
    public async Task Phase13_RoundTrip_CenterSquareAnchorSurvivesExportImportExportImport()
    {
        var layout = CreateAnchoredLayout(
            DocumentWrapMode.Square,
            DocumentObjectWrapSide.BothSides,
            horizontalAlignment: DocumentImageHorizontalPosition.Center);
        layout.Position.HorizontalRelativeTo = DocumentRelativePosition.Margin;
        layout.Position.VerticalRelativeTo = DocumentRelativePosition.Paragraph;
        layout.Position.X = 0;
        layout.Position.Y = 14;

        var imported = await ExportImportExportImportAsync(CreateSingleDrawingDocument(
            "Phase13 center square",
            layout));

        var drawing = FindDrawing(imported, "Phase13 center square");
        drawing.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        drawing.Layout.Position.HorizontalAlignment.Should().Be(DocumentImageHorizontalPosition.Center);
        drawing.Layout.Position.HorizontalRelativeTo.Should().Be(DocumentRelativePosition.Margin);
        drawing.Layout.Position.VerticalRelativeTo.Should().Be(DocumentRelativePosition.Paragraph);
        drawing.Layout.Position.Y.Should().BeApproximately(14, 0.1);
    }

    [Fact]
    public async Task Phase13_RoundTrip_AbsoluteDragPositionSurvivesExportImportExportImport()
    {
        var layout = CreateAnchoredLayout(DocumentWrapMode.Square, DocumentObjectWrapSide.BothSides);
        layout.Position.HorizontalRelativeTo = DocumentRelativePosition.Page;
        layout.Position.VerticalRelativeTo = DocumentRelativePosition.Page;
        layout.Position.X = 37;
        layout.Position.Y = 43;
        layout.Position.HorizontalAlignment = null;
        layout.Position.VerticalAlignment = DocumentObjectVerticalAlignment.None;

        var imported = await ExportImportExportImportAsync(CreateSingleDrawingDocument(
            "Phase13 dragged square",
            layout));

        var drawing = FindDrawing(imported, "Phase13 dragged square");
        drawing.Layout.Position.HorizontalRelativeTo.Should().Be(DocumentRelativePosition.Page);
        drawing.Layout.Position.VerticalRelativeTo.Should().Be(DocumentRelativePosition.Page);
        drawing.Layout.Position.HorizontalAlignment.Should().BeNull();
        drawing.Layout.Position.X.Should().BeApproximately(37, 0.1);
        drawing.Layout.Position.Y.Should().BeApproximately(43, 0.1);
    }

    [Fact]
    public async Task Phase13_RoundTrip_TightPolygonSurvivesExportImportExportImport()
    {
        var imported = await ExportImportExportImportAsync(CreateSingleDrawingDocument(
            "Phase13 tight polygon",
            CreateAnchoredLayout(DocumentWrapMode.Tight, DocumentObjectWrapSide.Largest)));

        var drawing = FindDrawing(imported, "Phase13 tight polygon");
        drawing.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Tight);
        drawing.Layout.Wrap.Side.Should().Be(DocumentObjectWrapSide.Largest);
        drawing.Layout.Wrap.WrapContourPoints.Select(point => (Math.Round(point.X, 3), Math.Round(point.Y, 3)))
            .Should()
            .Equal((0.5, 0), (1, 0.5), (0.5, 1), (0, 0.5));
    }

    [Fact]
    public async Task Phase13_RoundTrip_BehindAndFrontLayersSurviveExportImportExportImport()
    {
        var document = DocumentEditorDocument.Empty("phase13-layer-roundtrip");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "phase13-layer-block",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Layers " },
                    CreateDrawing("Phase13 behind", ConfigureLayer(
                        CreateAnchoredLayout(DocumentWrapMode.BehindText, DocumentObjectWrapSide.BothSides),
                        zIndex: 2,
                        allowOverlap: true)),
                    new TextRun { Text = " and " },
                    CreateDrawing("Phase13 front", ConfigureLayer(
                        CreateAnchoredLayout(DocumentWrapMode.InFrontOfText, DocumentObjectWrapSide.BothSides),
                        zIndex: 8,
                        allowOverlap: true,
                        fixedOnPage: true))
                ]
            }
        });

        var imported = await ExportImportExportImportAsync(document);

        var behind = FindDrawing(imported, "Phase13 behind");
        behind.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.BehindText);
        behind.Layout.Stacking.ZIndex.Should().Be(2);
        behind.Layout.Stacking.AllowOverlap.Should().BeTrue();

        var front = FindDrawing(imported, "Phase13 front");
        front.Layout.Kind.Should().Be(DocumentObjectLayoutKind.Fixed);
        front.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.InFrontOfText);
        front.Layout.Anchor.FixedOnPage.Should().BeTrue();
        front.Layout.Stacking.ZIndex.Should().Be(8);
        front.Layout.Stacking.AllowOverlap.Should().BeTrue();
    }

    private static async Task<DocumentEditorDocument> ExportImportExportImportAsync(DocumentEditorDocument document)
    {
        var firstExport = await new DocumentDocxExporter().ExportAsync(document);
        var firstImport = await new DocumentDocxImporter().ImportAsync(new MemoryStream(firstExport.Content));
        var secondExport = await new DocumentDocxExporter().ExportAsync(firstImport.Document);
        var secondImport = await new DocumentDocxImporter().ImportAsync(new MemoryStream(secondExport.Content));
        return secondImport.Document;
    }

    private static DocumentEditorDocument CreatePhase13Document()
    {
        var document = DocumentEditorDocument.Empty("phase13-docx-drawing-parity");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "phase13-block",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    CreateDrawing("Phase13 square", CreateAnchoredLayout(DocumentWrapMode.Square, DocumentObjectWrapSide.Right, 2, 3, 4, 5)),
                    CreateDrawing("Phase13 tight", CreateAnchoredLayout(DocumentWrapMode.Tight, DocumentObjectWrapSide.Largest)),
                    CreateDrawing("Phase13 through", CreateAnchoredLayout(DocumentWrapMode.Through, DocumentObjectWrapSide.Left)),
                    CreateDrawing("Phase13 top bottom", CreateAnchoredLayout(DocumentWrapMode.TopBottom, DocumentObjectWrapSide.BothSides, distanceTop: 7, distanceBottom: 9))
                ]
            }
        });
        return document;
    }

    private static DocumentEditorDocument CreateSingleDrawingDocument(string altText, DocumentObjectLayout layout)
    {
        var document = DocumentEditorDocument.Empty($"phase13-{altText.Replace(' ', '-')}");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "phase13-single-block",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "Before " }, CreateDrawing(altText, layout), new TextRun { Text = " after" }]
            }
        });
        return document;
    }

    private static DocumentDrawingRun FindDrawing(DocumentEditorDocument document, string altText)
        => DocumentImagePersistence.EnumerateDrawingRuns(document).Single(drawing => drawing.AltText == altText);

    private static DocumentDrawingRun CreateDrawing(string altText, DocumentObjectLayout layout)
        => new()
        {
            Id = $"{altText.Replace(' ', '-').ToLowerInvariant()}-run",
            ObjectId = $"{altText.Replace(' ', '-').ToLowerInvariant()}-object",
            Source = DocumentImageSource.Url,
            Url = DocumentFormatTestData.TransparentPngDataUrl,
            AltText = altText,
            Size = new DocumentImageSize { Width = 120, Height = 80 },
            NaturalSize = new DocumentImageSize { Width = 120, Height = 80 },
            Layout = layout
        };

    private static DocumentObjectLayout ConfigureLayer(
        DocumentObjectLayout layout,
        int zIndex,
        bool allowOverlap,
        bool fixedOnPage = false)
    {
        layout.Stacking.ZIndex = zIndex;
        layout.Stacking.AllowOverlap = allowOverlap;
        if (fixedOnPage)
        {
            layout.Kind = DocumentObjectLayoutKind.Fixed;
            layout.Anchor.MoveWithText = false;
            layout.Anchor.FixedOnPage = true;
        }

        return layout;
    }

    private static DocumentObjectLayout CreateAnchoredLayout(
        DocumentWrapMode mode,
        DocumentObjectWrapSide side,
        double distanceLeft = 0,
        double distanceRight = 0,
        double distanceTop = 0,
        double distanceBottom = 0,
        DocumentImageHorizontalPosition? horizontalAlignment = null)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = "phase13-block",
                InlineIndex = 1,
                Offset = 7,
                MoveWithText = true
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Margin,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                X = 11,
                Y = 13,
                HorizontalAlignment = horizontalAlignment
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = mode,
                Side = side,
                DistanceLeft = distanceLeft,
                DistanceRight = distanceRight,
                DistanceTop = distanceTop,
                DistanceBottom = distanceBottom,
                WrapContourPoints =
                [
                    new() { X = 0.5, Y = 0 },
                    new() { X = 1, Y = 0.5 },
                    new() { X = 0.5, Y = 1 },
                    new() { X = 0, Y = 0.5 }
                ]
            },
            Transform = new DocumentObjectTransform
            {
                Width = 120,
                Height = 80,
                NaturalWidth = 120,
                NaturalHeight = 80,
                LockAspectRatio = true
            }
        };

    private static MemoryStream CreateNativeAnchorDocx(OpenXmlElement wrap)
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Native ")), CreateNativeAnchorRun(main, wrap)),
                new W.SectionProperties()));
            main.Document.Save();
        }

        memory.Position = 0;
        return memory;
    }

    private static W.Run CreateNativeAnchorRun(MainDocumentPart owner, OpenXmlElement wrap)
    {
        var imagePart = owner.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(PngBytes))
        {
            imagePart.FeedData(stream);
        }

        var cx = DocxUnitConverter.PointToEmu(120);
        var cy = DocxUnitConverter.PointToEmu(80);
        var anchor = new DW.Anchor(
            new DW.SimplePosition { X = 0L, Y = 0L },
            new DW.HorizontalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.HorizontalRelativePositionValues.Margin },
            new DW.VerticalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.VerticalRelativePositionValues.Paragraph },
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            wrap,
            new DW.DocProperties { Id = 1U, Name = "Phase13 native", Description = "Phase13 native" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            CreatePictureGraphic(owner.GetIdOfPart(imagePart), cx, cy))
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

    private static A.Graphic CreatePictureGraphic(string relationshipId, long cx, long cy)
        => new(new A.GraphicData(
            new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    new PIC.NonVisualDrawingProperties { Id = 2U, Name = "Phase13 native", Description = "Phase13 native" },
                    new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });
}
