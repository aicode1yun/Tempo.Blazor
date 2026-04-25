using System.Globalization;
using System.Text;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Computes SVG paths, endpoint positions, and arrowhead geometry for wireframe connectors.
/// All methods are pure (no side effects) and can be unit-tested without a Blazor host.
/// </summary>
public static class WireframeConnectorRenderer
{
    // ── Endpoint computation ─────────────────────────────────────────────────

    /// <summary>
    /// Computes the best connection points on the boundaries of two elements.
    /// Chooses the side whose center is closest to the other element's center.
    /// </summary>
    public static (DiagramPoint Source, DiagramPoint Target) ComputeEndpoints(
        WireframeElement source, WireframeElement target)
    {
        var sCenter = new DiagramPoint(source.X + source.W / 2, source.Y + source.H / 2);
        var tCenter = new DiagramPoint(target.X + target.W / 2, target.Y + target.H / 2);

        var sSide = GetBestSide(sCenter, tCenter, source);
        var tSide = GetBestSide(tCenter, sCenter, target);

        var sPt = GetSidePoint(source, sSide, sCenter);
        var tPt = GetSidePoint(target, tSide, tCenter);

        return (sPt, tPt);
    }

    private static string GetBestSide(DiagramPoint from, DiagramPoint to, WireframeElement el)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;

        // Determine which side of 'el' faces towards 'to'
        // Prefer the axis with larger delta
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx >= 0 ? "right" : "left";
        }
        return dy >= 0 ? "bottom" : "top";
    }

    private static DiagramPoint GetSidePoint(WireframeElement el, string side, DiagramPoint center)
    {
        return side switch
        {
            "left"   => new DiagramPoint(el.X, center.Y),
            "right"  => new DiagramPoint(el.X + el.W, center.Y),
            "top"    => new DiagramPoint(center.X, el.Y),
            "bottom" => new DiagramPoint(center.X, el.Y + el.H),
            _        => center,
        };
    }

    // ── Path generation ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds an SVG path <c>d</c> attribute for the given connector.
    /// </summary>
    public static string BuildPath(WireframeConnector connector, WireframeElement? source, WireframeElement? target)
    {
        if (source is null || target is null)
            return string.Empty;

        var (sPt, tPt) = ComputeEndpoints(source, target);
        var waypoints = connector.Waypoints;

        return connector.Routing switch
        {
            "orthogonal" => BuildOrthogonalPath(sPt, tPt, waypoints),
            "curved"     => BuildCurvedPath(sPt, tPt, waypoints),
            _            => BuildStraightPath(sPt, tPt, waypoints),
        };
    }

    private static string BuildStraightPath(DiagramPoint s, DiagramPoint t, List<DiagramPoint> waypoints)
    {
        var sb = new StringBuilder();
        sb.Append(FmtM(s.X, s.Y));
        foreach (var w in waypoints)
            sb.Append(' ').Append(FmtL(w.X, w.Y));
        sb.Append(' ').Append(FmtL(t.X, t.Y));
        return sb.ToString();
    }

    private static string BuildOrthogonalPath(DiagramPoint s, DiagramPoint t, List<DiagramPoint> waypoints)
    {
        // If explicit waypoints exist, use them (e.g. from user drag)
        if (waypoints.Count > 0)
        {
            var sb = new StringBuilder();
            sb.Append(FmtM(s.X, s.Y));
            foreach (var w in waypoints)
                sb.Append(' ').Append(FmtL(w.X, w.Y));
            sb.Append(' ').Append(FmtL(t.X, t.Y));
            return sb.ToString();
        }

        // Auto-generate simple manhattan-style waypoints
        var pts = ComputeOrthogonalWaypoints(s, t);
        var sb2 = new StringBuilder();
        sb2.Append(FmtM(pts[0].X, pts[0].Y));
        for (int i = 1; i < pts.Count; i++)
            sb2.Append(' ').Append(FmtL(pts[i].X, pts[i].Y));
        return sb2.ToString();
    }

    internal static List<DiagramPoint> ComputeOrthogonalWaypoints(DiagramPoint s, DiagramPoint t)
    {
        double dx = t.X - s.X;
        double dy = t.Y - s.Y;
        var midX = s.X + dx / 2;
        var midY = s.Y + dy / 2;

        // Simple heuristic: one bend via midpoint
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            // Horizontal-first:  s → midX,s.y → midX,t.y → t
            return new List<DiagramPoint>
            {
                s,
                new DiagramPoint(midX, s.Y),
                new DiagramPoint(midX, t.Y),
                t,
            };
        }
        else
        {
            // Vertical-first: s → s.x,midY → t.x,midY → t
            return new List<DiagramPoint>
            {
                s,
                new DiagramPoint(s.X, midY),
                new DiagramPoint(t.X, midY),
                t,
            };
        }
    }

    private static string BuildCurvedPath(DiagramPoint s, DiagramPoint t, List<DiagramPoint> waypoints)
    {
        var sb = new StringBuilder();
        sb.Append(FmtM(s.X, s.Y));

        if (waypoints.Count > 0)
        {
            // Use waypoints as control points for a cubic bezier chain
            // Simple approach: quadratic bezier through each waypoint
            var all = new List<DiagramPoint> { s };
            all.AddRange(waypoints);
            all.Add(t);

            for (int i = 1; i < all.Count; i++)
            {
                var prev = all[i - 1];
                var cur = all[i];
                var mx = (prev.X + cur.X) / 2;
                var my = (prev.Y + cur.Y) / 2;
                sb.Append(' ').Append(FmtQ(mx, my, cur.X, cur.Y));
            }
        }
        else
        {
            // Auto cubic bezier
            double dx = t.X - s.X;
            double dy = t.Y - s.Y;
            var c1 = new DiagramPoint(s.X + dx * 0.5, s.Y);
            var c2 = new DiagramPoint(t.X - dx * 0.5, t.Y);
            sb.Append(' ').Append(FmtC(c1.X, c1.Y, c2.X, c2.Y, t.X, t.Y));
        }

        return sb.ToString();
    }

    // ── Label position ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns a point approximately halfway along the path for label placement.
    /// </summary>
    public static DiagramPoint GetLabelPosition(WireframeConnector connector, WireframeElement? source, WireframeElement? target)
    {
        if (source is null || target is null)
            return new DiagramPoint(0, 0);

        var (s, t) = ComputeEndpoints(source, target);
        var waypoints = connector.Waypoints;

        // Simple midpoint between start and end (ignoring waypoints for label position)
        return new DiagramPoint((s.X + t.X) / 2, (s.Y + t.Y) / 2);
    }

    // ── Arrowhead geometry ───────────────────────────────────────────────────

    /// <summary>
    /// Returns SVG <c>marker-end</c> / <c>marker-start</c> attribute value for a registered arrowhead.
    /// Callers should embed the referenced marker definitions in SVG &lt;defs&gt;.
    /// </summary>
    public static string? GetArrowMarkerRef(string arrowType, bool isStart, string connectorId)
    {
        if (arrowType == "none")
            return null;
        var prefix = isStart ? "start" : "end";
        return $"url(#tm-wd-arrow-{prefix}-{arrowType}-{connectorId})";
    }

    /// <summary>
    /// Generates SVG marker definitions for start and end arrowheads.
    /// </summary>
    public static string BuildArrowMarkers(WireframeConnector connector)
    {
        var sb = new StringBuilder();
        if (connector.StartArrow != "none")
            sb.Append(BuildSingleMarker(connector.StartArrow, true, connector.Id, connector.Stroke));
        if (connector.EndArrow != "none")
            sb.Append(BuildSingleMarker(connector.EndArrow, false, connector.Id, connector.Stroke));
        return sb.ToString();
    }

    private static string BuildSingleMarker(string arrowType, bool isStart, string connectorId, string stroke)
    {
        var prefix = isStart ? "start" : "end";
        var id = $"tm-wd-arrow-{prefix}-{arrowType}-{connectorId}";
        var orient = isStart ? "auto-start-reverse" : "auto";

        var (path, viewBox, refX) = arrowType switch
        {
            "classic" => ("M 0 0 L 10 5 L 0 10 L 2 5 Z", "0 0 10 10", 10),
            "block"   => ("M 0 0 L 10 5 L 0 10 Z", "0 0 10 10", 10),
            "open"    => ("M 0 0 L 10 5 L 0 10", "0 0 10 10", 8),
            "oval"    => ("M 5 0 A 5 5 0 1 1 5 10 A 5 5 0 1 1 5 0", "0 0 10 10", 10),
            "diamond" => ("M 5 0 L 10 5 L 5 10 L 0 5 Z", "0 0 10 10", 10),
            _         => ("M 0 0 L 10 5 L 0 10 L 2 5 Z", "0 0 10 10", 10),
        };

        var fill = arrowType == "open" ? "none" : stroke;

        return $"""<marker id="{id}" viewBox="{viewBox}" refX="{refX.ToString(CultureInfo.InvariantCulture)}" refY="5" markerWidth="8" markerHeight="8" orient="{orient}" markerUnits="userSpaceOnUse"><path d="{path}" fill="{fill}" stroke="{stroke}" stroke-width="1"/></marker>""";
    }

    // ── Formatting helpers ───────────────────────────────────────────────────

    private static string FmtM(double x, double y)
        => $"M {F(x)} {F(y)}";

    private static string FmtL(double x, double y)
        => $"L {F(x)} {F(y)}";

    private static string FmtQ(double cx, double cy, double x, double y)
        => $"Q {F(cx)} {F(cy)}, {F(x)} {F(y)}";

    private static string FmtC(double x1, double y1, double x2, double y2, double x, double y)
        => $"C {F(x1)} {F(y1)}, {F(x2)} {F(y2)}, {F(x)} {F(y)}";

    private static string F(double v)
        => v.ToString("0.##", CultureInfo.InvariantCulture);
}
