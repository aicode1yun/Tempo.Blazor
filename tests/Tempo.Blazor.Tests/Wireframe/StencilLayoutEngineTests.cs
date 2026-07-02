using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilLayoutEngineTests
{
    [Fact]
    public void Stack_SpacesChildrenByGap()
    {
        var container = Node(RenderNodeKind.Stack, ("gap", 8), ("padding", 0));
        var sizes = Enumerable.Repeat(new LayoutRect(0, 0, 40, 20), 3).ToArray();

        var rects = StencilLayoutEngine.LayoutChildren(container, 120, 120, sizes);

        rects.Select(x => x.Y).Should().Equal(0, 28, 56);
    }

    [Fact]
    public void Grid_PlacesChildrenInColumns()
    {
        var container = Node(RenderNodeKind.Grid, ("columns", 3), ("gap", 0), ("padding", 0));
        var sizes = Enumerable.Repeat(new LayoutRect(0, 0, 20, 20), 4).ToArray();

        var rects = StencilLayoutEngine.LayoutChildren(container, 300, 120, sizes);

        rects[0].X.Should().Be(0);
        rects[0].W.Should().Be(100);
        rects[3].X.Should().Be(rects[0].X);
        rects[3].Y.Should().BeGreaterThan(0);
    }

    [Fact]
    public void NineSlice_KeepsCornerGeometryConstant_AsWidthGrows()
    {
        var small = StencilLayoutEngine.NineSlice(100, 80, new SliceInsets(16, 16, 16, 16));
        var large = StencilLayoutEngine.NineSlice(400, 80, new SliceInsets(16, 16, 16, 16));

        foreach (var name in new[] { "top-left", "top-right", "bottom-left", "bottom-right" })
        {
            var smallCorner = small.Single(x => x.Name == name).Rect;
            var largeCorner = large.Single(x => x.Name == name).Rect;
            largeCorner.W.Should().Be(smallCorner.W);
            largeCorner.H.Should().Be(smallCorner.H);
        }

        large.Single(x => x.Name == "top").Rect.W
            .Should().BeGreaterThan(small.Single(x => x.Name == "top").Rect.W);
    }

    [Fact]
    public void RightAnchoredNode_PinsToElementWidth()
    {
        var node = Node(RenderNodeKind.Rect, ("w", 40), ("h", 20), ("anchor", "right"), ("margin.right", 10));

        StencilLayoutEngine.ApplyAnchors(node, 200, 80).X.Should().Be(150);
        StencilLayoutEngine.ApplyAnchors(node, 400, 80).X.Should().Be(350);
    }

    [Fact]
    public void StretchAnchoredNode_FillsBetweenMargins()
    {
        var node = Node(RenderNodeKind.Rect, ("h", 20), ("anchor", "stretch"), ("margin.left", 12), ("margin.right", 8));

        var rect = StencilLayoutEngine.ApplyAnchors(node, 200, 80);

        rect.X.Should().Be(12);
        rect.W.Should().Be(180);
    }

    private static RenderNode Node(RenderNodeKind kind, params (string Key, object? Value)[] attributes)
        => new()
        {
            Kind = kind,
            Attributes = attributes.ToDictionary(x => x.Key, x => x.Value)
        };
}
