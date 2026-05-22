using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Geometry helpers used by document page layout and object wrapping.</summary>
public static class DocumentLayoutGeometryHelper
{
    private const double Epsilon = 0.0001;
    private const int MinimumWrapContourPointCount = 3;

    /// <summary>Returns whether two rectangles overlap with positive area.</summary>
    public static bool Intersects(DocumentLayoutRect a, DocumentLayoutRect b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return a.X < b.Right
            && a.Right > b.X
            && a.Y < b.Bottom
            && a.Bottom > b.Y;
    }

    /// <summary>Returns the overlapping area of two rectangles, or an empty rectangle if they do not overlap.</summary>
    public static DocumentLayoutRect Intersection(DocumentLayoutRect a, DocumentLayoutRect b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);

        if (right <= left || bottom <= top)
        {
            return new DocumentLayoutRect { X = left, Y = top };
        }

        return DocumentLayoutRect.FromBounds(left, top, right, bottom);
    }

    /// <summary>Returns the smallest rectangle containing both input rectangles.</summary>
    public static DocumentLayoutRect Union(DocumentLayoutRect a, DocumentLayoutRect b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.IsEmpty)
        {
            return b.Clone();
        }

        if (b.IsEmpty)
        {
            return a.Clone();
        }

        return DocumentLayoutRect.FromBounds(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Max(a.Right, b.Right),
            Math.Max(a.Bottom, b.Bottom));
    }

    /// <summary>Returns the smallest rectangle containing all input rectangles.</summary>
    public static DocumentLayoutRect Union(IEnumerable<DocumentLayoutRect> rects)
    {
        ArgumentNullException.ThrowIfNull(rects);

        using var enumerator = rects.Where(rect => rect is not null && !rect.IsEmpty).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return new DocumentLayoutRect();
        }

        var result = enumerator.Current.Clone();
        while (enumerator.MoveNext())
        {
            result = Union(result, enumerator.Current);
        }

        return result;
    }

    /// <summary>Clamps a rectangle origin into the page body while preserving the rectangle size when possible.</summary>
    public static DocumentLayoutRect ClampToBody(DocumentLayoutRect rect, DocumentLayoutRect bodyRect)
    {
        ArgumentNullException.ThrowIfNull(rect);
        ArgumentNullException.ThrowIfNull(bodyRect);

        var x = rect.Width >= bodyRect.Width
            ? bodyRect.X
            : Math.Clamp(rect.X, bodyRect.X, bodyRect.Right - rect.Width);
        var y = rect.Height >= bodyRect.Height
            ? bodyRect.Y
            : Math.Clamp(rect.Y, bodyRect.Y, bodyRect.Bottom - rect.Height);

        return new DocumentLayoutRect
        {
            X = x,
            Y = y,
            Width = rect.Width,
            Height = rect.Height
        };
    }

    /// <summary>Resolves an object rectangle from relative object layout metadata.</summary>
    public static DocumentLayoutRect ResolveObjectRect(
        DocumentObjectLayout layout,
        DocumentLayoutRect pageRect,
        DocumentLayoutRect bodyRect,
        DocumentLayoutRect? paragraphRect = null,
        DocumentLayoutRect? characterRect = null,
        DocumentLayoutRect? lineRect = null,
        double fallbackWidth = 0,
        double fallbackHeight = 0,
        bool clampToBody = false)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(pageRect);
        ArgumentNullException.ThrowIfNull(bodyRect);

        var width = layout.Transform.Width
            ?? layout.Transform.NaturalWidth
            ?? fallbackWidth;
        var height = layout.Transform.Height
            ?? layout.Transform.NaturalHeight
            ?? fallbackHeight;
        var horizontalReference = ResolveReferenceRect(layout.Position.HorizontalRelativeTo, pageRect, bodyRect, paragraphRect, characterRect, lineRect);
        var verticalReference = ResolveReferenceRect(layout.Position.VerticalRelativeTo, pageRect, bodyRect, paragraphRect, characterRect, lineRect);

        var x = ResolveHorizontalPosition(layout.Position, horizontalReference, width);
        var y = ResolveVerticalPosition(layout.Position, verticalReference, height);
        var rect = new DocumentLayoutRect
        {
            X = x,
            Y = y,
            Width = Math.Max(0, width),
            Height = Math.Max(0, height)
        };

        return clampToBody ? ClampToBody(rect, bodyRect) : rect;
    }

    /// <summary>Creates a full object layout box including wrap rectangle and z-index.</summary>
    public static DocumentObjectLayoutBox CreateObjectLayoutBox(
        string objectId,
        string blockId,
        DocumentObjectLayout layout,
        DocumentLayoutRect pageRect,
        DocumentLayoutRect bodyRect,
        DocumentLayoutRect? paragraphRect = null,
        DocumentLayoutRect? characterRect = null,
        DocumentLayoutRect? lineRect = null,
        double fallbackWidth = 0,
        double fallbackHeight = 0,
        bool clampToBody = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockId);
        ArgumentNullException.ThrowIfNull(layout);

        var objectRect = ResolveObjectRect(
            layout,
            pageRect,
            bodyRect,
            paragraphRect,
            characterRect,
            lineRect,
            fallbackWidth,
            fallbackHeight,
            clampToBody);

        return new DocumentObjectLayoutBox
        {
            Id = objectId,
            BlockId = blockId,
            AnchorBlockId = layout.Anchor.BlockId,
            ObjectRect = objectRect,
            WrapRect = ComputeWrapRect(objectRect, layout.Wrap),
            Layout = layout,
            ZIndex = layout.Stacking.ZIndex,
            AllowOverlap = layout.Stacking.AllowOverlap
        };
    }

    /// <summary>Expands an object rectangle by the configured wrap distances.</summary>
    public static DocumentLayoutRect ComputeWrapRect(DocumentLayoutRect objectRect, DocumentObjectWrap wrap)
    {
        ArgumentNullException.ThrowIfNull(objectRect);
        ArgumentNullException.ThrowIfNull(wrap);

        return DocumentLayoutRect.FromBounds(
            objectRect.X - wrap.DistanceLeft,
            objectRect.Y - wrap.DistanceTop,
            objectRect.Right + wrap.DistanceRight,
            objectRect.Bottom + wrap.DistanceBottom);
    }

    /// <summary>Returns the default rectangular wrap contour in normalized object coordinates.</summary>
    public static IReadOnlyList<DocumentObjectWrapPoint> CreateDefaultWrapContourPoints()
        =>
        [
            new() { X = 0, Y = 0 },
            new() { X = 1, Y = 0 },
            new() { X = 1, Y = 1 },
            new() { X = 0, Y = 1 }
        ];

    /// <summary>Clamps wrap contour points to normalized coordinates and falls back to a rectangle when too few valid points are provided.</summary>
    public static IReadOnlyList<DocumentObjectWrapPoint> NormalizeWrapContourPoints(IEnumerable<DocumentObjectWrapPoint>? points)
    {
        var normalized = (points ?? [])
            .Where(point => point is not null)
            .Select(point => new DocumentObjectWrapPoint
            {
                X = ClampNormalized(point.X),
                Y = ClampNormalized(point.Y)
            })
            .ToList();

        return normalized.Count >= MinimumWrapContourPointCount
            ? normalized
            : CreateDefaultWrapContourPoints();
    }

    /// <summary>Projects normalized wrap contour points to page coordinates.</summary>
    public static IReadOnlyList<DocumentLayoutPoint> ProjectWrapContourPoints(
        DocumentObjectLayoutBox objectBox,
        DocumentLayoutRect pageBodyRect)
    {
        ArgumentNullException.ThrowIfNull(objectBox);
        ArgumentNullException.ThrowIfNull(pageBodyRect);

        var sourceRect = objectBox.WrapRect.IsEmpty ? objectBox.ObjectRect : objectBox.WrapRect;
        return NormalizeWrapContourPoints(objectBox.Layout.Wrap.WrapContourPoints)
            .Select(point => new DocumentLayoutPoint
            {
                X = Math.Clamp(sourceRect.X + (sourceRect.Width * point.X), pageBodyRect.X, pageBodyRect.Right),
                Y = Math.Clamp(sourceRect.Y + (sourceRect.Height * point.Y), pageBodyRect.Y, pageBodyRect.Bottom)
            })
            .ToList();
    }

    /// <summary>Orders object boxes by page and z-index while preserving stable relative order for equal z-index values.</summary>
    public static IReadOnlyList<DocumentObjectLayoutBox> OrderByZIndex(IEnumerable<DocumentObjectLayoutBox> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);

        return objects
            .Select((box, index) => new { Box = box, Index = index })
            .OrderBy(item => item.Box.PageIndex)
            .ThenBy(item => item.Box.ZIndex)
            .ThenBy(item => item.Index)
            .Select(item => item.Box)
            .ToList();
    }

    /// <summary>Builds text exclusion zones for a set of positioned object boxes.</summary>
    public static IReadOnlyList<DocumentExclusionZone> BuildExclusionZones(
        IEnumerable<DocumentObjectLayoutBox> objects,
        DocumentLayoutRect pageBodyRect)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(pageBodyRect);

        return OrderByZIndex(objects)
            .Select(box => CreateExclusionZone(box, pageBodyRect))
            .Where(zone => zone is not null)
            .Select(zone => zone!)
            .ToList();
    }

    /// <summary>Creates a text exclusion zone for one object, or returns <c>null</c> when the wrap mode does not block text.</summary>
    public static DocumentExclusionZone? CreateExclusionZone(DocumentObjectLayoutBox objectBox, DocumentLayoutRect pageBodyRect)
    {
        ArgumentNullException.ThrowIfNull(objectBox);
        ArgumentNullException.ThrowIfNull(pageBodyRect);

        if (objectBox.Layout.IsInline)
        {
            return null;
        }

        var mode = objectBox.Layout.Wrap.Mode;
        if (mode is DocumentWrapMode.BehindText or DocumentWrapMode.InFrontOfText)
        {
            return null;
        }

        List<DocumentLayoutPoint> polygon = [];
        var isContourPlaceholder = false;
        var candidate = mode == DocumentWrapMode.TopBottom
            ? DocumentLayoutRect.FromBounds(pageBodyRect.X, objectBox.WrapRect.Y, pageBodyRect.Right, objectBox.WrapRect.Bottom)
            : objectBox.WrapRect;
        if (mode is DocumentWrapMode.Tight or DocumentWrapMode.Through)
        {
            polygon = ProjectWrapContourPoints(objectBox, pageBodyRect).ToList();
            candidate = BoundsOfPoints(polygon);
        }

        var clipped = Intersection(candidate, pageBodyRect);

        if (clipped.IsEmpty)
        {
            return null;
        }

        return new DocumentExclusionZone
        {
            ObjectId = objectBox.Id,
            BlockId = objectBox.BlockId,
            PageIndex = objectBox.PageIndex,
            WrapMode = mode,
            Rect = clipped,
            Polygon = polygon,
            BlocksText = true,
            IsContourPlaceholder = isContourPlaceholder
        };
    }

    /// <summary>Returns the horizontal line intervals left after subtracting all overlapping exclusion zones.</summary>
    public static IReadOnlyList<DocumentLayoutInterval> GetAvailableLineIntervals(
        double y,
        double lineHeight,
        IEnumerable<DocumentExclusionZone> exclusions,
        DocumentLayoutRect lineBounds,
        double minimumIntervalWidth = 1)
    {
        ArgumentNullException.ThrowIfNull(exclusions);
        ArgumentNullException.ThrowIfNull(lineBounds);

        var lineRect = new DocumentLayoutRect
        {
            X = lineBounds.X,
            Y = y,
            Width = lineBounds.Width,
            Height = lineHeight
        };

        var intervals = new List<DocumentLayoutInterval>
        {
            new()
            {
                X = lineBounds.X,
                Width = lineBounds.Width
            }
        };

        foreach (var exclusion in exclusions.Where(exclusion => exclusion.BlocksText && Intersects(lineRect, exclusion.Rect)))
        {
            var blockedIntervals = exclusion.Polygon.Count >= MinimumWrapContourPointCount
                ? GetPolygonBlockedIntervals(exclusion.Polygon, y, lineHeight, lineBounds, minimumIntervalWidth)
                : [new DocumentLayoutInterval
                    {
                        X = Math.Max(exclusion.Rect.X, lineBounds.X),
                        Width = Math.Min(exclusion.Rect.Right, lineBounds.Right) - Math.Max(exclusion.Rect.X, lineBounds.X)
                    }];

            foreach (var blocked in blockedIntervals)
            {
                var blockedLeft = Math.Max(blocked.X, lineBounds.X);
                var blockedRight = Math.Min(blocked.End, lineBounds.Right);
                if (blockedRight <= blockedLeft)
                {
                    continue;
                }

                intervals = SubtractInterval(intervals, blockedLeft, blockedRight, minimumIntervalWidth);
                if (intervals.Count == 0)
                {
                    break;
                }
            }
        }

        return intervals
            .Where(interval => interval.Width >= minimumIntervalWidth - Epsilon)
            .OrderBy(interval => interval.X)
            .ToList();
    }

    private static IReadOnlyList<DocumentLayoutInterval> GetPolygonBlockedIntervals(
        IReadOnlyList<DocumentLayoutPoint> polygon,
        double y,
        double lineHeight,
        DocumentLayoutRect lineBounds,
        double minimumIntervalWidth)
    {
        var top = y;
        var bottom = y + lineHeight;
        var sampleYs = new SortedSet<double>
        {
            ClampBandSample(top + Epsilon, top, bottom),
            ClampBandSample(top + (lineHeight / 2), top, bottom),
            ClampBandSample(bottom - Epsilon, top, bottom)
        };

        foreach (var point in polygon)
        {
            if (point.Y > top + Epsilon && point.Y < bottom - Epsilon)
            {
                sampleYs.Add(point.Y);
            }
        }

        var intervals = new List<DocumentLayoutInterval>();
        foreach (var sampleY in sampleYs)
        {
            foreach (var interval in GetPolygonIntervalsAtY(polygon, sampleY))
            {
                var left = Math.Max(lineBounds.X, interval.X);
                var right = Math.Min(lineBounds.Right, interval.End);
                if (right - left >= minimumIntervalWidth - Epsilon)
                {
                    intervals.Add(new DocumentLayoutInterval { X = left, Width = right - left });
                }
            }
        }

        return MergeIntervals(intervals, minimumIntervalWidth);
    }

    private static IReadOnlyList<DocumentLayoutInterval> GetPolygonIntervalsAtY(
        IReadOnlyList<DocumentLayoutPoint> polygon,
        double y)
    {
        var intersections = new List<double>();
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if (Math.Abs(a.Y - b.Y) < Epsilon)
            {
                continue;
            }

            var minY = Math.Min(a.Y, b.Y);
            var maxY = Math.Max(a.Y, b.Y);
            if (y < minY || y >= maxY)
            {
                continue;
            }

            var ratio = (y - a.Y) / (b.Y - a.Y);
            intersections.Add(a.X + ((b.X - a.X) * ratio));
        }

        intersections.Sort();
        var intervals = new List<DocumentLayoutInterval>();
        for (var i = 0; i + 1 < intersections.Count; i += 2)
        {
            var left = intersections[i];
            var right = intersections[i + 1];
            if (right > left + Epsilon)
            {
                intervals.Add(new DocumentLayoutInterval { X = left, Width = right - left });
            }
        }

        return intervals;
    }

    private static IReadOnlyList<DocumentLayoutInterval> MergeIntervals(
        IEnumerable<DocumentLayoutInterval> intervals,
        double minimumIntervalWidth)
    {
        var ordered = intervals
            .Where(interval => interval.Width >= minimumIntervalWidth - Epsilon)
            .OrderBy(interval => interval.X)
            .ToList();
        if (ordered.Count <= 1)
        {
            return ordered;
        }

        var merged = new List<DocumentLayoutInterval> { ordered[0] };
        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = merged[^1];
            var current = ordered[i];
            if (current.X <= previous.End + Epsilon)
            {
                previous.Width = Math.Max(previous.End, current.End) - previous.X;
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }

    private static DocumentLayoutRect BoundsOfPoints(IReadOnlyList<DocumentLayoutPoint> points)
    {
        if (points.Count == 0)
        {
            return new DocumentLayoutRect();
        }

        return DocumentLayoutRect.FromBounds(
            points.Min(point => point.X),
            points.Min(point => point.Y),
            points.Max(point => point.X),
            points.Max(point => point.Y));
    }

    private static double ClampNormalized(double value)
        => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    private static double ClampBandSample(double value, double top, double bottom)
        => Math.Clamp(value, top + Epsilon, Math.Max(top + Epsilon, bottom - Epsilon));

    private static List<DocumentLayoutInterval> SubtractInterval(
        IEnumerable<DocumentLayoutInterval> intervals,
        double blockedLeft,
        double blockedRight,
        double minimumIntervalWidth)
    {
        var result = new List<DocumentLayoutInterval>();

        foreach (var interval in intervals)
        {
            if (blockedRight <= interval.X || blockedLeft >= interval.End)
            {
                result.Add(interval);
                continue;
            }

            var leftWidth = Math.Max(0, blockedLeft - interval.X);
            if (leftWidth >= minimumIntervalWidth - Epsilon)
            {
                result.Add(new DocumentLayoutInterval
                {
                    X = interval.X,
                    Width = leftWidth
                });
            }

            var rightStart = Math.Max(blockedRight, interval.X);
            var rightWidth = Math.Max(0, interval.End - rightStart);
            if (rightWidth >= minimumIntervalWidth - Epsilon)
            {
                result.Add(new DocumentLayoutInterval
                {
                    X = rightStart,
                    Width = rightWidth
                });
            }
        }

        return result;
    }

    private static DocumentLayoutRect ResolveReferenceRect(
        DocumentRelativePosition relativeTo,
        DocumentLayoutRect pageRect,
        DocumentLayoutRect bodyRect,
        DocumentLayoutRect? paragraphRect,
        DocumentLayoutRect? characterRect,
        DocumentLayoutRect? lineRect)
        => relativeTo switch
        {
            DocumentRelativePosition.Page => pageRect,
            DocumentRelativePosition.Margin or DocumentRelativePosition.Column => bodyRect,
            DocumentRelativePosition.Paragraph => paragraphRect ?? bodyRect,
            DocumentRelativePosition.Character => characterRect ?? paragraphRect ?? bodyRect,
            DocumentRelativePosition.Line => lineRect ?? paragraphRect ?? bodyRect,
            _ => bodyRect
        };

    private static double ResolveHorizontalPosition(DocumentObjectPosition position, DocumentLayoutRect reference, double width)
        => position.HorizontalAlignment switch
        {
            DocumentImageHorizontalPosition.Left => reference.X + position.X,
            DocumentImageHorizontalPosition.Center => reference.X + ((reference.Width - width) / 2) + position.X,
            DocumentImageHorizontalPosition.Right => reference.Right - width + position.X,
            _ => reference.X + position.X
        };

    private static double ResolveVerticalPosition(DocumentObjectPosition position, DocumentLayoutRect reference, double height)
        => position.VerticalAlignment switch
        {
            DocumentObjectVerticalAlignment.Top => reference.Y + position.Y,
            DocumentObjectVerticalAlignment.Middle => reference.Y + ((reference.Height - height) / 2) + position.Y,
            DocumentObjectVerticalAlignment.Bottom => reference.Bottom - height + position.Y,
            _ => reference.Y + position.Y
        };
}
