using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentLayoutGeometryTests
{
    [Fact]
    public void DebugSnapshot_FromSnapshot_SerializesLayoutCountsAndPages()
    {
        var snapshot = new DocumentPageLayoutSnapshot
        {
            DocumentId = "doc-layout",
            Diagnostics = ["layout ok"],
            Pages =
            [
                new DocumentPageLayoutBox
                {
                    PageIndex = 0,
                    PageNumber = 1,
                    PageRect = Rect(0, 0, 600, 800),
                    BodyRect = Rect(72, 72, 456, 656),
                    Paragraphs =
                    [
                        new DocumentParagraphLayoutBox
                        {
                            BlockId = "p1",
                            Lines =
                            [
                                new DocumentLineBox
                                {
                                    BlockId = "p1",
                                    Segments =
                                    [
                                        new DocumentTextSegmentBox
                                        {
                                            BlockId = "p1",
                                            Text = "Hello"
                                        }
                                    ]
                                }
                            ]
                        }
                    ],
                    Objects =
                    [
                        new DocumentObjectLayoutBox
                        {
                            Id = "img1",
                            BlockId = "img-block",
                            ObjectRect = Rect(72, 120, 120, 80)
                        }
                    ],
                    Exclusions =
                    [
                        new DocumentExclusionZone
                        {
                            ObjectId = "img1",
                            BlockId = "img-block",
                            WrapMode = DocumentWrapMode.Square,
                            Rect = Rect(72, 120, 128, 88)
                        }
                    ]
                }
            ]
        };

        var debug = DocumentPageLayoutDebugSnapshot.FromSnapshot(snapshot);
        var json = JsonSerializer.Serialize(debug);
        var restored = JsonSerializer.Deserialize<DocumentPageLayoutDebugSnapshot>(json);

        restored!.DocumentId.Should().Be("doc-layout");
        restored.PageCount.Should().Be(1);
        restored.ParagraphCount.Should().Be(1);
        restored.LineCount.Should().Be(1);
        restored.SegmentCount.Should().Be(1);
        restored.ObjectCount.Should().Be(1);
        restored.ExclusionCount.Should().Be(1);
        restored.Pages.Should().ContainSingle();
        restored.Diagnostics.Should().ContainSingle().Which.Should().Be("layout ok");
    }

    [Fact]
    public void Intersection_ReturnsOverlapAndIntersectsDetectsPositiveAreaOnly()
    {
        var a = Rect(10, 10, 100, 80);
        var b = Rect(60, 40, 100, 80);
        var touching = Rect(110, 10, 20, 20);

        DocumentLayoutGeometryHelper.Intersects(a, b).Should().BeTrue();
        DocumentLayoutGeometryHelper.Intersects(a, touching).Should().BeFalse();

        var intersection = DocumentLayoutGeometryHelper.Intersection(a, b);
        intersection.X.Should().Be(60);
        intersection.Y.Should().Be(40);
        intersection.Width.Should().Be(50);
        intersection.Height.Should().Be(50);
    }

    [Fact]
    public void Union_ReturnsBoundingRectangleForAllRects()
    {
        var union = DocumentLayoutGeometryHelper.Union(
        [
            Rect(20, 30, 40, 50),
            Rect(10, 70, 30, 20),
            Rect(80, 10, 20, 10)
        ]);

        union.X.Should().Be(10);
        union.Y.Should().Be(10);
        union.Width.Should().Be(90);
        union.Height.Should().Be(80);
    }

    [Fact]
    public void ClampToBody_PreservesSizeAndMovesOriginInsideBody()
    {
        var clamped = DocumentLayoutGeometryHelper.ClampToBody(
            Rect(10, 20, 120, 80),
            Rect(72, 72, 300, 300));

        clamped.X.Should().Be(72);
        clamped.Y.Should().Be(72);
        clamped.Width.Should().Be(120);
        clamped.Height.Should().Be(80);
    }

    [Fact]
    public void ResolveObjectRect_UsesRelativeReferenceAlignmentAndTransformSize()
    {
        var layout = AnchoredLayout(DocumentWrapMode.Square, width: 100, height: 60);
        layout.Position.HorizontalRelativeTo = DocumentRelativePosition.Margin;
        layout.Position.VerticalRelativeTo = DocumentRelativePosition.Paragraph;
        layout.Position.HorizontalAlignment = DocumentImageHorizontalPosition.Center;
        layout.Position.VerticalAlignment = DocumentObjectVerticalAlignment.Bottom;
        layout.Position.X = 10;
        layout.Position.Y = -5;

        var rect = DocumentLayoutGeometryHelper.ResolveObjectRect(
            layout,
            pageRect: Rect(0, 0, 600, 800),
            bodyRect: Rect(72, 72, 456, 656),
            paragraphRect: Rect(100, 200, 300, 120));

        rect.X.Should().Be(72 + ((456 - 100) / 2) + 10);
        rect.Y.Should().Be(200 + 120 - 60 - 5);
        rect.Width.Should().Be(100);
        rect.Height.Should().Be(60);
    }

    [Fact]
    public void ComputeWrapRect_ExpandsObjectByDistances()
    {
        var wrap = new DocumentObjectWrap
        {
            DistanceLeft = 4,
            DistanceTop = 6,
            DistanceRight = 8,
            DistanceBottom = 10
        };

        var rect = DocumentLayoutGeometryHelper.ComputeWrapRect(Rect(100, 120, 50, 40), wrap);

        rect.X.Should().Be(96);
        rect.Y.Should().Be(114);
        rect.Width.Should().Be(62);
        rect.Height.Should().Be(56);
    }

    [Fact]
    public void OrderByZIndex_SortsByPageThenZIndexAndKeepsStableOrderForEqualZ()
    {
        var first = ObjectBox("first", Rect(0, 0, 10, 10), DocumentWrapMode.Square, zIndex: 2, pageIndex: 0);
        var second = ObjectBox("second", Rect(0, 0, 10, 10), DocumentWrapMode.Square, zIndex: 1, pageIndex: 0);
        var third = ObjectBox("third", Rect(0, 0, 10, 10), DocumentWrapMode.Square, zIndex: 1, pageIndex: 0);
        var nextPage = ObjectBox("next", Rect(0, 0, 10, 10), DocumentWrapMode.Square, zIndex: -5, pageIndex: 1);

        var ordered = DocumentLayoutGeometryHelper.OrderByZIndex([first, second, third, nextPage]);

        ordered.Select(box => box.Id).Should().Equal("second", "third", "first", "next");
    }

    [Fact]
    public void CreateExclusionZone_SquareLeft_BlocksLeftObjectWrapRectangle()
    {
        var body = Rect(72, 72, 456, 656);
        var box = ObjectBox("left-image", Rect(72, 120, 120, 80), DocumentWrapMode.Square);

        var zone = DocumentLayoutGeometryHelper.CreateExclusionZone(box, body);

        zone.Should().NotBeNull();
        zone!.WrapMode.Should().Be(DocumentWrapMode.Square);
        zone.Rect.X.Should().Be(72);
        zone.Rect.Right.Should().Be(192);
        zone.Rect.Y.Should().Be(120);
        zone.Rect.Bottom.Should().Be(200);
    }

    [Fact]
    public void CreateExclusionZone_SquareRight_BlocksRightObjectWrapRectangle()
    {
        var body = Rect(72, 72, 456, 656);
        var box = ObjectBox("right-image", Rect(408, 120, 120, 80), DocumentWrapMode.Square);

        var zone = DocumentLayoutGeometryHelper.CreateExclusionZone(box, body);

        zone.Should().NotBeNull();
        zone!.Rect.X.Should().Be(408);
        zone.Rect.Right.Should().Be(528);
    }

    [Fact]
    public void CreateExclusionZone_TopBottom_BlocksFullLineWidthAcrossObjectHeight()
    {
        var body = Rect(72, 72, 456, 656);
        var box = ObjectBox("top-bottom-image", Rect(250, 120, 120, 80), DocumentWrapMode.TopBottom);

        var zone = DocumentLayoutGeometryHelper.CreateExclusionZone(box, body);

        zone.Should().NotBeNull();
        zone!.Rect.X.Should().Be(72);
        zone.Rect.Right.Should().Be(528);
        zone.Rect.Y.Should().Be(120);
        zone.Rect.Bottom.Should().Be(200);
    }

    [Theory]
    [InlineData(DocumentWrapMode.BehindText)]
    [InlineData(DocumentWrapMode.InFrontOfText)]
    public void CreateExclusionZone_OverlayModes_DoNotBlockText(DocumentWrapMode mode)
    {
        var zone = DocumentLayoutGeometryHelper.CreateExclusionZone(
            ObjectBox("overlay-image", Rect(100, 120, 120, 80), mode),
            Rect(72, 72, 456, 656));

        zone.Should().BeNull();
    }

    [Theory]
    [InlineData(DocumentWrapMode.Tight)]
    [InlineData(DocumentWrapMode.Through)]
    public void CreateExclusionZone_TightAndThrough_UsePolygonContour(DocumentWrapMode mode)
    {
        var box = ObjectBox("contour-image", Rect(100, 120, 120, 80), mode);
        box.Layout.Wrap.WrapContourPoints =
        [
            new() { X = 0.5, Y = 0 },
            new() { X = 1, Y = 0.5 },
            new() { X = 0.5, Y = 1 },
            new() { X = 0, Y = 0.5 }
        ];

        var zone = DocumentLayoutGeometryHelper.CreateExclusionZone(
            box,
            Rect(72, 72, 456, 656));

        zone.Should().NotBeNull();
        zone!.IsContourPlaceholder.Should().BeFalse();
        zone.Polygon.Should().HaveCount(4);
        zone.Rect.X.Should().Be(100);
        zone.Rect.Width.Should().Be(120);
    }

    [Fact]
    public void NormalizeWrapContourPoints_UsesDefaultRectangleWhenTooFewPoints()
    {
        var points = DocumentLayoutGeometryHelper.NormalizeWrapContourPoints(
        [
            new DocumentObjectWrapPoint { X = 0.2, Y = 0.2 },
            new DocumentObjectWrapPoint { X = 0.8, Y = 0.8 }
        ]);

        points.Should().HaveCount(4);
        points.Select(point => (point.X, point.Y)).Should().Equal((0, 0), (1, 0), (1, 1), (0, 1));
    }

    [Fact]
    public void NormalizeWrapContourPoints_ClampsPointsIntoNormalizedRange()
    {
        var points = DocumentLayoutGeometryHelper.NormalizeWrapContourPoints(
        [
            new DocumentObjectWrapPoint { X = -1, Y = 0.25 },
            new DocumentObjectWrapPoint { X = 0.5, Y = 2 },
            new DocumentObjectWrapPoint { X = 1.5, Y = -2 }
        ]);

        points.Select(point => (point.X, point.Y)).Should().Equal((0, 0.25), (0.5, 1), (1, 0));
    }

    [Fact]
    public void GetAvailableLineIntervals_ReturnsFullWidthWhenNoExclusionOverlaps()
    {
        var body = Rect(72, 72, 456, 656);

        var intervals = DocumentLayoutGeometryHelper.GetAvailableLineIntervals(300, 20, [], body);

        intervals.Should().ContainSingle();
        intervals[0].X.Should().Be(72);
        intervals[0].Width.Should().Be(456);
    }

    [Fact]
    public void GetAvailableLineIntervals_LeftSquare_ReturnsRightInterval()
    {
        var body = Rect(72, 72, 456, 656);
        var exclusions = Zones(ObjectBox("left-image", Rect(72, 120, 120, 80), DocumentWrapMode.Square), body);

        var intervals = DocumentLayoutGeometryHelper.GetAvailableLineIntervals(140, 20, exclusions, body);

        intervals.Should().ContainSingle();
        intervals[0].X.Should().Be(192);
        intervals[0].Width.Should().Be(336);
    }

    [Fact]
    public void GetAvailableLineIntervals_RightSquare_ReturnsLeftInterval()
    {
        var body = Rect(72, 72, 456, 656);
        var exclusions = Zones(ObjectBox("right-image", Rect(408, 120, 120, 80), DocumentWrapMode.Square), body);

        var intervals = DocumentLayoutGeometryHelper.GetAvailableLineIntervals(140, 20, exclusions, body);

        intervals.Should().ContainSingle();
        intervals[0].X.Should().Be(72);
        intervals[0].Width.Should().Be(336);
    }

    [Fact]
    public void GetAvailableLineIntervals_CenteredSquare_ReturnsTwoIntervals()
    {
        var body = Rect(72, 72, 456, 656);
        var exclusions = Zones(ObjectBox("middle-image", Rect(240, 120, 120, 80), DocumentWrapMode.Square), body);

        var intervals = DocumentLayoutGeometryHelper.GetAvailableLineIntervals(140, 20, exclusions, body);

        intervals.Should().HaveCount(2);
        intervals[0].X.Should().Be(72);
        intervals[0].Width.Should().Be(168);
        intervals[1].X.Should().Be(360);
        intervals[1].Width.Should().Be(168);
    }

    [Fact]
    public void GetAvailableLineIntervals_TopBottomFullWidth_ReturnsEmptyIntervals()
    {
        var body = Rect(72, 72, 456, 656);
        var exclusions = Zones(ObjectBox("top-bottom-image", Rect(240, 120, 120, 80), DocumentWrapMode.TopBottom), body);

        var intervals = DocumentLayoutGeometryHelper.GetAvailableLineIntervals(140, 20, exclusions, body);

        intervals.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailableLineIntervals_TightDiamond_UsesPolygonInsteadOfSquareBounds()
    {
        var body = Rect(0, 0, 300, 240);
        var box = ObjectBox("diamond-image", Rect(100, 80, 100, 100), DocumentWrapMode.Tight);
        box.Layout.Wrap.WrapContourPoints =
        [
            new() { X = 0.5, Y = 0 },
            new() { X = 1, Y = 0.5 },
            new() { X = 0.5, Y = 1 },
            new() { X = 0, Y = 0.5 }
        ];
        var exclusions = Zones(box, body);

        var intervals = DocumentLayoutGeometryHelper.GetAvailableLineIntervals(88, 12, exclusions, body);

        intervals.Should().HaveCount(2);
        intervals[0].End.Should().BeGreaterThan(100);
        intervals[1].X.Should().BeLessThan(200);
    }

    [Fact]
    public void GetAvailableLineIntervals_ThroughIrregularPolygon_UsesIrregularContour()
    {
        var body = Rect(0, 0, 320, 240);
        var box = ObjectBox("irregular-image", Rect(100, 80, 120, 100), DocumentWrapMode.Through);
        box.Layout.Wrap.WrapContourPoints =
        [
            new() { X = 0.1, Y = 0 },
            new() { X = 1, Y = 0.15 },
            new() { X = 0.78, Y = 0.55 },
            new() { X = 1, Y = 1 },
            new() { X = 0, Y = 0.88 },
            new() { X = 0.22, Y = 0.45 }
        ];
        var exclusions = Zones(box, body);

        var intervals = DocumentLayoutGeometryHelper.GetAvailableLineIntervals(125, 14, exclusions, body);

        intervals.Should().HaveCount(2);
        intervals[0].X.Should().Be(0);
        intervals[0].Width.Should().BeGreaterThan(100);
        intervals[1].End.Should().Be(320);
    }

    [Fact]
    public void BuildExclusionZones_IgnoresBehindAndInFrontObjects()
    {
        var body = Rect(72, 72, 456, 656);
        var zones = DocumentLayoutGeometryHelper.BuildExclusionZones(
        [
            ObjectBox("behind", Rect(100, 120, 120, 80), DocumentWrapMode.BehindText),
            ObjectBox("front", Rect(100, 220, 120, 80), DocumentWrapMode.InFrontOfText),
            ObjectBox("square", Rect(100, 320, 120, 80), DocumentWrapMode.Square)
        ], body);

        zones.Should().ContainSingle();
        zones[0].ObjectId.Should().Be("square");
    }

    private static IReadOnlyList<DocumentExclusionZone> Zones(DocumentObjectLayoutBox box, DocumentLayoutRect body)
        => DocumentLayoutGeometryHelper.BuildExclusionZones([box], body);

    private static DocumentObjectLayoutBox ObjectBox(
        string id,
        DocumentLayoutRect rect,
        DocumentWrapMode mode,
        int zIndex = 0,
        int pageIndex = 0)
    {
        var layout = AnchoredLayout(mode, rect.Width, rect.Height);
        layout.Stacking.ZIndex = zIndex;
        return new DocumentObjectLayoutBox
        {
            Id = id,
            BlockId = id,
            PageIndex = pageIndex,
            ObjectRect = rect,
            WrapRect = DocumentLayoutGeometryHelper.ComputeWrapRect(rect, layout.Wrap),
            Layout = layout,
            ZIndex = zIndex
        };
    }

    private static DocumentObjectLayout AnchoredLayout(DocumentWrapMode mode, double width, double height)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Wrap = new DocumentObjectWrap
            {
                Mode = mode
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height
            }
        };

    private static DocumentLayoutRect Rect(double x, double y, double width, double height)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
}
