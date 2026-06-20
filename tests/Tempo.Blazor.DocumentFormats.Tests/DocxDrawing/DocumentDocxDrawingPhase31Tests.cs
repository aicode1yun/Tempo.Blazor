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

public sealed class DocumentDocxDrawingPhase31Tests
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        DocumentFormatTestData.TransparentPngDataUrl[(DocumentFormatTestData.TransparentPngDataUrl.IndexOf(',') + 1)..]);

    [Fact]
    public async Task Phase31_ExportAsync_WritesNativeWrapModesSidesDistancesAndPolygons()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateWrapModesDocument());

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);

        var square = FindAnchor(word, "Square wrap").GetFirstChild<DW.WrapSquare>()!;
        square.WrapText!.Value.Should().Be(DW.WrapTextValues.BothSides);
        square.DistanceFromLeft!.Value.Should().Be((uint)DocxUnitConverter.PointToEmu(4));
        square.DistanceFromRight!.Value.Should().Be((uint)DocxUnitConverter.PointToEmu(6));
        square.DistanceFromTop!.Value.Should().Be((uint)DocxUnitConverter.PointToEmu(2));
        square.DistanceFromBottom!.Value.Should().Be((uint)DocxUnitConverter.PointToEmu(8));

        FindAnchor(word, "Top bottom wrap").GetFirstChild<DW.WrapTopBottom>().Should().NotBeNull();
        FindAnchor(word, "Behind wrap").GetFirstChild<DW.WrapNone>().Should().NotBeNull();
        FindAnchor(word, "Behind wrap").BehindDoc!.Value.Should().BeTrue();
        FindAnchor(word, "Front wrap").GetFirstChild<DW.WrapNone>().Should().NotBeNull();
        FindAnchor(word, "Front wrap").BehindDoc!.Value.Should().BeFalse();

        var tight = FindAnchor(word, "Tight wrap").GetFirstChild<DW.WrapTight>()!;
        tight.WrapText!.Value.Should().Be(DW.WrapTextValues.Largest);
        tight.GetFirstChild<DW.WrapPolygon>()!.Elements<DW.LineTo>().Should().HaveCount(3);
        tight.GetFirstChild<DW.WrapPolygon>()!.GetFirstChild<DW.StartPoint>()!.X!.Value.Should().Be(DocxUnitConverter.PointToEmu(60));

        var through = FindAnchor(word, "Through wrap").GetFirstChild<DW.WrapThrough>()!;
        through.WrapText!.Value.Should().Be(DW.WrapTextValues.Left);
        through.GetFirstChild<DW.WrapPolygon>()!.Elements<DW.LineTo>().Should().HaveCount(3);
    }

    [Fact]
    public async Task Phase31_ImportAsync_WrapSquareReadsNativeDistancesAndSide()
    {
        await using var package = CreateNativeWrapDocx(CreateSquareWrap());

        var imported = await new DocumentDocxImporter().ImportAsync(package);
        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        drawing.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        drawing.Layout.Wrap.Side.Should().Be(DocumentObjectWrapSide.Right);
        drawing.Layout.Wrap.DistanceLeft.Should().BeApproximately(2, 0.1);
        drawing.Layout.Wrap.DistanceRight.Should().BeApproximately(3, 0.1);
        drawing.Layout.Wrap.DistanceTop.Should().BeApproximately(4, 0.1);
        drawing.Layout.Wrap.DistanceBottom.Should().BeApproximately(5, 0.1);
    }

    [Theory]
    [InlineData(DocumentWrapMode.Tight)]
    [InlineData(DocumentWrapMode.Through)]
    public async Task Phase31_ImportAsync_TightAndThroughReadNativeWrapPolygon(DocumentWrapMode mode)
    {
        await using var package = CreateNativeWrapDocx(CreatePolygonWrap(mode, includePolygon: true));

        var imported = await new DocumentDocxImporter().ImportAsync(package);
        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        drawing.Layout.Wrap.Mode.Should().Be(mode);
        drawing.Layout.Wrap.Side.Should().Be(DocumentObjectWrapSide.Left);
        drawing.Layout.Wrap.WrapContourPoints.Select(point => (point.X, point.Y))
            .Should()
            .Equal((0.5, 0), (1, 0.5), (0.5, 1), (0, 0.5));
    }

    [Fact]
    public async Task Phase31_ImportAsync_TightWithoutPolygonWarnsAndUsesRectangleFallback()
    {
        await using var package = CreateNativeWrapDocx(CreatePolygonWrap(DocumentWrapMode.Tight, includePolygon: false));

        var imported = await new DocumentDocxImporter().ImportAsync(package);
        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        imported.Warnings.Should().Contain(warning =>
            warning.Code == "docx.drawingWrapPolygonMissing"
            && warning.Severity == DocumentFormatCompatibilitySeverity.Warning);
        drawing.Layout.Wrap.WrapContourPoints.Select(point => (point.X, point.Y))
            .Should()
            .Equal((0, 0), (1, 0), (1, 1), (0, 1));
    }

    [Theory]
    [InlineData(DocumentWrapMode.Tight)]
    [InlineData(DocumentWrapMode.Through)]
    public async Task Phase31_ImportedContourDoesNotBlockMoreLineWidthThanSquareFallback(DocumentWrapMode mode)
    {
        await using var package = CreateNativeWrapDocx(CreatePolygonWrap(mode, includePolygon: true));
        var imported = await new DocumentDocxImporter().ImportAsync(package);
        var layout = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single().Layout;
        var contourIntervals = AvailableIntervals(layout);

        var squareLayout = CloneLayout(layout);
        squareLayout.Wrap.Mode = DocumentWrapMode.Square;
        squareLayout.Wrap.WrapContourPoints.Clear();
        var squareIntervals = AvailableIntervals(squareLayout);

        contourIntervals.Sum(interval => interval.Width).Should().BeGreaterThanOrEqualTo(squareIntervals.Sum(interval => interval.Width));
    }

    private static IReadOnlyList<DocumentLayoutInterval> AvailableIntervals(DocumentObjectLayout layout)
    {
        var body = Rect(0, 0, 320, 240);
        var objectRect = Rect(100, 80, 120, 80);
        var box = new DocumentObjectLayoutBox
        {
            Id = "imported-wrap",
            BlockId = "imported-wrap",
            ObjectRect = objectRect,
            WrapRect = DocumentLayoutGeometryHelper.ComputeWrapRect(objectRect, layout.Wrap),
            Layout = layout
        };
        var exclusions = DocumentLayoutGeometryHelper.BuildExclusionZones([box], body);
        return DocumentLayoutGeometryHelper.GetAvailableLineIntervals(88, 12, exclusions, body);
    }

    private static DocumentObjectLayout CloneLayout(DocumentObjectLayout layout)
        => new()
        {
            Kind = layout.Kind,
            Anchor = layout.Anchor,
            Position = layout.Position,
            Wrap = new DocumentObjectWrap
            {
                Mode = layout.Wrap.Mode,
                Side = layout.Wrap.Side,
                DistanceLeft = layout.Wrap.DistanceLeft,
                DistanceRight = layout.Wrap.DistanceRight,
                DistanceTop = layout.Wrap.DistanceTop,
                DistanceBottom = layout.Wrap.DistanceBottom,
                WrapContourPoints = layout.Wrap.WrapContourPoints.Select(point => new DocumentObjectWrapPoint { X = point.X, Y = point.Y }).ToList()
            },
            Transform = layout.Transform,
            Stacking = layout.Stacking
        };

    private static DW.Anchor FindAnchor(WordprocessingDocument word, string altText)
        => word.MainDocumentPart!.Document.Body!.Descendants<DW.Anchor>()
            .Single(anchor => anchor.Descendants<DW.DocProperties>().Any(properties => properties.Description?.Value == altText));

    private static DocumentEditorDocument CreateWrapModesDocument()
    {
        var document = DocumentEditorDocument.Empty("phase31-wrap-modes");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    CreateDrawing("Square wrap", DocumentWrapMode.Square, DocumentObjectWrapSide.BothSides, 4, 6, 2, 8),
                    CreateDrawing("Top bottom wrap", DocumentWrapMode.TopBottom, DocumentObjectWrapSide.BothSides),
                    CreateDrawing("Behind wrap", DocumentWrapMode.BehindText, DocumentObjectWrapSide.BothSides),
                    CreateDrawing("Front wrap", DocumentWrapMode.InFrontOfText, DocumentObjectWrapSide.BothSides),
                    CreateDrawing("Tight wrap", DocumentWrapMode.Tight, DocumentObjectWrapSide.Largest),
                    CreateDrawing("Through wrap", DocumentWrapMode.Through, DocumentObjectWrapSide.Left)
                ]
            }
        });
        return document;
    }

    private static DocumentDrawingRun CreateDrawing(
        string altText,
        DocumentWrapMode mode,
        DocumentObjectWrapSide side,
        double distanceLeft = 0,
        double distanceRight = 0,
        double distanceTop = 0,
        double distanceBottom = 0)
        => new()
        {
            Source = DocumentImageSource.Url,
            Url = DocumentFormatTestData.TransparentPngDataUrl,
            AltText = altText,
            Size = new DocumentImageSize { Width = 120, Height = 80 },
            Layout = new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Anchored,
                Position = new DocumentObjectPosition { HorizontalRelativeTo = DocumentRelativePosition.Margin, VerticalRelativeTo = DocumentRelativePosition.Paragraph },
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
                Transform = new DocumentObjectTransform { Width = 120, Height = 80 }
            }
        };

    private static MemoryStream CreateNativeWrapDocx(OpenXmlElement wrap)
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Before ")), CreateNativeAnchorRun(main, wrap)),
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

        var relId = owner.GetIdOfPart(imagePart);
        var cx = DocxUnitConverter.PointToEmu(120);
        var cy = DocxUnitConverter.PointToEmu(80);
        var anchor = new DW.Anchor(
            new DW.SimplePosition { X = 0L, Y = 0L },
            new DW.HorizontalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.HorizontalRelativePositionValues.Margin },
            new DW.VerticalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.VerticalRelativePositionValues.Paragraph },
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            wrap,
            new DW.DocProperties { Id = 1U, Name = "Native wrap", Description = "Native wrap" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            CreatePictureGraphic(relId, cx, cy))
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

    private static DW.WrapSquare CreateSquareWrap()
        => new()
        {
            WrapText = DW.WrapTextValues.Right,
            DistanceFromLeft = (UInt32Value)(uint)DocxUnitConverter.PointToEmu(2),
            DistanceFromRight = (UInt32Value)(uint)DocxUnitConverter.PointToEmu(3),
            DistanceFromTop = (UInt32Value)(uint)DocxUnitConverter.PointToEmu(4),
            DistanceFromBottom = (UInt32Value)(uint)DocxUnitConverter.PointToEmu(5)
        };

    private static OpenXmlElement CreatePolygonWrap(DocumentWrapMode mode, bool includePolygon)
    {
        var wrap = mode == DocumentWrapMode.Through
            ? (OpenXmlElement)new DW.WrapThrough { WrapText = DW.WrapTextValues.Left }
            : new DW.WrapTight { WrapText = DW.WrapTextValues.Left };
        if (includePolygon)
        {
            wrap.Append(CreateDiamondPolygon(DocxUnitConverter.PointToEmu(120), DocxUnitConverter.PointToEmu(80)));
        }

        return wrap;
    }

    private static DW.WrapPolygon CreateDiamondPolygon(long cx, long cy)
        => new(
            new DW.StartPoint { X = cx / 2, Y = 0L },
            new DW.LineTo { X = cx, Y = cy / 2 },
            new DW.LineTo { X = cx / 2, Y = cy },
            new DW.LineTo { X = 0L, Y = cy / 2 })
        {
            Edited = true
        };

    private static A.Graphic CreatePictureGraphic(string relationshipId, long cx, long cy)
        => new(new A.GraphicData(
            new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    new PIC.NonVisualDrawingProperties { Id = 2U, Name = "Native wrap", Description = "Native wrap" },
                    new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });

    private static DocumentLayoutRect Rect(double x, double y, double width, double height)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
}
