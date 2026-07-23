using System.Globalization;

namespace Tempo.Blazor.Components.Charts;

internal static class SankeyLayoutEngine
{
    private const double DefaultHorizontalPadding = 64;
    private const double DefaultVerticalPadding = 16;

    internal static SankeyLayoutResult Layout(
        SankeyData? data,
        double width,
        double height,
        double nodeWidth,
        double nodePadding,
        double minLinkWidth,
        double horizontalPadding = DefaultHorizontalPadding,
        double verticalPadding = DefaultVerticalPadding)
    {
        if (!HasValidConfiguration(
                data,
                width,
                height,
                nodeWidth,
                nodePadding,
                minLinkWidth,
                horizontalPadding,
                verticalPadding))
        {
            return SankeyLayoutResult.Invalid(SankeyLayoutErrorKind.InvalidData);
        }

        var nodes = data!.Nodes;
        var links = data.Links;
        if (nodes.Count == 0)
        {
            return links.Count == 0
                ? SankeyLayoutResult.Valid([], [])
                : SankeyLayoutResult.Invalid(SankeyLayoutErrorKind.InvalidData);
        }

        var nodeIndexes = new Dictionary<string, int>(nodes.Count, StringComparer.Ordinal);
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node is null ||
                string.IsNullOrWhiteSpace(node.Id) ||
                string.IsNullOrWhiteSpace(node.Label) ||
                !nodeIndexes.TryAdd(node.Id, index))
            {
                return SankeyLayoutResult.Invalid(SankeyLayoutErrorKind.InvalidData);
            }
        }

        var incoming = CreateLinkIndex(nodes.Count);
        var outgoing = CreateLinkIndex(nodes.Count);
        var incomingValues = new double[nodes.Count];
        var outgoingValues = new double[nodes.Count];
        var indegrees = new int[nodes.Count];

        for (var linkIndex = 0; linkIndex < links.Count; linkIndex++)
        {
            var link = links[linkIndex];
            if (link is null ||
                !nodeIndexes.TryGetValue(link.SourceId ?? string.Empty, out var sourceIndex) ||
                !nodeIndexes.TryGetValue(link.TargetId ?? string.Empty, out var targetIndex) ||
                !double.IsFinite(link.Value) ||
                link.Value <= 0)
            {
                return SankeyLayoutResult.Invalid(SankeyLayoutErrorKind.InvalidData);
            }

            outgoing[sourceIndex].Add(linkIndex);
            incoming[targetIndex].Add(linkIndex);
            outgoingValues[sourceIndex] += link.Value;
            incomingValues[targetIndex] += link.Value;
            if (!double.IsFinite(outgoingValues[sourceIndex]) ||
                !double.IsFinite(incomingValues[targetIndex]))
            {
                return SankeyLayoutResult.Invalid(SankeyLayoutErrorKind.InvalidData);
            }

            indegrees[targetIndex]++;
        }

        var layers = CalculateLayers(nodes, links, nodeIndexes, outgoing, indegrees);
        if (layers is null)
        {
            return SankeyLayoutResult.Invalid(SankeyLayoutErrorKind.Cycle);
        }

        var nodeValues = new double[nodes.Count];
        for (var index = 0; index < nodes.Count; index++)
        {
            nodeValues[index] = Math.Max(incomingValues[index], outgoingValues[index]);
        }

        var maxLayer = layers.Max();
        for (var index = 0; index < nodes.Count; index++)
        {
            if (outgoing[index].Count == 0)
            {
                layers[index] = maxLayer;
            }
        }

        var flowScale = CalculateFlowScale(
            layers,
            nodeValues,
            maxLayer,
            height,
            nodePadding,
            verticalPadding);

        var linkWidths = links
            .Select(link => Math.Max(link.Value * flowScale, minLinkWidth))
            .ToArray();

        var nodeHeights = new double[nodes.Count];
        for (var index = 0; index < nodes.Count; index++)
        {
            nodeHeights[index] = nodeValues[index] * flowScale;
        }

        var nodeLayouts = PositionNodes(
            nodes,
            layers,
            nodeValues,
            nodeHeights,
            maxLayer,
            width,
            height,
            nodeWidth,
            nodePadding,
            horizontalPadding,
            verticalPadding);

        var linkLayouts = PositionLinks(
            nodes,
            links,
            nodeIndexes,
            incoming,
            outgoing,
            nodeLayouts,
            linkWidths);

        return SankeyLayoutResult.Valid(nodeLayouts, linkLayouts);
    }

    private static bool HasValidConfiguration(
        SankeyData? data,
        double width,
        double height,
        double nodeWidth,
        double nodePadding,
        double minLinkWidth,
        double horizontalPadding,
        double verticalPadding) =>
        data is not null &&
        data.Nodes is not null &&
        data.Links is not null &&
        IsFinitePositive(width) &&
        IsFinitePositive(height) &&
        IsFinitePositive(nodeWidth) &&
        IsFiniteNonNegative(nodePadding) &&
        IsFiniteNonNegative(minLinkWidth) &&
        IsFiniteNonNegative(horizontalPadding) &&
        IsFiniteNonNegative(verticalPadding) &&
        width >= (horizontalPadding * 2) + nodeWidth &&
        height >= verticalPadding * 2;

    private static bool IsFinitePositive(double value) =>
        double.IsFinite(value) && value > 0;

    private static bool IsFiniteNonNegative(double value) =>
        double.IsFinite(value) && value >= 0;

    private static List<int>[] CreateLinkIndex(int nodeCount) =>
        Enumerable.Range(0, nodeCount)
            .Select(_ => new List<int>())
            .ToArray();

    private static int[]? CalculateLayers(
        IReadOnlyList<SankeyNode> nodes,
        IReadOnlyList<SankeyLink> links,
        IReadOnlyDictionary<string, int> nodeIndexes,
        IReadOnlyList<List<int>> outgoing,
        int[] indegrees)
    {
        var remainingIndegrees = (int[])indegrees.Clone();
        var layers = new int[nodes.Count];
        var ready = new Queue<int>(
            Enumerable.Range(0, nodes.Count).Where(index => remainingIndegrees[index] == 0));
        var processed = 0;

        while (ready.Count > 0)
        {
            var sourceIndex = ready.Dequeue();
            processed++;

            foreach (var linkIndex in outgoing[sourceIndex])
            {
                var targetIndex = nodeIndexes[links[linkIndex].TargetId];
                layers[targetIndex] = Math.Max(layers[targetIndex], layers[sourceIndex] + 1);
                remainingIndegrees[targetIndex]--;
                if (remainingIndegrees[targetIndex] == 0)
                {
                    ready.Enqueue(targetIndex);
                }
            }
        }

        return processed == nodes.Count ? layers : null;
    }

    private static double CalculateFlowScale(
        IReadOnlyList<int> layers,
        IReadOnlyList<double> nodeValues,
        int maxLayer,
        double height,
        double nodePadding,
        double verticalPadding)
    {
        var availableHeight = height - (verticalPadding * 2);
        var scale = double.PositiveInfinity;

        for (var layer = 0; layer <= maxLayer; layer++)
        {
            var indexes = Enumerable.Range(0, layers.Count)
                .Where(index => layers[index] == layer)
                .ToArray();
            var layerValue = indexes.Sum(index => nodeValues[index]);
            if (layerValue <= 0)
            {
                continue;
            }

            var padding = Math.Max(0, indexes.Length - 1) * nodePadding;
            var usableHeight = Math.Max(0, availableHeight - padding);
            scale = Math.Min(scale, usableHeight / layerValue);
        }

        return double.IsPositiveInfinity(scale) ? 0 : scale;
    }

    private static SankeyNodeLayout[] PositionNodes(
        IReadOnlyList<SankeyNode> nodes,
        IReadOnlyList<int> layers,
        IReadOnlyList<double> nodeValues,
        IReadOnlyList<double> nodeHeights,
        int maxLayer,
        double width,
        double height,
        double nodeWidth,
        double nodePadding,
        double horizontalPadding,
        double verticalPadding)
    {
        var result = new SankeyNodeLayout[nodes.Count];
        var availableHeight = height - (verticalPadding * 2);
        var availableWidth = width - (horizontalPadding * 2) - nodeWidth;
        var layerGap = maxLayer == 0 ? 0 : availableWidth / maxLayer;

        for (var layer = 0; layer <= maxLayer; layer++)
        {
            var indexes = Enumerable.Range(0, nodes.Count)
                .Where(index => layers[index] == layer)
                .ToArray();
            var contentHeight =
                indexes.Sum(index => nodeHeights[index]) +
                (Math.Max(0, indexes.Length - 1) * nodePadding);
            var y = verticalPadding + ((availableHeight - contentHeight) / 2);

            foreach (var index in indexes)
            {
                result[index] = new SankeyNodeLayout(
                    nodes[index],
                    layer,
                    horizontalPadding + (layer * layerGap),
                    y,
                    nodeWidth,
                    nodeHeights[index],
                    nodeValues[index]);
                y += nodeHeights[index] + nodePadding;
            }
        }

        return result;
    }

    private static SankeyLinkLayout[] PositionLinks(
        IReadOnlyList<SankeyNode> nodes,
        IReadOnlyList<SankeyLink> links,
        IReadOnlyDictionary<string, int> nodeIndexes,
        IReadOnlyList<List<int>> incoming,
        IReadOnlyList<List<int>> outgoing,
        IReadOnlyList<SankeyNodeLayout> nodeLayouts,
        IReadOnlyList<double> linkWidths)
    {
        var sourceCenters = new double[links.Count];
        var targetCenters = new double[links.Count];

        for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            PositionLinkCenters(
                outgoing[nodeIndex]
                    .OrderBy(linkIndex => nodeIndexes[links[linkIndex].TargetId])
                    .ThenBy(linkIndex => linkIndex),
                nodeLayouts[nodeIndex],
                linkWidths,
                sourceCenters);
            PositionLinkCenters(
                incoming[nodeIndex]
                    .OrderBy(linkIndex => nodeIndexes[links[linkIndex].SourceId])
                    .ThenBy(linkIndex => linkIndex),
                nodeLayouts[nodeIndex],
                linkWidths,
                targetCenters);
        }

        var result = new SankeyLinkLayout[links.Count];
        for (var linkIndex = 0; linkIndex < links.Count; linkIndex++)
        {
            var link = links[linkIndex];
            var source = nodeLayouts[nodeIndexes[link.SourceId]];
            var target = nodeLayouts[nodeIndexes[link.TargetId]];
            var sourceX = source.X + source.Width;
            var targetX = target.X;
            var midpointX = sourceX + ((targetX - sourceX) / 2);
            var sourceY = sourceCenters[linkIndex];
            var targetY = targetCenters[linkIndex];
            var pathData = FormattableString.Invariant(
                $"M {F(sourceX)},{F(sourceY)} C {F(midpointX)},{F(sourceY)} {F(midpointX)},{F(targetY)} {F(targetX)},{F(targetY)}");

            result[linkIndex] = new SankeyLinkLayout(
                link,
                sourceX,
                sourceY,
                targetX,
                targetY,
                midpointX,
                linkWidths[linkIndex],
                pathData);
        }

        return result;
    }

    private static void PositionLinkCenters(
        IEnumerable<int> orderedLinkIndexes,
        SankeyNodeLayout node,
        IReadOnlyList<double> linkWidths,
        double[] centers)
    {
        var indexes = orderedLinkIndexes.ToArray();
        var totalWidth = indexes.Sum(index => linkWidths[index]);
        var offset = node.Y + ((node.Height - totalWidth) / 2);

        foreach (var linkIndex in indexes)
        {
            centers[linkIndex] = offset + (linkWidths[linkIndex] / 2);
            offset += linkWidths[linkIndex];
        }
    }

    private static string F(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}

internal sealed record SankeyLayoutResult(
    bool IsValid,
    SankeyLayoutErrorKind ErrorKind,
    IReadOnlyList<SankeyNodeLayout> Nodes,
    IReadOnlyList<SankeyLinkLayout> Links)
{
    internal static SankeyLayoutResult Valid(
        IReadOnlyList<SankeyNodeLayout> nodes,
        IReadOnlyList<SankeyLinkLayout> links) =>
        new(true, SankeyLayoutErrorKind.None, nodes, links);

    internal static SankeyLayoutResult Invalid(SankeyLayoutErrorKind errorKind) =>
        new(false, errorKind, [], []);
}

internal sealed record SankeyNodeLayout(
    SankeyNode Node,
    int Layer,
    double X,
    double Y,
    double Width,
    double Height,
    double Value);

internal sealed record SankeyLinkLayout(
    SankeyLink Link,
    double SourceX,
    double SourceY,
    double TargetX,
    double TargetY,
    double MidpointX,
    double Width,
    string PathData);
