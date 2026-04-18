using System.Globalization;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>
/// Renders standalone SVG arrowheads for diagram edges.
/// Extracted from TmDiagramCanvas for testability.
/// </summary>
public static class DiagramArrowheadRenderer
{
    /// <summary>
    /// Returns how many pixels the edge line should be shortened at the given end
    /// so the arrowhead sits correctly without being hidden under the HTML node overlay.
    /// </summary>
    public static double GetArrowheadInset(DiagramEdge edge, bool isStart)
    {
        var arrow = isStart ? edge.StartArrow : edge.EndArrow;
        var def = DiagramArrowheadRegistry.Get(arrow);
        if (def is null || def.LinkInset <= 0) return 0;

        // Tip-based arrowheads: line goes all the way to the node border
        if (def.Anchor == ArrowheadAnchor.Tip) return 0;

        // Legacy special case: open/openThin on single-line edges keep the line at the node border
        if (edge.Shape != "link" && arrow is "open" or "openThin") return 0;

        var size = (isStart ? edge.StartArrowSize : edge.EndArrowSize) ?? 10;
        return def.LinkInset * size;
    }

    /// <summary>
    /// Renders an arrowhead as a standalone SVG &lt;path&gt; element.
    /// </summary>
    /// <param name="edge">The edge whose arrowhead to render.</param>
    /// <param name="isStart">true = start arrowhead, false = end arrowhead.</param>
    /// <param name="edgePoints">The polyline points of the edge (after waypoints / routing).</param>
    /// <param name="isFilled">Whether the arrowhead should be filled.</param>
    /// <param name="strokeWidth">Stroke width for line-style arrowheads.</param>
    /// <param name="color">Stroke/fill colour.</param>
    public static string RenderArrowhead(
        DiagramEdge edge,
        bool isStart,
        (double X, double Y)[] edgePoints,
        bool isFilled,
        double strokeWidth,
        string color)
    {
        var arrow = isStart ? edge.StartArrow : edge.EndArrow;
        if (string.IsNullOrEmpty(arrow) || arrow == "none") return "";
        var def = DiagramArrowheadRegistry.Get(arrow);
        if (def is null || def.FillMode == "none") return "";

        var pts = edgePoints;
        if (pts.Length < 2) return "";

        // Apply inset so the arrowhead isn't hidden under the HTML node overlay
        var inset = GetArrowheadInset(edge, isStart);
        if (inset > 0)
        {
            if (isStart)
            {
                double dx = pts[1].X - pts[0].X;
                double dy = pts[1].Y - pts[0].Y;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.001)
                {
                    var ratio = Math.Min(inset / len, 0.95);
                    pts[0] = (pts[0].X + dx * ratio, pts[0].Y + dy * ratio);
                }
            }
            else
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
        }

        var p = isStart ? pts[0] : pts[^1];
        var prev = isStart ? pts[1] : pts[^2];

        double dirX = p.X - prev.X;
        double dirY = p.Y - prev.Y;
        var dirLen = Math.Sqrt(dirX * dirX + dirY * dirY);
        if (dirLen > 0.001)
        {
            dirX /= dirLen;
            dirY /= dirLen;
        }

        double perpX = -dirY;
        double perpY = dirX;

        var size = (isStart ? edge.StartArrowSize : edge.EndArrowSize) ?? 10;

        // ── Open arrowheads ──
        if (arrow is "open" or "openThin")
        {
            var wingBase = size * 0.35;
            var tipDist = size * 0.9;

            var tipX = p.X;
            var tipY = p.Y;
            var baseCenterX = p.X - dirX * tipDist;
            var baseCenterY = p.Y - dirY * tipDist;

            if (edge.Shape == "link")
            {
                var offsetPts = OffsetPolyline(pts, 3);
                if (offsetPts.Length < 2) return "";

                var pMain = isStart ? pts[0] : pts[^1];
                var pOff = isStart ? offsetPts[0] : offsetPts[^1];

                double perpOffX = pOff.X - pMain.X;
                double perpOffY = pOff.Y - pMain.Y;
                var perpOffLen = Math.Sqrt(perpOffX * perpOffX + perpOffY * perpOffY);
                if (perpOffLen > 0.001)
                {
                    perpOffX /= perpOffLen;
                    perpOffY /= perpOffLen;
                }

                double dirOutX, dirOutY;
                if (isStart)
                {
                    dirOutX = pts[1].X - pts[0].X;
                    dirOutY = pts[1].Y - pts[0].Y;
                }
                else
                {
                    dirOutX = pts[^2].X - pts[^1].X;
                    dirOutY = pts[^2].Y - pts[^1].Y;
                }
                var dirOutLen = Math.Sqrt(dirOutX * dirOutX + dirOutY * dirOutY);
                if (dirOutLen > 0.001)
                {
                    dirOutX /= dirOutLen;
                    dirOutY /= dirOutLen;
                }

                var wingBaseLink = 5.0;
                var tipDistLink = size * 0.9;

                var mx = (pMain.X + pOff.X) / 2;
                var my = (pMain.Y + pOff.Y) / 2;
                var tX = mx - dirOutX * tipDistLink;
                var tY = my - dirOutY * tipDistLink;

                var bMainX = pMain.X + perpOffX * wingBaseLink;
                var bMainY = pMain.Y + perpOffY * wingBaseLink;
                var bOffX = pOff.X - perpOffX * wingBaseLink;
                var bOffY = pOff.Y - perpOffY * wingBaseLink;

                return $"<path class=\"tm-diagram-arrowhead\" d=\"M {F(pMain.X)} {F(pMain.Y)} L {F(bMainX)} {F(bMainY)} L {F(tX)} {F(tY)} " +
                       $"M {F(pOff.X)} {F(pOff.Y)} L {F(bOffX)} {F(bOffY)} L {F(tX)} {F(tY)}\" " +
                       $"fill=\"none\" stroke=\"{color}\" stroke-width=\"{F(strokeWidth)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />";
            }

            var baseLeftX = baseCenterX + perpX * wingBase;
            var baseLeftY = baseCenterY + perpY * wingBase;
            var baseRightX = baseCenterX - perpX * wingBase;
            var baseRightY = baseCenterY - perpY * wingBase;

            return $"<path class=\"tm-diagram-arrowhead\" d=\"M {F(baseLeftX)} {F(baseLeftY)} L {F(tipX)} {F(tipY)} L {F(baseRightX)} {F(baseRightY)}\" " +
                   $"fill=\"none\" stroke=\"{color}\" stroke-width=\"{F(strokeWidth)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />";
        }

        // ── Filled / other arrowheads ──
        double angleDeg = Math.Atan2(dirY, dirX) * 180 / Math.PI;
        var headScale = size / 10.0 * 1.3;

        double cx, cy;
        if (edge.Shape == "link")
        {
            var offsetPts = OffsetPolyline(pts, 3);
            var pM = isStart ? pts[0] : pts[^1];
            var pO = isStart ? offsetPts[0] : offsetPts[^1];
            cx = (pM.X + pO.X) / 2;
            cy = (pM.Y + pO.Y) / 2;
        }
        else
        {
            cx = p.X;
            cy = p.Y;
        }

        // Tip-based arrowheads place their tip at the line end.
        // All others place their base (or centre for symmetric) at the line end.
        double offsetX = def.Anchor switch
        {
            ArrowheadAnchor.Tip => -(def.RefX + (def.IsSymmetric ? def.Width / 2.0 - def.RefX : 0)),
            ArrowheadAnchor.Center => -def.Width / 2.0,
            _ => 0
        };

        var transform = $"translate({F(cx)},{F(cy)}) rotate({F(angleDeg)}) scale({F(headScale)}) translate({F(offsetX)},-{F(def.RefY)})";

        var fill = def.FillMode == "line" ? "none" : (isFilled ? color : "none");
        var sw = def.FillMode == "line" ? F(strokeWidth) : (isFilled ? "0" : F(strokeWidth));

        var sb = new System.Text.StringBuilder();
        sb.Append($"<path class=\"tm-diagram-arrowhead\" d=\"{def.PathData}\" fill=\"{fill}\" stroke=\"{color}\" stroke-width=\"{sw}\" stroke-linejoin=\"round\" transform=\"{transform}\" />");
        if (!string.IsNullOrEmpty(def.ExtraPath))
        {
            sb.Append($"<path class=\"tm-diagram-arrowhead\" d=\"{def.ExtraPath}\" fill=\"{fill}\" stroke=\"{color}\" stroke-width=\"{sw}\" stroke-linejoin=\"round\" transform=\"{transform}\" />");
        }
        return sb.ToString();
    }

    /// <summary>Offsets each point of a polyline perpendicular to its local direction.</summary>
    public static (double X, double Y)[] OffsetPolyline((double X, double Y)[] pts, double offset)
    {
        if (pts.Length < 2) return pts;
        var result = new (double X, double Y)[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            (double X, double Y) before = i > 0 ? pts[i - 1] : (pts[0].X * 2 - pts[1].X, pts[0].Y * 2 - pts[1].Y);
            (double X, double Y) after = i < pts.Length - 1 ? pts[i + 1] : (pts[^1].X * 2 - pts[^2].X, pts[^1].Y * 2 - pts[^2].Y);

            double dx = after.X - before.X;
            double dy = after.Y - before.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001)
            {
                result[i] = pts[i];
                continue;
            }
            double px = dy / len * offset;
            double py = -dx / len * offset;
            result[i] = (pts[i].X + px, pts[i].Y + py);
        }
        return result;
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
