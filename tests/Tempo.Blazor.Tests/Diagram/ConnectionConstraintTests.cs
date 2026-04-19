using FluentAssertions;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class ConnectionConstraintTests
{
    [Fact]
    public void GetEdgePoints_WithSourceConstraint_UsesConstraintPosition()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { Id = "n1", X = 100, Y = 100, W = 50, H = 50 };
        doc.Nodes.Add(node);

        var edge = new DiagramEdge
        {
            SourceNodeId = "n1",
            SourceConstraint = new DiagramConnectionConstraint { RelativeX = 0.5, RelativeY = 0.0 },
            TargetNodeId = "n1",
        };
        doc.Edges.Add(edge);

        var pts = DiagramGeometryHelper.GetEdgePoints(doc, edge);
        pts.Should().HaveCount(2);
        pts[0].X.Should().Be(125); // 100 + 50*0.5
        pts[0].Y.Should().Be(100); // 100 + 50*0.0
    }

    [Fact]
    public void GetEdgePoints_WithTargetConstraint_UsesConstraintPosition()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { Id = "n1", X = 100, Y = 100, W = 50, H = 50 };
        doc.Nodes.Add(node);

        var edge = new DiagramEdge
        {
            SourceNodeId = "n1",
            TargetNodeId = "n1",
            TargetConstraint = new DiagramConnectionConstraint { RelativeX = 1.0, RelativeY = 0.5 },
        };
        doc.Edges.Add(edge);

        var pts = DiagramGeometryHelper.GetEdgePoints(doc, edge);
        pts.Should().HaveCount(2);
        pts[^1].X.Should().Be(150); // 100 + 50*1.0
        pts[^1].Y.Should().Be(125); // 100 + 50*0.5
    }

    [Theory]
    [InlineData(0.0, 0.5, PortSide.Left)]
    [InlineData(1.0, 0.5, PortSide.Right)]
    [InlineData(0.5, 0.0, PortSide.Top)]
    [InlineData(0.5, 1.0, PortSide.Bottom)]
    [InlineData(0.5, 0.5, PortSide.Right)]
    public void InferSideFromConstraint_ReturnsExpectedSide(double cx, double cy, PortSide expected)
    {
        DiagramGeometryHelper.InferSideFromConstraint(cx, cy).Should().Be(expected);
    }

    [Fact]
    public void GetEdgePoints_ConstraintOverridesPort()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode
        {
            Id = "n1",
            X = 100, Y = 100, W = 50, H = 50,
            Ports = { new DiagramPort { Side = PortSide.Left, Offset = 0.5 } }
        };
        doc.Nodes.Add(node);

        var edge = new DiagramEdge
        {
            SourceNodeId = "n1",
            SourcePortId = node.Ports[0].Id,
            SourceConstraint = new DiagramConnectionConstraint { RelativeX = 0.5, RelativeY = 1.0 },
            TargetNodeId = "n1",
        };
        doc.Edges.Add(edge);

        var pts = DiagramGeometryHelper.GetEdgePoints(doc, edge);
        pts[0].X.Should().Be(125); // constraint, not port
        pts[0].Y.Should().Be(150); // constraint (bottom), not port (left)
    }

    [Fact]
    public void GetEdgePoints_WithSourceConstraint_AppliesSpacing()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { Id = "n1", X = 100, Y = 100, W = 50, H = 50 };
        doc.Nodes.Add(node);

        var edge = new DiagramEdge
        {
            SourceNodeId = "n1",
            SourceConstraint = new DiagramConnectionConstraint { RelativeX = 0.0, RelativeY = 0.5 },
            SourceSpacing = 10,
            TargetNodeId = "n1",
        };
        doc.Edges.Add(edge);

        var pts = DiagramGeometryHelper.GetEdgePoints(doc, edge);
        pts[0].X.Should().Be(90); // 100 - 10 spacing
        pts[0].Y.Should().Be(125);
    }

    [Theory]
    [InlineData(0.5, 1.0, "rectangle", 50, 100)]
    [InlineData(0.5, 0.5, "rectangle", 50, 0)]    // center -> top edge
    [InlineData(1.0, 0.5, "ellipse", 100, 50)]    // right edge of ellipse
    [InlineData(0.5, 0.0, "ellipse", 50, 0)]      // top edge of ellipse
    [InlineData(0.5, 1.0, "diamond", 50, 100)]    // bottom tip of diamond
    [InlineData(1.0, 0.5, "diamond", 100, 50)]    // right tip of diamond
    public void ProjectToPerimeter_ReturnsCorrectPoint(double rx, double ry, string shape, double expectedX, double expectedY)
    {
        var (x, y) = DiagramGeometryHelper.ProjectToPerimeter(100, 100, rx, ry, shape);
        x.Should().BeApproximately(expectedX, 0.1);
        y.Should().BeApproximately(expectedY, 0.1);
    }

    [Fact]
    public void GetEdgePoints_WithPerimeterConstraint_ProjectsOntoShape()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode { Id = "n1", X = 0, Y = 0, W = 100, H = 100, BackgroundShape = "ellipse" };
        doc.Nodes.Add(node);

        var edge = new DiagramEdge
        {
            SourceNodeId = "n1",
            SourceConstraint = new DiagramConnectionConstraint { RelativeX = 0.5, RelativeY = 0.0, Perimeter = true },
            TargetNodeId = "n1",
        };
        doc.Edges.Add(edge);

        var pts = DiagramGeometryHelper.GetEdgePoints(doc, edge);
        pts[0].X.Should().BeApproximately(50, 0.1); // top center of ellipse
        pts[0].Y.Should().BeApproximately(0, 0.1);
    }
}
