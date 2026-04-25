using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Commands;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// Tests for connector model, commands, and renderer (Phase 7).
/// </summary>
public class WireframeConnectorTests
{
    // ── Model ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Connector_Defaults_AreExpected()
    {
        var c = new WireframeConnector();

        c.Id.Should().NotBeNullOrEmpty();
        c.Routing.Should().Be("straight");
        c.StartArrow.Should().Be("none");
        c.EndArrow.Should().Be("classic");
        c.Stroke.Should().Be("#94a3b8");
        c.StrokeWidth.Should().Be(2);
        c.StrokeDasharray.Should().BeNull();
        c.ZIndex.Should().Be(0);
        c.Waypoints.Should().BeEmpty();
    }

    [Fact]
    public void Connector_DeepCopy_CreatesNewId()
    {
        var c = new WireframeConnector
        {
            FromId = "a",
            ToId = "b",
            Label = "test",
            Routing = "curved",
            Waypoints = { new DiagramPoint(10, 20) },
            StartArrow = "block",
            EndArrow = "open",
            Stroke = "#ff0000",
            StrokeWidth = 4,
            ZIndex = 5,
        };

        var copy = c.DeepCopy();

        copy.Id.Should().NotBe(c.Id);
        copy.FromId.Should().Be(c.FromId);
        copy.ToId.Should().Be(c.ToId);
        copy.Label.Should().Be(c.Label);
        copy.Routing.Should().Be(c.Routing);
        copy.Waypoints.Should().HaveCount(1);
        copy.Waypoints[0].X.Should().Be(10);
        copy.Waypoints[0].Y.Should().Be(20);
        copy.StartArrow.Should().Be(c.StartArrow);
        copy.EndArrow.Should().Be(c.EndArrow);
        copy.Stroke.Should().Be(c.Stroke);
        copy.StrokeWidth.Should().Be(c.StrokeWidth);
        copy.ZIndex.Should().Be(c.ZIndex);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddConnectorCommand_AddsAndRemoves()
    {
        var doc = CreateDocWithElements();
        var c = new WireframeConnector { FromId = "e1", ToId = "e2" };

        var cmd = new AddConnectorCommand(doc, c);
        cmd.Execute();
        doc.Connectors.Should().Contain(c);

        cmd.Undo();
        doc.Connectors.Should().NotContain(c);
    }

    [Fact]
    public void RemoveConnectorsCommand_RemovesAndRestores()
    {
        var doc = CreateDocWithElements();
        var c1 = new WireframeConnector { FromId = "e1", ToId = "e2" };
        var c2 = new WireframeConnector { FromId = "e2", ToId = "e1" };
        doc.Connectors.Add(c1);
        doc.Connectors.Add(c2);

        var cmd = new RemoveConnectorsCommand(doc, [c1.Id]);
        cmd.Execute();
        doc.Connectors.Should().NotContain(c1);
        doc.Connectors.Should().Contain(c2);

        cmd.Undo();
        doc.Connectors.Should().Contain(c1);
        doc.Connectors.Should().Contain(c2);
    }

    [Fact]
    public void UpdateConnectorRoutingCommand_ChangesRouting()
    {
        var doc = CreateDocWithElements();
        var c = new WireframeConnector { FromId = "e1", ToId = "e2", Routing = "straight" };
        doc.Connectors.Add(c);

        var cmd = new UpdateConnectorRoutingCommand(doc, c.Id, "straight", [], "orthogonal", []);
        cmd.Execute();
        c.Routing.Should().Be("orthogonal");

        cmd.Undo();
        c.Routing.Should().Be("straight");
    }

    [Fact]
    public void UpdateConnectorWaypointsCommand_ChangesWaypoints()
    {
        var doc = CreateDocWithElements();
        var c = new WireframeConnector { FromId = "e1", ToId = "e2" };
        doc.Connectors.Add(c);
        var before = new List<DiagramPoint>();
        var after = new List<DiagramPoint> { new DiagramPoint(50, 50) };

        var cmd = new UpdateConnectorWaypointsCommand(doc, c.Id, before, after);
        cmd.Execute();
        c.Waypoints.Should().HaveCount(1);

        cmd.Undo();
        c.Waypoints.Should().BeEmpty();
    }

    [Fact]
    public void UpdateConnectorStyleCommand_ChangesStyle()
    {
        var doc = CreateDocWithElements();
        var c = new WireframeConnector { FromId = "e1", ToId = "e2", Stroke = "#000", StrokeWidth = 1, StartArrow = "none", EndArrow = "none" };
        doc.Connectors.Add(c);

        var cmd = new UpdateConnectorStyleCommand(doc, c.Id,
            "#000", 1, null, "none", "none",
            "#fff", 3, "4 2", "block", "classic");
        cmd.Execute();
        c.Stroke.Should().Be("#fff");
        c.StrokeWidth.Should().Be(3);
        c.StrokeDasharray.Should().Be("4 2");
        c.StartArrow.Should().Be("block");
        c.EndArrow.Should().Be("classic");

        cmd.Undo();
        c.Stroke.Should().Be("#000");
        c.StrokeWidth.Should().Be(1);
        c.StrokeDasharray.Should().BeNull();
        c.StartArrow.Should().Be("none");
        c.EndArrow.Should().Be("none");
    }

    [Fact]
    public void UpdateConnectorLabelCommand_ChangesLabel()
    {
        var doc = CreateDocWithElements();
        var c = new WireframeConnector { FromId = "e1", ToId = "e2", Label = "old" };
        doc.Connectors.Add(c);

        var cmd = new UpdateConnectorLabelCommand(doc, c.Id, "old", "new");
        cmd.Execute();
        c.Label.Should().Be("new");

        cmd.Undo();
        c.Label.Should().Be("old");
    }

    // ── Renderer ──────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeEndpoints_Horizontal_RightToLeft()
    {
        var source = new WireframeElement { Id = "s", X = 0, Y = 0, W = 100, H = 50 };
        var target = new WireframeElement { Id = "t", X = 200, Y = 0, W = 100, H = 50 };

        var (sPt, tPt) = WireframeConnectorRenderer.ComputeEndpoints(source, target);

        sPt.X.Should().BeApproximately(100, 0.1);
        sPt.Y.Should().BeApproximately(25, 0.1);
        tPt.X.Should().BeApproximately(200, 0.1);
        tPt.Y.Should().BeApproximately(25, 0.1);
    }

    [Fact]
    public void ComputeEndpoints_Vertical_BottomToTop()
    {
        var source = new WireframeElement { Id = "s", X = 0, Y = 0, W = 100, H = 50 };
        var target = new WireframeElement { Id = "t", X = 0, Y = 200, W = 100, H = 50 };

        var (sPt, tPt) = WireframeConnectorRenderer.ComputeEndpoints(source, target);

        sPt.X.Should().BeApproximately(50, 0.1);
        sPt.Y.Should().BeApproximately(50, 0.1);
        tPt.X.Should().BeApproximately(50, 0.1);
        tPt.Y.Should().BeApproximately(200, 0.1);
    }

    [Fact]
    public void BuildPath_Straight_ReturnsSimplePath()
    {
        var source = new WireframeElement { Id = "s", X = 0, Y = 0, W = 100, H = 50 };
        var target = new WireframeElement { Id = "t", X = 200, Y = 0, W = 100, H = 50 };
        var c = new WireframeConnector { FromId = "s", ToId = "t", Routing = "straight" };

        var path = WireframeConnectorRenderer.BuildPath(c, source, target);

        path.Should().StartWith("M");
        path.Should().Contain("L");
    }

    [Fact]
    public void BuildPath_Orthogonal_ReturnsManhattanPath()
    {
        var source = new WireframeElement { Id = "s", X = 0, Y = 0, W = 100, H = 50 };
        var target = new WireframeElement { Id = "t", X = 200, Y = 100, W = 100, H = 50 };
        var c = new WireframeConnector { FromId = "s", ToId = "t", Routing = "orthogonal" };

        var path = WireframeConnectorRenderer.BuildPath(c, source, target);

        path.Should().StartWith("M");
        path.Should().Contain("L");
    }

    [Fact]
    public void BuildPath_Curved_ReturnsBezierPath()
    {
        var source = new WireframeElement { Id = "s", X = 0, Y = 0, W = 100, H = 50 };
        var target = new WireframeElement { Id = "t", X = 200, Y = 0, W = 100, H = 50 };
        var c = new WireframeConnector { FromId = "s", ToId = "t", Routing = "curved" };

        var path = WireframeConnectorRenderer.BuildPath(c, source, target);

        path.Should().StartWith("M");
        path.Should().Contain("C");
    }

    [Fact]
    public void BuildArrowMarkers_ClassicEnd_GeneratesMarker()
    {
        var c = new WireframeConnector { Id = "abc", EndArrow = "classic", Stroke = "#000" };
        var markers = WireframeConnectorRenderer.BuildArrowMarkers(c);

        markers.Should().Contain("<marker");
        markers.Should().Contain("id=\"tm-wd-arrow-end-classic-abc\"");
    }

    [Fact]
    public void GetArrowMarkerRef_None_ReturnsNull()
    {
        WireframeConnectorRenderer.GetArrowMarkerRef("none", true, "x")
            .Should().BeNull();
    }

    [Fact]
    public void GetArrowMarkerRef_Classic_ReturnsUrl()
    {
        WireframeConnectorRenderer.GetArrowMarkerRef("classic", false, "x")
            .Should().Be("url(#tm-wd-arrow-end-classic-x)");
    }

    [Fact]
    public void UpdateConnectorWaypointsCommand_AddWaypoint_AppendsToList()
    {
        var doc = CreateDocWithElements();
        var c = new WireframeConnector { FromId = "e1", ToId = "e2" };
        doc.Connectors.Add(c);
        var before = new List<DiagramPoint> { new DiagramPoint(10, 10) };
        var after = new List<DiagramPoint> { new DiagramPoint(10, 10), new DiagramPoint(50, 50) };

        var cmd = new UpdateConnectorWaypointsCommand(doc, c.Id, before, after);
        cmd.Execute();
        c.Waypoints.Should().HaveCount(2);
        c.Waypoints[1].X.Should().Be(50);
        c.Waypoints[1].Y.Should().Be(50);

        cmd.Undo();
        c.Waypoints.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateConnectorWaypointsCommand_DragConnector_MovesAllWaypoints()
    {
        var doc = CreateDocWithElements();
        var c = new WireframeConnector { FromId = "e1", ToId = "e2" };
        doc.Connectors.Add(c);
        var before = new List<DiagramPoint>
        {
            new DiagramPoint(10, 20),
            new DiagramPoint(30, 40),
        };
        c.Waypoints = before.ToList();
        var after = new List<DiagramPoint>
        {
            new DiagramPoint(20, 30),
            new DiagramPoint(40, 50),
        };

        var cmd = new UpdateConnectorWaypointsCommand(doc, c.Id, before, after);
        cmd.Execute();
        c.Waypoints[0].X.Should().Be(20);
        c.Waypoints[0].Y.Should().Be(30);
        c.Waypoints[1].X.Should().Be(40);
        c.Waypoints[1].Y.Should().Be(50);

        cmd.Undo();
        c.Waypoints[0].X.Should().Be(10);
        c.Waypoints[1].Y.Should().Be(40);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WireframeDocument CreateDocWithElements()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Id = "e1", X = 0, Y = 0, W = 100, H = 50 });
        doc.Elements.Add(new WireframeElement { Id = "e2", X = 200, Y = 0, W = 100, H = 50 });
        return doc;
    }
}
