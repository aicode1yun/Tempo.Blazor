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
        else
        {
            var srcNode = doc.Nodes.FirstOrDefault(n => n.Id == edge.SourceNodeId);
            if (srcNode is null) return [];
            var srcPort = edge.SourcePortId is not null
                ? srcNode.Ports.FirstOrDefault(p => p.Id == edge.SourcePortId)
                : srcNode.Ports.FirstOrDefault();
            var sPort = srcPort ?? new DiagramPort { Side = PortSide.Right, Offset = 0.5 };
            p1 = ComputePortPosition(srcNode, sPort);
            sSide = sPort.Side;
            p1 = ApplyPortSpacing(p1, sSide, edge.SourceSpacing ?? 0);
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
        else
        {
            var tgtNode = doc.Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
            if (tgtNode is null) return [];
            var tgtPort = edge.TargetPortId is not null
                ? tgtNode.Ports.FirstOrDefault(p => p.Id == edge.TargetPortId)
                : tgtNode.Ports.FirstOrDefault();
            var tPort = tgtPort ?? new DiagramPort { Side = PortSide.Left, Offset = 0.5 };
            p2 = ComputePortPosition(tgtNode, tPort);
            tSide = tPort.Side;
            p2 = ApplyPortSpacing(p2, tSide, edge.TargetSpacing ?? 0);
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
