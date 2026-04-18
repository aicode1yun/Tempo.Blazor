using FluentAssertions;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class DiagramArrowheadRendererTests
{
    // ── GetArrowheadInset tests ─────────────────────────────────────────────

    [Theory]
    [InlineData("classic", "link", 10, 9)]    // 0.9 * 10
    [InlineData("classic", "connector", 10, 9)]
    [InlineData("block", "link", 10, 9)]
    [InlineData("block", "connector", 10, 9)]
    [InlineData("classicThin", "link", 10, 7)]
    [InlineData("diamond", "link", 10, 13)]   // 1.3 * 10
    [InlineData("diamond", "connector", 10, 13)]
    [InlineData("box", "link", 10, 13)]
    [InlineData("oval", "link", 10, 13)]
    [InlineData("circle", "link", 10, 13)]
    [InlineData("cross", "link", 10, 10)]     // 1.0 * 10
    [InlineData("circlePlus", "link", 10, 13)]  // 1.3 * 10
    public void GetArrowheadInset_WithLinkInset_ReturnsScaledValue(string arrow, string shape, double size, double expected)
    {
        var edge = new DiagramEdge
        {
            EndArrow = arrow,
            EndArrowSize = size,
            Shape = shape
        };
        var actual = DiagramArrowheadRenderer.GetArrowheadInset(edge, isStart: false);
        actual.Should().BeApproximately(expected, 0.001);
    }

    [Theory]
    [InlineData("open", "connector")]
    [InlineData("openThin", "connector")]
    public void GetArrowheadInset_OpenOnConnector_ReturnsZero(string arrow, string shape)
    {
        var edge = new DiagramEdge
        {
            EndArrow = arrow,
            EndArrowSize = 10,
            Shape = shape
        };
        DiagramArrowheadRenderer.GetArrowheadInset(edge, isStart: false).Should().Be(0);
    }

    [Theory]
    [InlineData("async")]
    [InlineData("openAsync")]
    [InlineData("crow")]
    [InlineData("crowFilled")]
    [InlineData("ERone")]
    [InlineData("ERmany")]
    [InlineData("ERmandOne")]
    [InlineData("ERoneToMany")]
    [InlineData("ERzeroToOne")]
    [InlineData("ERzeroToMany")]
    [InlineData("one")]
    [InlineData("many")]
    [InlineData("zero-one")]
    [InlineData("zero-many")]
    [InlineData("open")]
    [InlineData("openThin")]
    public void GetArrowheadInset_ZeroLinkInset_ReturnsZero(string arrow)
    {
        var edge = new DiagramEdge
        {
            EndArrow = arrow,
            EndArrowSize = 10,
            Shape = "link"
        };
        DiagramArrowheadRenderer.GetArrowheadInset(edge, isStart: false).Should().Be(0);
    }

    [Fact]
    public void GetArrowheadInset_None_ReturnsZero()
    {
        var edge = new DiagramEdge { EndArrow = "none", Shape = "link" };
        DiagramArrowheadRenderer.GetArrowheadInset(edge, isStart: false).Should().Be(0);
    }

    // ── RenderArrowhead transform tests ─────────────────────────────────────

    /// <summary>
    /// Horizontal edge from (0,0) to (100,0). End arrowhead sits at the end point.
    /// </summary>
    private static (double X, double Y)[] HorizontalEdgePoints() => [(0, 0), (100, 0)];

    [Fact]
    public void RenderArrowhead_ClassicEnd_ContainsBaseAtShortenedLineEnd()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "classic",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        // LinkInset = 0.9 * 10 = 9, so line end is at x = 91
        // offsetX = 0 (base at line end), angle = 0° (pointing right)
        svg.Should().Contain("translate(91,0) rotate(0) scale(1.3) translate(0,-5)");
        svg.Should().Contain("d=\"M0,0 L0,10 L9,5 z\"");
    }

    [Fact]
    public void RenderArrowhead_BlockEnd_ContainsBaseAtShortenedLineEnd()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "block",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        // LinkInset = 0.9 * 10 = 9, line end at x = 91
        // offsetX = 0 (base at line end)
        svg.Should().Contain("translate(91,0) rotate(0) scale(1.3) translate(0,-5)");
        svg.Should().Contain("d=\"M0,0 L0,10 L10,5 z\"");
    }

    [Fact]
    public void RenderArrowhead_AsyncEnd_ContainsTipAtLineEnd()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "async",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        // LinkInset = 0, line end at x = 100
        // offsetX = -RefX = -10 (tip at line end)
        svg.Should().Contain("translate(100,0) rotate(0) scale(1.3) translate(-10,-5)");
        svg.Should().Contain("d=\"M0,0 L10,5 L0,10 M10,0 L10,10\"");
    }

    [Fact]
    public void RenderArrowhead_OpenAsyncEnd_ContainsTipAtLineEnd()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "openAsync",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        // LinkInset = 0, line end at x = 100
        // offsetX = -RefX = -8 (tip at line end)
        svg.Should().Contain("translate(100,0) rotate(0) scale(1.3) translate(-8,-5)");
    }

    [Fact]
    public void RenderArrowhead_CrowEnd_ContainsTipAtLineEnd()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "crow",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        // LinkInset = 0, line end at x = 100
        // offsetX = -RefX = -10 (tip at line end)
        svg.Should().Contain("translate(100,0) rotate(0) scale(1.3) translate(-10,-5)");
        svg.Should().Contain("d=\"M0,0 L10,5 L0,10 M8,0 L8,10\"");
    }

    [Fact]
    public void RenderArrowhead_DiamondEnd_ContainsBaseAtShortenedLineEnd()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "diamond",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: false, strokeWidth: 1.5, color: "#000");

        // LinkInset = 1.3 * 10 = 13, line end at x = 87
        // offsetX = 0 (base at line end — RefX is 0, left edge of diamond)
        svg.Should().Contain("translate(87,0) rotate(0) scale(1.3) translate(0,-5)");
        svg.Should().Contain("d=\"M0,5 L5,0 L10,5 L5,10 z\"");
    }

    [Fact]
    public void RenderArrowhead_OvalEnd_ContainsBaseAtShortenedLineEnd()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "oval",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: false, strokeWidth: 1.5, color: "#000");

        // LinkInset = 1.3 * 10 = 13, line end at x = 87
        // offsetX = 0 (base at line end — RefX is 0, left edge of oval)
        svg.Should().Contain("translate(87,0) rotate(0) scale(1.3) translate(0,-5)");
    }

    [Fact]
    public void RenderArrowhead_CrossEnd_IsCentered()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "cross",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        // LinkInset = 1.0 * 10 = 10, line end at x = 90
        // IsSymmetric = true => offsetX = -Width/2 = -5
        svg.Should().Contain("translate(90,0) rotate(0) scale(1.3) translate(-5,-5)");
    }

    [Fact]
    public void RenderArrowhead_HalfCircleEnd_IsCentered()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "halfCircle",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: false, strokeWidth: 1.5, color: "#000");

        // LinkInset = 1.3 * 10 = 13, line end at x = 87
        // IsSymmetric = true, RefX = 5 => offsetX = -Width/2 = -5
        svg.Should().Contain("translate(87,0) rotate(0) scale(1.3) translate(-5,-5)");
    }

    [Fact]
    public void RenderArrowhead_OpenEnd_DrawsWings()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "open",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        // LinkInset = 0 (special case for open on connector)
        // tip at (100,0), base wings at x = 100 - 9 = 91
        svg.Should().StartWith("<path class=\"tm-diagram-arrowhead\" d=\"M");
        // Should contain the tip coordinate (100,0)
        svg.Should().Contain("L 100 0");
        svg.Should().Contain("fill=\"none\"");
    }

    [Fact]
    public void RenderArrowhead_OpenEndOnLink_DrawsTwoWings()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "open",
            EndArrowSize = 10,
            Shape = "link",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        // Should have two M-L-L path sequences (one for each parallel line)
        var mCount = svg.Split('M').Length - 1;
        mCount.Should().Be(2);
        svg.Should().Contain("fill=\"none\"");
    }

    [Fact]
    public void RenderArrowhead_ClassicStart_PointsLeft()
    {
        var edge = new DiagramEdge
        {
            StartArrow = "classic",
            StartArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: true, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        // Start arrowhead: inset shortens pts[0] from (0,0) toward (100,0) by 9 => (9,0)
        // Direction from prev (100,0) toward p (9,0) is left => angle = 180°
        svg.Should().Contain("translate(9,0) rotate(180) scale(1.3) translate(0,-5)");
    }

    [Fact]
    public void RenderArrowhead_None_ReturnsEmpty()
    {
        var edge = new DiagramEdge { EndArrow = "none" };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1, color: "#000");
        svg.Should().BeEmpty();
    }

    [Fact]
    public void RenderArrowhead_FillModeLine_StrokeWidthPreserved()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "async",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 2.5, color: "#000");

        // FillMode = line => stroke-width should be preserved
        svg.Should().Contain("stroke-width=\"2.5\"");
    }

    [Fact]
    public void RenderArrowhead_FilledArrowhead_FillSetToColor()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "classic",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#ff0000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#ff0000");

        svg.Should().Contain("fill=\"#ff0000\"");
        svg.Should().Contain("stroke=\"#ff0000\"");
    }

    [Fact]
    public void RenderArrowhead_UnfilledArrowhead_FillSetToNone()
    {
        var edge = new DiagramEdge
        {
            EndArrow = "classic",
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#ff0000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: false, strokeWidth: 1.5, color: "#ff0000");

        svg.Should().Contain("fill=\"none\"");
    }

    // ── OffsetPolyline tests ────────────────────────────────────────────────

    [Fact]
    public void OffsetPolyline_HorizontalLine_OffsetsPerpendicular()
    {
        var pts = new (double X, double Y)[] { (0, 0), (10, 0), (20, 0) };
        var offset = DiagramArrowheadRenderer.OffsetPolyline(pts, 3);

        // Horizontal line -> perpendicular is vertical (clockwise rotation)
        // dy = 0, dx = 10 => px = 0, py = -1 => offset (0,-3)
        offset[0].X.Should().BeApproximately(0, 0.001);
        offset[0].Y.Should().BeApproximately(-3, 0.001);
        offset[1].X.Should().BeApproximately(10, 0.001);
        offset[1].Y.Should().BeApproximately(-3, 0.001);
        offset[2].X.Should().BeApproximately(20, 0.001);
        offset[2].Y.Should().BeApproximately(-3, 0.001);
    }

    [Fact]
    public void OffsetPolyline_VerticalLine_OffsetsPerpendicular()
    {
        var pts = new (double X, double Y)[] { (0, 0), (0, 10), (0, 20) };
        var offset = DiagramArrowheadRenderer.OffsetPolyline(pts, 3);

        // Vertical line -> perpendicular is horizontal (clockwise rotation)
        // dy = 10, dx = 0 => px = 1, py = 0 => offset (3,0)
        offset[0].X.Should().BeApproximately(3, 0.001);
        offset[0].Y.Should().BeApproximately(0, 0.001);
        offset[1].X.Should().BeApproximately(3, 0.001);
        offset[1].Y.Should().BeApproximately(10, 0.001);
        offset[2].X.Should().BeApproximately(3, 0.001);
        offset[2].Y.Should().BeApproximately(20, 0.001);
    }

    [Fact]
    public void OffsetPolyline_SinglePoint_ReturnsSame()
    {
        var pts = new (double X, double Y)[] { (5, 5) };
        var offset = DiagramArrowheadRenderer.OffsetPolyline(pts, 3);
        offset.Length.Should().Be(1);
        offset[0].Should().Be((5.0, 5.0));
    }

    // ── Exhaustive registry tests ───────────────────────────────────────────

    public static IEnumerable<object[]> AllArrowheadTypes()
    {
        foreach (var kv in DiagramArrowheadRegistry.Definitions)
        {
            if (kv.Key == "none") continue;
            yield return new object[] { kv.Key };
        }
    }

    [Theory]
    [MemberData(nameof(AllArrowheadTypes))]
    public void GetArrowheadInset_AllTypes_MatchesExpected(string arrowType)
    {
        var def = DiagramArrowheadRegistry.Get(arrowType);
        def.Should().NotBeNull($"{arrowType} should exist in registry");

        const double size = 10;
        var expectedInset = def!.LinkInset * size;

        var edgeConnector = new DiagramEdge
        {
            EndArrow = arrowType,
            EndArrowSize = size,
            Shape = "connector"
        };
        var actualConnector = DiagramArrowheadRenderer.GetArrowheadInset(edgeConnector, isStart: false);

        var edgeLink = new DiagramEdge
        {
            EndArrow = arrowType,
            EndArrowSize = size,
            Shape = "link"
        };
        var actualLink = DiagramArrowheadRenderer.GetArrowheadInset(edgeLink, isStart: false);

        if (def.Anchor == ArrowheadAnchor.Tip)
        {
            // Tip-based arrowheads: line goes to the node border, no inset
            actualConnector.Should().Be(0, $"{arrowType} on connector should have 0 inset (Tip anchor)");
            actualLink.Should().Be(0, $"{arrowType} on link should have 0 inset (Tip anchor)");
        }
        else
        {
            // Base / Center arrowheads: line is shortened by LinkInset
            actualConnector.Should().BeApproximately(expectedInset, 0.001, $"{arrowType} on connector");
            actualLink.Should().BeApproximately(expectedInset, 0.001, $"{arrowType} on link");
        }
    }

    [Theory]
    [MemberData(nameof(AllArrowheadTypes))]
    public void RenderArrowhead_AllTypes_ProducesCorrectTransform(string arrowType)
    {
        var def = DiagramArrowheadRegistry.Get(arrowType);
        def.Should().NotBeNull();

        var edge = new DiagramEdge
        {
            EndArrow = arrowType,
            EndArrowSize = 10,
            Shape = "connector",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        // open / openThin render as custom paths without a transform
        if (arrowType is "open" or "openThin")
        {
            svg.Should().StartWith("<path class=\"tm-diagram-arrowhead\"");
            svg.Should().Contain("fill=\"none\"");
            svg.Should().NotContain("transform=");
            return;
        }

        // All other types use a transform
        svg.Should().Contain("transform=\"translate(");

        // Extract the inner translate(offsetX, -RefY) from the transform chain
        var match = System.Text.RegularExpressions.Regex.Match(
            svg,
            @"translate\([^)]+\) rotate\([^)]+\) scale\([^)]+\) translate\(([^,]+),-([^)]+)\)");

        match.Success.Should().BeTrue($"{arrowType}: transform should contain inner translate(offsetX,-RefY)");
        var actualOffsetX = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var actualRefY = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);

        // RefY must match definition
        actualRefY.Should().BeApproximately(def!.RefY, 0.01, $"{arrowType}: RefY should match definition");

        // Expected offsetX depends on Anchor
        double expectedOffsetX = def.Anchor switch
        {
            ArrowheadAnchor.Tip => -(def.RefX + (def.IsSymmetric ? def.Width / 2.0 - def.RefX : 0)),
            ArrowheadAnchor.Center => -def.Width / 2.0,
            _ => 0
        };

        actualOffsetX.Should().BeApproximately(expectedOffsetX, 0.01,
            $"{arrowType}: offsetX should be {expectedOffsetX} (Anchor={def.Anchor}, RefX={def.RefX}, Width={def.Width})");
    }

    [Theory]
    [MemberData(nameof(AllArrowheadTypes))]
    public void RenderArrowhead_AllTypes_OnLink_ProducesCorrectTransform(string arrowType)
    {
        var def = DiagramArrowheadRegistry.Get(arrowType);
        def.Should().NotBeNull();

        var edge = new DiagramEdge
        {
            EndArrow = arrowType,
            EndArrowSize = 10,
            Shape = "link",
            Style = new DiagramStyle { Stroke = "#000" }
        };
        var svg = DiagramArrowheadRenderer.RenderArrowhead(edge, isStart: false, HorizontalEdgePoints(), isFilled: true, strokeWidth: 1.5, color: "#000");

        if (arrowType is "open" or "openThin")
        {
            // On link shape open arrowheads draw two wings
            svg.Should().StartWith("<path class=\"tm-diagram-arrowhead\"");
            var mCount = svg.Split('M').Length - 1;
            mCount.Should().Be(2, $"{arrowType} on link should draw two wings");
            return;
        }

        // All others use transform, but on link the placement point is centred between parallel lines
        svg.Should().Contain("transform=\"translate(");

        var match = System.Text.RegularExpressions.Regex.Match(
            svg,
            @"translate\(([^,]+),([^)]+)\) rotate\([^)]+\) scale\([^)]+\) translate\(([^,]+),-([^)]+)\)");
        match.Success.Should().BeTrue();

        var cx = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var cy = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);

        // Replicate the same point-shortening + offset logic that RenderArrowhead uses
        var pts = HorizontalEdgePoints();
        var inset = DiagramArrowheadRenderer.GetArrowheadInset(edge, isStart: false);
        if (inset > 0)
        {
            double dx = pts[^2].X - pts[^1].X;
            double dy = pts[^2].Y - pts[^1].Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 0.001)
            {
                var ratio = Math.Min(inset / len, 0.95);
                pts[^1] = (pts[^1].X + dx * ratio, pts[^1].Y + dy * ratio);
            }
        }
        var offsetPts = DiagramArrowheadRenderer.OffsetPolyline(pts, 3);
        var expectedCx = (pts[^1].X + offsetPts[^1].X) / 2;
        var expectedCy = (pts[^1].Y + offsetPts[^1].Y) / 2;

        cx.Should().BeApproximately(expectedCx, 0.5,
            $"{arrowType}: link placement x should be at shortened line end");
        cy.Should().BeApproximately(expectedCy, 0.5,
            $"{arrowType}: link placement y should be centred between parallel lines");
    }
}
