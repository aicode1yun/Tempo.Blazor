using System.Globalization;
using System.Text.Json;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

internal static class StencilLayoutEngine
{
    internal static IReadOnlyList<LayoutRect> LayoutChildren(
        RenderNode container,
        double width,
        double height,
        IReadOnlyList<LayoutRect> childSizes)
    {
        var gap = Number(container, "gap", 0);
        var padding = Number(container, "padding", 0);

        return container.Kind == RenderNodeKind.Grid
            ? LayoutGrid(container, width, childSizes, gap, padding)
            : LayoutStackOrRow(container, width, height, childSizes, gap, padding);
    }

    internal static LayoutRect ApplyAnchors(RenderNode node, double width, double height)
    {
        var marginLeft = Number(node, "margin.left", Number(node, "marginLeft", 0));
        var marginRight = Number(node, "margin.right", Number(node, "marginRight", 0));
        var marginTop = Number(node, "margin.top", Number(node, "marginTop", 0));
        var marginBottom = Number(node, "margin.bottom", Number(node, "marginBottom", 0));
        var nodeW = Number(node, "w", Number(node, "width", 0));
        var nodeH = Number(node, "h", Number(node, "height", 0));
        var x = Number(node, "x", 0);
        var y = Number(node, "y", 0);
        var anchor = Text(node, "anchor", string.Empty);
        var anchorX = Text(node, "anchorX", anchor);
        var anchorY = Text(node, "anchorY", anchor);

        if (HasAnchor(anchorX, "stretch") || HasAnchor(node, "stretchX"))
        {
            x = marginLeft;
            nodeW = Math.Max(0, width - marginLeft - marginRight);
        }
        else if (HasAnchor(anchorX, "right") || HasAnchor(node, "right"))
        {
            x = width - marginRight - nodeW;
        }
        else if (HasAnchor(anchorX, "left") || HasAnchor(node, "left"))
        {
            x = marginLeft;
        }

        if (HasAnchor(anchorY, "stretch") || HasAnchor(node, "stretchY"))
        {
            y = marginTop;
            nodeH = Math.Max(0, height - marginTop - marginBottom);
        }
        else if (HasAnchor(anchorY, "bottom") || HasAnchor(node, "bottom"))
        {
            y = height - marginBottom - nodeH;
        }
        else if (HasAnchor(anchorY, "top") || HasAnchor(node, "top"))
        {
            y = marginTop;
        }

        return new LayoutRect(x, y, nodeW, nodeH);
    }

    internal static IReadOnlyList<SliceSegment> NineSlice(
        double width,
        double height,
        SliceInsets slice)
    {
        var left = Clamp(slice.Left, 0, width);
        var right = Clamp(slice.Right, 0, Math.Max(0, width - left));
        var top = Clamp(slice.Top, 0, height);
        var bottom = Clamp(slice.Bottom, 0, Math.Max(0, height - top));
        var centerW = Math.Max(0, width - left - right);
        var centerH = Math.Max(0, height - top - bottom);
        var rightX = width - right;
        var bottomY = height - bottom;

        return
        [
            new("top-left", new LayoutRect(0, 0, left, top)),
            new("top", new LayoutRect(left, 0, centerW, top)),
            new("top-right", new LayoutRect(rightX, 0, right, top)),
            new("left", new LayoutRect(0, top, left, centerH)),
            new("center", new LayoutRect(left, top, centerW, centerH)),
            new("right", new LayoutRect(rightX, top, right, centerH)),
            new("bottom-left", new LayoutRect(0, bottomY, left, bottom)),
            new("bottom", new LayoutRect(left, bottomY, centerW, bottom)),
            new("bottom-right", new LayoutRect(rightX, bottomY, right, bottom))
        ];
    }

    internal static bool HasAnchor(RenderNode node)
        => HasAttribute(node, "anchor")
           || HasAttribute(node, "anchorX")
           || HasAttribute(node, "anchorY")
           || HasAttribute(node, "left")
           || HasAttribute(node, "right")
           || HasAttribute(node, "top")
           || HasAttribute(node, "bottom")
           || HasAttribute(node, "stretchX")
           || HasAttribute(node, "stretchY");

    private static IReadOnlyList<LayoutRect> LayoutStackOrRow(
        RenderNode container,
        double width,
        double height,
        IReadOnlyList<LayoutRect> childSizes,
        double gap,
        double padding)
    {
        var direction = Text(container, "direction", container.Kind == RenderNodeKind.Row ? "row" : "column");
        var align = Text(container, "align", "start");
        var isRow = direction.Equals("row", StringComparison.OrdinalIgnoreCase);
        var results = new List<LayoutRect>(childSizes.Count);
        var cursor = padding;
        var cross = Math.Max(0, (isRow ? height : width) - padding * 2);

        foreach (var size in childSizes)
        {
            var w = size.W;
            var h = size.H;
            if (align.Equals("stretch", StringComparison.OrdinalIgnoreCase))
            {
                if (isRow)
                    h = cross;
                else
                    w = cross;
            }

            results.Add(isRow
                ? new LayoutRect(cursor, padding, w, h)
                : new LayoutRect(padding, cursor, w, h));
            cursor += (isRow ? w : h) + gap;
        }

        return results;
    }

    private static IReadOnlyList<LayoutRect> LayoutGrid(
        RenderNode container,
        double width,
        IReadOnlyList<LayoutRect> childSizes,
        double gap,
        double padding)
    {
        var columns = Math.Max(1, (int)Math.Round(Number(container, "columns", 1)));
        var cellW = Math.Max(0, (width - padding * 2 - (columns - 1) * gap) / columns);
        var cellH = childSizes.Count == 0 ? 0 : childSizes.Max(x => x.H);
        var results = new List<LayoutRect>(childSizes.Count);

        for (var i = 0; i < childSizes.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;
            results.Add(new LayoutRect(
                padding + col * (cellW + gap),
                padding + row * (cellH + gap),
                cellW,
                childSizes[i].H));
        }

        return results;
    }

    private static bool HasAnchor(string value, string expected)
        => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(x => x.Equals(expected, StringComparison.OrdinalIgnoreCase));

    private static bool HasAnchor(RenderNode node, string key)
        => node.Attributes.TryGetValue(key, out var value)
           && new StencilValue(value).AsBool();

    private static bool HasAttribute(RenderNode node, string key)
        => node.Attributes.ContainsKey(key);

    private static string Text(RenderNode node, string key, string fallback)
        => node.Attributes.TryGetValue(key, out var value)
            ? AttributeText(value)
            : fallback;

    private static double Number(RenderNode node, string key, double fallback)
        => node.Attributes.TryGetValue(key, out var value)
           && double.TryParse(AttributeText(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : fallback;

    private static string AttributeText(object? value)
        => value switch
        {
            null => string.Empty,
            string text => text,
            JsonElement element => element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? string.Empty
                : element.GetRawText(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

    private static double Clamp(double value, double min, double max)
        => Math.Min(max, Math.Max(min, value));
}

internal readonly record struct LayoutRect(double X, double Y, double W, double H);

internal readonly record struct SliceInsets(double Left, double Top, double Right, double Bottom);

internal readonly record struct SliceSegment(string Name, LayoutRect Rect);
