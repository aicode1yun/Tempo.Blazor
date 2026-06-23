using System;
using System.Collections.Generic;
using System.Linq;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>Static geometry helpers used by the diagram canvas and commands.</summary>
public static class DiagramGeometryHelper
{
    public static (double X, double Y) ComputePortPosition(DiagramNode node, DiagramPort port)
    {
        if (port.MagnetStrategy == "perimeter")
        {
            var (px, py) = port.Side switch
            {
                PortSide.Left => (0.0, node.H * port.Offset),
                PortSide.Right => (node.W, node.H * port.Offset),
                PortSide.Top => (node.W * port.Offset, 0.0),
                PortSide.Bottom => (node.W * port.Offset, node.H),
                _ => (node.W * port.Offset, node.H * port.Offset)
            };
            return (node.X + px, node.Y + py);
        }

        var x = port.Side switch
        {
            PortSide.Left => 0,
            PortSide.Right => node.W,
            _ => node.W * port.Offset,
        };
        var y = port.Side switch
        {
            PortSide.Top => 0,
            PortSide.Bottom => node.H,
            _ => node.H * port.Offset,
        };
        return (node.X + x, node.Y + y);
    }

    public static PortSide InferSideFromConstraint(double cx, double cy)
    {
        // Heuristic: determine closest side from relative constraint (0-1)
        if (cx <= 0.25) return PortSide.Left;
        if (cx >= 0.75) return PortSide.Right;
        if (cy <= 0.25) return PortSide.Top;
        if (cy >= 0.75) return PortSide.Bottom;
        // Default to Right when constraint is near center
        return PortSide.Right;
    }

    public static (double X, double Y)[] GetEdgePoints(DiagramDocument doc, DiagramEdge edge)
    {
        if (doc is null) return [];

        (double X, double Y) p1;
        PortSide sSide;

        if (!string.IsNullOrEmpty(edge.SourceEdgeId))
        {
            var srcEdge = doc.Edges.FirstOrDefault(e => e.Id == edge.SourceEdgeId);
            if (srcEdge is null) return [];
            p1 = ComputeEdgePointAtT(doc, srcEdge, edge.SourceEdgeT ?? 0.5);
            sSide = PortSide.Right;
        }
        else if (!string.IsNullOrEmpty(edge.SourceNodeId))
        {
            var srcNode = doc.Nodes.FirstOrDefault(n => n.Id == edge.SourceNodeId);
            if (srcNode is null) return [];

            if (edge.SourceConstraint is not null)
            {
                var c = edge.SourceConstraint;
                if (c.Perimeter)
                {
                    var (px, py) = ProjectToPerimeter(srcNode.W, srcNode.H, c.RelativeX, c.RelativeY, srcNode.BackgroundShape);
                    p1 = (srcNode.X + px + c.Dx, srcNode.Y + py + c.Dy);
                }
                else
                {
                    p1 = (srcNode.X + srcNode.W * c.RelativeX + c.Dx, srcNode.Y + srcNode.H * c.RelativeY + c.Dy);
                }
                sSide = InferSideFromConstraint(c.RelativeX, c.RelativeY);
            }
            else
            {
                var srcPort = edge.SourcePortId is not null
                    ? srcNode.Ports.FirstOrDefault(p => p.Id == edge.SourcePortId)
                    : srcNode.Ports.FirstOrDefault();
                var sPort = srcPort ?? new DiagramPort { Side = PortSide.Right, Offset = 0.5 };
                p1 = ComputePortPosition(srcNode, sPort);
                sSide = sPort.Side;
            }
            p1 = ApplyPortSpacing(p1, sSide, edge.SourceSpacing ?? 0);
        }
        else if (edge.SourcePoint is not null)
        {
            // Dangling source end (absolute point in document coordinates)
            p1 = (edge.SourcePoint.X, edge.SourcePoint.Y);
            sSide = PortSide.Right; // default direction; spacing ignored for dangling ends
        }
        else
        {
            return [];
        }

        (double X, double Y) p2;
        PortSide tSide;

        if (!string.IsNullOrEmpty(edge.TargetEdgeId))
        {
            var tgtEdge = doc.Edges.FirstOrDefault(e => e.Id == edge.TargetEdgeId);
            if (tgtEdge is null) return [];
            p2 = ComputeEdgePointAtT(doc, tgtEdge, edge.TargetEdgeT ?? 0.5);
            tSide = PortSide.Left;
        }
        else if (!string.IsNullOrEmpty(edge.TargetNodeId))
        {
            var tgtNode = doc.Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
            if (tgtNode is null) return [];

            if (edge.TargetConstraint is not null)
            {
                var c = edge.TargetConstraint;
                if (c.Perimeter)
                {
                    var (px, py) = ProjectToPerimeter(tgtNode.W, tgtNode.H, c.RelativeX, c.RelativeY, tgtNode.BackgroundShape);
                    p2 = (tgtNode.X + px + c.Dx, tgtNode.Y + py + c.Dy);
                }
                else
                {
                    p2 = (tgtNode.X + tgtNode.W * c.RelativeX + c.Dx, tgtNode.Y + tgtNode.H * c.RelativeY + c.Dy);
                }
                tSide = InferSideFromConstraint(c.RelativeX, c.RelativeY);
            }
            else
            {
                var tgtPort = edge.TargetPortId is not null
                    ? tgtNode.Ports.FirstOrDefault(p => p.Id == edge.TargetPortId)
                    : tgtNode.Ports.FirstOrDefault();
                var tPort = tgtPort ?? new DiagramPort { Side = PortSide.Left, Offset = 0.5 };
                p2 = ComputePortPosition(tgtNode, tPort);
                tSide = tPort.Side;
            }
            p2 = ApplyPortSpacing(p2, tSide, edge.TargetSpacing ?? 0);
        }
        else if (edge.TargetPoint is not null)
        {
            // Dangling target end (absolute point in document coordinates)
            p2 = (edge.TargetPoint.X, edge.TargetPoint.Y);
            tSide = PortSide.Left; // default direction; spacing ignored for dangling ends
        }
        else
        {
            return [];
        }

        var pts = new List<(double X, double Y)> { p1 };
        foreach (var wp in edge.Waypoints)
            pts.Add((wp.X, wp.Y));
        pts.Add(p2);
        return pts.ToArray();
    }

    public static (double X, double Y) ComputeEdgePointAtT(DiagramDocument doc, DiagramEdge edge, double t)
    {
        var pts = GetEdgePoints(doc, edge);
        if (pts.Length < 2) return (0, 0);

        double totalLen = 0;
        var segs = new List<(int Idx, double Len)>();
        for (int i = 0; i < pts.Length - 1; i++)
        {
            var dx = pts[i + 1].X - pts[i].X;
            var dy = pts[i + 1].Y - pts[i].Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            segs.Add((i, len));
            totalLen += len;
        }

        if (totalLen == 0) return pts[0];

        var target = totalLen * Math.Clamp(t, 0, 1);
        double accum = 0;
        foreach (var (idx, len) in segs)
        {
            if (accum + len >= target)
            {
                var segT = (target - accum) / len;
                var x = pts[idx].X + segT * (pts[idx + 1].X - pts[idx].X);
                var y = pts[idx].Y + segT * (pts[idx + 1].Y - pts[idx].Y);
                return (x, y);
            }
            accum += len;
        }
        return pts[^1];
    }

    public static double FindClosestT(DiagramDocument doc, DiagramEdge edge, double targetX, double targetY, int samples = 100)
    {
        var pts = GetEdgePoints(doc, edge);
        if (pts.Length < 2) return 0.5;

        double totalLen = 0;
        var segs = new List<(int Idx, double Len)>();
        for (int i = 0; i < pts.Length - 1; i++)
        {
            var dx = pts[i + 1].X - pts[i].X;
            var dy = pts[i + 1].Y - pts[i].Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            segs.Add((i, len));
            totalLen += len;
        }

        if (totalLen == 0) return 0;

        double bestT = 0;
        double bestDist = double.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            double t = i / (double)samples;
            double targetDist = totalLen * t;
            double accum = 0;
            double sx = 0, sy = 0;
            bool found = false;
            foreach (var (idx, len) in segs)
            {
                if (accum + len >= targetDist)
                {
                    double segT = (targetDist - accum) / len;
                    sx = pts[idx].X + segT * (pts[idx + 1].X - pts[idx].X);
                    sy = pts[idx].Y + segT * (pts[idx + 1].Y - pts[idx].Y);
                    found = true;
                    break;
                }
                accum += len;
            }
            if (!found)
            {
                sx = pts[^1].X;
                sy = pts[^1].Y;
            }
            double dx = sx - targetX;
            double dy = sy - targetY;
            double d = dx * dx + dy * dy;
            if (d < bestDist)
            {
                bestDist = d;
                bestT = t;
            }
        }

        return bestT;
    }

    /// <summary>Determines whether three points are collinear within the given tolerance (in pixels).</summary>
    public static bool IsCollinear((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, double tolerance = 5.0)
    {
        var dx1 = b.X - a.X;
        var dy1 = b.Y - a.Y;
        var dx2 = c.X - b.X;
        var dy2 = c.Y - b.Y;

        if (Math.Abs(dx1) < 0.001 && Math.Abs(dx2) < 0.001) return true; // vertical
        if (Math.Abs(dy1) < 0.001 && Math.Abs(dy2) < 0.001) return true; // horizontal

        var cross = dx1 * dy2 - dy1 * dx2;
        if (Math.Abs(cross) < 0.001) return true; // same slope

        var area = Math.Abs(dx1 * (c.Y - a.Y) - dy1 * (c.X - a.X));
        var baseLen = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
        var dist = baseLen > 0.001 ? area / baseLen : 0;
        return dist <= tolerance;
    }

    /// <summary>Projects a relative constraint point onto the perimeter of a node shape.</summary>
    public static (double X, double Y) ProjectToPerimeter(double w, double h, double rx, double ry, string? shapeType)
    {
        var cx = w * rx;
        var cy = h * ry;
        var halfW = w / 2.0;
        var halfH = h / 2.0;

        switch (shapeType?.ToLowerInvariant())
        {
            case "ellipse":
            case "double-ellipse":
            case "half-ellipse":
                {
                    // Ray from center to point, intersect with ellipse
                    var dx = cx - halfW;
                    var dy = cy - halfH;
                    if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                        return (halfW, 0); // center -> top
                    var angle = Math.Atan2(dy / halfH, dx / halfW);
                    return (halfW + halfW * Math.Cos(angle), halfH + halfH * Math.Sin(angle));
                }

            case "diamond":
                {
                    // Ray from center to point, intersect with diamond edges
                    var dx = cx - halfW;
                    var dy = cy - halfH;
                    if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                        return (halfW, 0);
                    var absDx = Math.Abs(dx);
                    var absDy = Math.Abs(dy);
                    var scale = absDx / halfW + absDy / halfH;
                    if (scale < 0.001) return (halfW, 0);
                    return (halfW + dx / scale, halfH + dy / scale);
                }

            case "rectangle":
            case "rounded":
            case "sticky-note":
            case "note":
            case "document":
            case "package":
            case "component":
            case "cube":
            case "cylinder":
            case "swimlane-horizontal":
            case "swimlane-vertical":
            case "table":
            case "pool":
            case "weak-entity":
            default:
                {
                    // Ray from center to point, intersect with rectangle boundary
                    var dx = cx - halfW;
                    var dy = cy - halfH;
                    if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                        return (halfW, 0);
                    var tx = Math.Abs(halfW / dx);
                    var ty = Math.Abs(halfH / dy);
                    var t = Math.Min(tx, ty);
                    return (halfW + dx * t, halfH + dy * t);
                }
        }
    }

    public static (double X, double Y) ApplyPortSpacing((double X, double Y) point, PortSide side, double spacing)
    {
        if (spacing <= 0) return point;
        return side switch
        {
            PortSide.Left => (point.X - spacing, point.Y),
            PortSide.Right => (point.X + spacing, point.Y),
            PortSide.Top => (point.X, point.Y - spacing),
            PortSide.Bottom => (point.X, point.Y + spacing),
            _ => point,
        };
    }
}
