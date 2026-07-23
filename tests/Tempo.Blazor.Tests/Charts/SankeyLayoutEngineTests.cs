using FluentAssertions;
using Tempo.Blazor.Components.Charts;

namespace Tempo.Blazor.Tests.Charts;

/// <summary>Unit tests for the pure C# Sankey layout engine.</summary>
public class SankeyLayoutEngineTests
{
    private const double Width = 800;
    private const double Height = 400;
    private const double NodeWidth = 16;
    private const double NodePadding = 10;
    private const double MinLinkWidth = 1;

    [Fact]
    public void Layout_AssignsLongestPathLayersAndMovesSinksToLastLayer()
    {
        var data = Data(
            [Node("A"), Node("B"), Node("C"), Node("D"), Node("E")],
            [
                Link("A", "B", 10),
                Link("B", "C", 10),
                Link("A", "D", 5),
            ]);

        var result = Layout(data);

        result.IsValid.Should().BeTrue();
        result.Nodes.Single(node => node.Node.Id == "A").Layer.Should().Be(0);
        result.Nodes.Single(node => node.Node.Id == "B").Layer.Should().Be(1);
        result.Nodes.Single(node => node.Node.Id == "C").Layer.Should().Be(2);
        result.Nodes.Single(node => node.Node.Id == "D").Layer.Should().Be(2);
        result.Nodes.Single(node => node.Node.Id == "E").Layer.Should().Be(2);
        result.Nodes.Select(node => node.X).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void Layout_ScalesNodeHeightProportionallyToNodeValue()
    {
        var data = Data(
            [Node("A"), Node("B"), Node("C"), Node("D")],
            [
                Link("A", "C", 20),
                Link("B", "D", 10),
            ]);

        var result = Layout(data);

        var large = result.Nodes.Single(node => node.Node.Id == "A");
        var small = result.Nodes.Single(node => node.Node.Id == "B");
        large.Value.Should().Be(20);
        small.Value.Should().Be(10);
        large.Height.Should().BeApproximately(small.Height * 2, 0.0001);
    }

    [Fact]
    public void Layout_UsesMaximumOfIncomingAndOutgoingFlowAsNodeValue()
    {
        var data = Data(
            [Node("A"), Node("B"), Node("C"), Node("D")],
            [
                Link("A", "B", 7),
                Link("D", "B", 5),
                Link("B", "C", 9),
            ]);

        var result = Layout(data);

        result.Nodes.Single(node => node.Node.Id == "B").Value.Should().Be(12);
    }

    [Fact]
    public void Layout_AppliesNodePaddingWithoutVerticalOverlap()
    {
        var data = Data(
            [Node("A"), Node("B"), Node("C"), Node("D")],
            [
                Link("A", "C", 10),
                Link("B", "D", 10),
            ]);

        var result = Layout(data);

        foreach (var layer in result.Nodes.GroupBy(node => node.Layer))
        {
            var ordered = layer.OrderBy(node => node.Y).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                ordered[index].Y.Should().BeGreaterThanOrEqualTo(
                    ordered[index - 1].Y + ordered[index - 1].Height + NodePadding);
            }
        }
    }

    [Fact]
    public void Layout_CreatesCubicBezierPathsThatMeetNodeEdges()
    {
        var data = Data(
            [Node("A"), Node("B")],
            [Link("A", "B", 10)]);

        var result = Layout(data);

        var source = result.Nodes.Single(node => node.Node.Id == "A");
        var target = result.Nodes.Single(node => node.Node.Id == "B");
        var link = result.Links.Single();
        link.SourceX.Should().Be(source.X + source.Width);
        link.TargetX.Should().Be(target.X);
        link.SourceY.Should().BeInRange(source.Y, source.Y + source.Height);
        link.TargetY.Should().BeInRange(target.Y, target.Y + target.Height);
        link.PathData.Should().Be(
            FormattableString.Invariant(
                $"M {link.SourceX:0.###},{link.SourceY:0.###} C {link.MidpointX:0.###},{link.SourceY:0.###} {link.MidpointX:0.###},{link.TargetY:0.###} {link.TargetX:0.###},{link.TargetY:0.###}"));
    }

    [Fact]
    public void Layout_StacksMultipleFlowsAtEachNodeWithoutOverlap()
    {
        var data = Data(
            [Node("A"), Node("B"), Node("C"), Node("D")],
            [
                Link("A", "C", 10),
                Link("A", "D", 20),
                Link("B", "D", 5),
            ]);

        var result = Layout(data);

        var outgoing = result.Links
            .Where(link => link.Link.SourceId == "A")
            .OrderBy(link => link.SourceY)
            .ToArray();
        outgoing.Should().HaveCount(2);
        (outgoing[0].SourceY + outgoing[0].Width / 2)
            .Should().BeLessThanOrEqualTo(outgoing[1].SourceY - outgoing[1].Width / 2);

        var incoming = result.Links
            .Where(link => link.Link.TargetId == "D")
            .OrderBy(link => link.TargetY)
            .ToArray();
        incoming.Should().HaveCount(2);
        (incoming[0].TargetY + incoming[0].Width / 2)
            .Should().BeLessThanOrEqualTo(incoming[1].TargetY - incoming[1].Width / 2);
    }

    [Fact]
    public void Layout_AppliesMinimumLinkWidth()
    {
        var data = Data(
            [Node("A"), Node("B"), Node("C"), Node("D")],
            [
                Link("A", "C", 1_000),
                Link("B", "D", 0.001),
            ]);

        var result = SankeyLayoutEngine.Layout(
            data,
            Width,
            Height,
            NodeWidth,
            NodePadding,
            minLinkWidth: 4);

        result.Links.Single(link => link.Link.SourceId == "B").Width.Should().Be(4);
        result.Nodes.Single(node => node.Node.Id == "B").Height.Should().BeLessThan(4);
    }

    [Fact]
    public void Layout_ReturnsCycleErrorForCyclicGraph()
    {
        var data = Data(
            [Node("A"), Node("B")],
            [
                Link("A", "B", 10),
                Link("B", "A", 10),
            ]);

        var result = Layout(data);

        result.IsValid.Should().BeFalse();
        result.ErrorKind.Should().Be(SankeyLayoutErrorKind.Cycle);
        result.Nodes.Should().BeEmpty();
        result.Links.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidData))]
    public void Layout_ReturnsInvalidDataForMalformedGraph(SankeyData data)
    {
        var result = Layout(data);

        result.IsValid.Should().BeFalse();
        result.ErrorKind.Should().Be(SankeyLayoutErrorKind.InvalidData);
        result.Nodes.Should().BeEmpty();
        result.Links.Should().BeEmpty();
    }

    [Fact]
    public void Layout_ReturnsEmptyValidResultForEmptyData()
    {
        var result = Layout(Data([], []));

        result.IsValid.Should().BeTrue();
        result.ErrorKind.Should().Be(SankeyLayoutErrorKind.None);
        result.Nodes.Should().BeEmpty();
        result.Links.Should().BeEmpty();
    }

    public static TheoryData<SankeyData> InvalidData => new()
    {
        Data([Node("A"), Node("A")], []),
        Data([Node("A")], [Link("A", "missing", 1)]),
        Data([Node("A"), Node("B")], [Link("A", "B", -1)]),
        Data([Node("A"), Node("B")], [Link("A", "B", 0)]),
        Data([Node("A"), Node("B")], [Link("A", "B", double.NaN)]),
    };

    private static SankeyLayoutResult Layout(SankeyData data) =>
        SankeyLayoutEngine.Layout(
            data,
            Width,
            Height,
            NodeWidth,
            NodePadding,
            MinLinkWidth);

    private static SankeyData Data(
        IReadOnlyList<SankeyNode> nodes,
        IReadOnlyList<SankeyLink> links) =>
        new()
        {
            Nodes = nodes,
            Links = links,
        };

    private static SankeyNode Node(string id) =>
        new()
        {
            Id = id,
            Label = id,
        };

    private static SankeyLink Link(string sourceId, string targetId, double value) =>
        new()
        {
            SourceId = sourceId,
            TargetId = targetId,
            Value = value,
        };
}
