using FluentAssertions;
using Tempo.Blazor.Components.Wireframe.Commands;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class WireframePhase1CommandTests
{
    private static WireframeDocument EmptyDoc() => new()
    {
        Title = "Test", Width = 800, Height = 600
    };

    private static WireframeElement MakeEl(string type = "TmButton", double x = 0, double y = 0)
        => new() { Type = type, X = x, Y = y, W = 120, H = 36 };

    // ── BringToFrontCommand ───────────────────────────────────────────────────

    [Fact]
    public void BringToFrontCommand_SingleElement_AssignsMaxPlusOne()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(); el1.ZIndex = 0;
        var el2 = MakeEl(); el2.ZIndex = 5;
        doc.Elements.AddRange([el1, el2]);

        var cmd = new BringToFrontCommand(doc, [el1.Id]);
        cmd.Execute();

        el1.ZIndex.Should().Be(6);
        el2.ZIndex.Should().Be(5);
    }

    [Fact]
    public void BringToFrontCommand_MultiElement_AssignsIncrementalZ()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(); el1.ZIndex = 0;
        var el2 = MakeEl(); el2.ZIndex = 3;
        doc.Elements.AddRange([el1, el2]);

        var cmd = new BringToFrontCommand(doc, [el1.Id, el2.Id]);
        cmd.Execute();

        el1.ZIndex.Should().Be(4);
        el2.ZIndex.Should().Be(5);
    }

    [Fact]
    public void BringToFrontCommand_UndoRestoresOriginalZ()
    {
        var doc = EmptyDoc();
        var el = MakeEl(); el.ZIndex = 2;
        doc.Elements.Add(el);

        var cmd = new BringToFrontCommand(doc, [el.Id]);
        cmd.Execute();
        cmd.Undo();

        el.ZIndex.Should().Be(2);
    }

    // ── SendToBackCommand ─────────────────────────────────────────────────────

    [Fact]
    public void SendToBackCommand_SingleElement_AssignsMinMinusOne()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(); el1.ZIndex = 5;
        var el2 = MakeEl(); el2.ZIndex = 10;
        doc.Elements.AddRange([el1, el2]);

        var cmd = new SendToBackCommand(doc, [el2.Id]);
        cmd.Execute();

        el2.ZIndex.Should().Be(4);
        el1.ZIndex.Should().Be(5);
    }

    [Fact]
    public void SendToBackCommand_UndoRestoresOriginalZ()
    {
        var doc = EmptyDoc();
        var el = MakeEl(); el.ZIndex = 5;
        doc.Elements.Add(el);

        var cmd = new SendToBackCommand(doc, [el.Id]);
        cmd.Execute();
        cmd.Undo();

        el.ZIndex.Should().Be(5);
    }

    // ── LockElementsCommand ───────────────────────────────────────────────────

    [Fact]
    public void LockElementsCommand_SetsIsLockedTrue()
    {
        var doc = EmptyDoc();
        var el = MakeEl();
        doc.Elements.Add(el);

        var cmd = new LockElementsCommand(doc, [el.Id]);
        cmd.Execute();

        el.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void LockElementsCommand_UndoRestoresUnlocked()
    {
        var doc = EmptyDoc();
        var el = MakeEl();
        doc.Elements.Add(el);

        var cmd = new LockElementsCommand(doc, [el.Id]);
        cmd.Execute();
        cmd.Undo();

        el.IsLocked.Should().BeFalse();
    }

    // ── UnlockElementsCommand ─────────────────────────────────────────────────

    [Fact]
    public void UnlockElementsCommand_SetsIsLockedFalse()
    {
        var doc = EmptyDoc();
        var el = MakeEl(); el.IsLocked = true;
        doc.Elements.Add(el);

        var cmd = new UnlockElementsCommand(doc, [el.Id]);
        cmd.Execute();

        el.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void UnlockElementsCommand_UndoRestoresLocked()
    {
        var doc = EmptyDoc();
        var el = MakeEl(); el.IsLocked = true;
        doc.Elements.Add(el);

        var cmd = new UnlockElementsCommand(doc, [el.Id]);
        cmd.Execute();
        cmd.Undo();

        el.IsLocked.Should().BeTrue();
    }

    // ── MoveElementsCommand skips locked ──────────────────────────────────────

    [Fact]
    public void MoveElementsCommand_SkipsLockedElement()
    {
        var doc = EmptyDoc();
        var el = MakeEl(x: 10, y: 20);
        el.IsLocked = true;
        doc.Elements.Add(el);

        var before = new Dictionary<string, (double X, double Y)> { [el.Id] = (10, 20) };
        var after = new Dictionary<string, (double X, double Y)> { [el.Id] = (50, 60) };
        var cmd = new MoveElementsCommand(doc, before, after);
        cmd.Execute();

        el.X.Should().Be(10);
        el.Y.Should().Be(20);
    }

    // ── ResizeElementCommand skips locked ─────────────────────────────────────

    [Fact]
    public void ResizeElementCommand_SkipsLockedElement()
    {
        var doc = EmptyDoc();
        var el = MakeEl();
        el.IsLocked = true;
        doc.Elements.Add(el);

        var cmd = new ResizeElementCommand(doc, el.Id, 0, 0, 120, 36, 50, 50, 200, 100);
        cmd.Execute();

        el.X.Should().Be(0);
        el.Y.Should().Be(0);
        el.W.Should().Be(120);
        el.H.Should().Be(36);
    }

    // ── RemoveElementsCommand skips locked ────────────────────────────────────

    [Fact]
    public void RemoveElementsCommand_SkipsLockedElement()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl();
        var el2 = MakeEl(); el2.IsLocked = true;
        doc.Elements.AddRange([el1, el2]);

        var cmd = new RemoveElementsCommand(doc, [el1.Id, el2.Id]);
        cmd.Execute();

        doc.Elements.Should().ContainSingle(e => e.Id == el2.Id);
    }

    // ── RotateElementCommand ──────────────────────────────────────────────────

    [Fact]
    public void RotateElementCommand_SetsRotation()
    {
        var doc = EmptyDoc();
        var el = MakeEl();
        doc.Elements.Add(el);

        var cmd = new RotateElementCommand(doc, el.Id, 0, 45);
        cmd.Execute();

        el.Rotation.Should().Be(45);
    }

    [Fact]
    public void RotateElementCommand_UndoRestoresRotation()
    {
        var doc = EmptyDoc();
        var el = MakeEl(); el.Rotation = 30;
        doc.Elements.Add(el);

        var cmd = new RotateElementCommand(doc, el.Id, 30, 90);
        cmd.Execute();
        cmd.Undo();

        el.Rotation.Should().Be(30);
    }

    // ── BulkRotateCommand ─────────────────────────────────────────────────────

    [Fact]
    public void BulkRotateCommand_SetsSameRotationOnAll()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(); el1.Rotation = 0;
        var el2 = MakeEl(); el2.Rotation = 15;
        doc.Elements.AddRange([el1, el2]);

        var before = new Dictionary<string, double> { [el1.Id] = 0, [el2.Id] = 15 };
        var cmd = new BulkRotateCommand(doc, [el1.Id, el2.Id], before, 45);
        cmd.Execute();

        el1.Rotation.Should().Be(45);
        el2.Rotation.Should().Be(45);
    }

    [Fact]
    public void BulkRotateCommand_UndoRestoresIndividualRotations()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(); el1.Rotation = 0;
        var el2 = MakeEl(); el2.Rotation = 15;
        doc.Elements.AddRange([el1, el2]);

        var before = new Dictionary<string, double> { [el1.Id] = 0, [el2.Id] = 15 };
        var cmd = new BulkRotateCommand(doc, [el1.Id, el2.Id], before, 45);
        cmd.Execute();
        cmd.Undo();

        el1.Rotation.Should().Be(0);
        el2.Rotation.Should().Be(15);
    }

    // ── WireframeElement new defaults ─────────────────────────────────────────

    [Fact]
    public void NewElement_IsLocked_DefaultsToFalse()
    {
        new WireframeElement().IsLocked.Should().BeFalse();
    }

    [Fact]
    public void NewElement_Rotation_DefaultsToZero()
    {
        new WireframeElement().Rotation.Should().Be(0);
    }

    [Fact]
    public void NewElement_LayerId_DefaultsToNull()
    {
        new WireframeElement().LayerId.Should().BeNull();
    }
}
