using FluentAssertions;
using Tempo.Blazor.Components.Wireframe.Commands;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class WireframePhase3CommandTests
{
    private static WireframeDocument EmptyDoc() => new()
    {
        Title = "Test", Width = 800, Height = 600
    };

    private static WireframeElement MakeEl(string type = "TmButton", double x = 0, double y = 0, double w = 120, double h = 36)
        => new() { Type = type, X = x, Y = y, W = w, H = h };

    // ── AlignElementsCommand ──────────────────────────────────────────────────

    [Fact]
    public void AlignElementsCommand_Left_AlignsToMinX()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(x: 10);
        var el2 = MakeEl(x: 50);
        doc.Elements.AddRange([el1, el2]);

        new AlignElementsCommand(doc, [el1.Id, el2.Id], WireframeAlignment.Left).Execute();

        el1.X.Should().Be(10);
        el2.X.Should().Be(10);
    }

    [Fact]
    public void AlignElementsCommand_Right_AlignsToMaxRight()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(x: 10);
        var el2 = MakeEl(x: 50);
        doc.Elements.AddRange([el1, el2]);

        new AlignElementsCommand(doc, [el1.Id, el2.Id], WireframeAlignment.Right).Execute();

        el1.X.Should().Be(50);
        el2.X.Should().Be(50);
    }

    [Fact]
    public void AlignElementsCommand_CenterH_AlignsToBoundingBoxCenter()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(x: 0, w: 100);
        var el2 = MakeEl(x: 100, w: 100);
        doc.Elements.AddRange([el1, el2]);

        new AlignElementsCommand(doc, [el1.Id, el2.Id], WireframeAlignment.CenterH).Execute();

        // Bounding box: 0 to 200, center = 100
        // el1: 100 - 100/2 = 50
        // el2: 100 - 100/2 = 50
        el1.X.Should().Be(50);
        el2.X.Should().Be(50);
    }

    [Fact]
    public void AlignElementsCommand_Top_AlignsToMinY()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(y: 10);
        var el2 = MakeEl(y: 50);
        doc.Elements.AddRange([el1, el2]);

        new AlignElementsCommand(doc, [el1.Id, el2.Id], WireframeAlignment.Top).Execute();

        el1.Y.Should().Be(10);
        el2.Y.Should().Be(10);
    }

    [Fact]
    public void AlignElementsCommand_Bottom_AlignsToMaxBottom()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(y: 10);
        var el2 = MakeEl(y: 50);
        doc.Elements.AddRange([el1, el2]);

        new AlignElementsCommand(doc, [el1.Id, el2.Id], WireframeAlignment.Bottom).Execute();

        el1.Y.Should().Be(50);
        el2.Y.Should().Be(50);
    }

    [Fact]
    public void AlignElementsCommand_CenterV_AlignsToBoundingBoxCenter()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(y: 0, h: 100);
        var el2 = MakeEl(y: 100, h: 100);
        doc.Elements.AddRange([el1, el2]);

        new AlignElementsCommand(doc, [el1.Id, el2.Id], WireframeAlignment.CenterV).Execute();

        // Bounding box: 0 to 200, center = 100
        el1.Y.Should().Be(50);
        el2.Y.Should().Be(50);
    }

    [Fact]
    public void AlignElementsCommand_UndoRestoresOriginalPositions()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(x: 10, y: 20);
        var el2 = MakeEl(x: 50, y: 60);
        doc.Elements.AddRange([el1, el2]);

        var cmd = new AlignElementsCommand(doc, [el1.Id, el2.Id], WireframeAlignment.Left);
        cmd.Execute();
        cmd.Undo();

        el1.X.Should().Be(10);
        el1.Y.Should().Be(20);
        el2.X.Should().Be(50);
        el2.Y.Should().Be(60);
    }

    [Fact]
    public void AlignElementsCommand_SingleElement_DoesNothing()
    {
        var doc = EmptyDoc();
        var el = MakeEl(x: 10);
        doc.Elements.Add(el);

        new AlignElementsCommand(doc, [el.Id], WireframeAlignment.Left).Execute();

        el.X.Should().Be(10);
    }

    [Fact]
    public void AlignElementsCommand_LockedElement_Skips()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(x: 10);
        var el2 = MakeEl(x: 50);
        el2.IsLocked = true;
        doc.Elements.AddRange([el1, el2]);

        new AlignElementsCommand(doc, [el1.Id, el2.Id], WireframeAlignment.Left).Execute();

        el1.X.Should().Be(10);
        el2.X.Should().Be(50); // unchanged
    }

    // ── DistributeElementsCommand ─────────────────────────────────────────────

    [Fact]
    public void DistributeElementsCommand_Horizontal_EqualCenters()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(x: 0, w: 10);
        var el2 = MakeEl(x: 50, w: 10);
        var el3 = MakeEl(x: 200, w: 10);
        doc.Elements.AddRange([el1, el2, el3]);

        new DistributeElementsCommand(doc, [el1.Id, el2.Id, el3.Id], WireframeDistribution.Horizontal).Execute();

        // Centers: 5, 55, 205 before; after: 5, 105, 205 (step = 100)
        (el1.X + el1.W / 2).Should().BeApproximately(5, 0.1);
        (el2.X + el2.W / 2).Should().BeApproximately(105, 0.1);
        (el3.X + el3.W / 2).Should().BeApproximately(205, 0.1);
    }

    [Fact]
    public void DistributeElementsCommand_Vertical_EqualCenters()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(y: 0, h: 10);
        var el2 = MakeEl(y: 40, h: 10);
        var el3 = MakeEl(y: 150, h: 10);
        doc.Elements.AddRange([el1, el2, el3]);

        new DistributeElementsCommand(doc, [el1.Id, el2.Id, el3.Id], WireframeDistribution.Vertical).Execute();

        // Centers: 5, 45, 155 before; after: 5, 80, 155 (step = 75)
        (el1.Y + el1.H / 2).Should().BeApproximately(5, 0.1);
        (el2.Y + el2.H / 2).Should().BeApproximately(80, 0.1);
        (el3.Y + el3.H / 2).Should().BeApproximately(155, 0.1);
    }

    [Fact]
    public void DistributeElementsCommand_UndoRestoresOriginalPositions()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(x: 0);
        var el2 = MakeEl(x: 50);
        var el3 = MakeEl(x: 200);
        doc.Elements.AddRange([el1, el2, el3]);

        var cmd = new DistributeElementsCommand(doc, [el1.Id, el2.Id, el3.Id], WireframeDistribution.Horizontal);
        cmd.Execute();
        cmd.Undo();

        el1.X.Should().Be(0);
        el2.X.Should().Be(50);
        el3.X.Should().Be(200);
    }

    [Fact]
    public void DistributeElementsCommand_LessThanThree_DoesNothing()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(x: 0);
        var el2 = MakeEl(x: 50);
        doc.Elements.AddRange([el1, el2]);

        new DistributeElementsCommand(doc, [el1.Id, el2.Id], WireframeDistribution.Horizontal).Execute();

        el1.X.Should().Be(0);
        el2.X.Should().Be(50);
    }

    [Fact]
    public void DistributeElementsCommand_LockedElement_Skips()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(x: 0, w: 10);
        var el2 = MakeEl(x: 50, w: 10);
        var el3 = MakeEl(x: 200, w: 10);
        el2.IsLocked = true;
        doc.Elements.AddRange([el1, el2, el3]);

        new DistributeElementsCommand(doc, [el1.Id, el2.Id, el3.Id], WireframeDistribution.Horizontal).Execute();

        el2.X.Should().Be(50); // unchanged
    }
}
