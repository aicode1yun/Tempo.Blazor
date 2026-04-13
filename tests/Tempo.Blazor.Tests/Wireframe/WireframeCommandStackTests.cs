using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Components.Wireframe.Commands;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class WireframeCommandStackTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WireframeDocument EmptyDoc() => new()
    {
        Title = "Test", Width = 800, Height = 600, Elements = []
    };

    private static WireframeElement MakeEl(string type = "TmButton", double x = 0, double y = 0)
        => new() { Type = type, X = x, Y = y, W = 120, H = 36 };

    // ── WireframeCommandStack basics ──────────────────────────────────────────

    [Fact]
    public void Push_ExecutesCommandAndAddsToUndoStack()
    {
        var doc   = EmptyDoc();
        var stack = new WireframeCommandStack();
        var el    = MakeEl();

        stack.Push(new AddElementCommand(doc, el));

        doc.Elements.Should().ContainSingle(e => e.Id == el.Id);
        stack.CanUndo.Should().BeTrue();
        stack.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Undo_ReversesCommand()
    {
        var doc   = EmptyDoc();
        var stack = new WireframeCommandStack();
        var el    = MakeEl();

        stack.Push(new AddElementCommand(doc, el));
        stack.Undo();

        doc.Elements.Should().BeEmpty();
        stack.CanUndo.Should().BeFalse();
        stack.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void Redo_ReappliesCommand()
    {
        var doc   = EmptyDoc();
        var stack = new WireframeCommandStack();
        var el    = MakeEl();

        stack.Push(new AddElementCommand(doc, el));
        stack.Undo();
        stack.Redo();

        doc.Elements.Should().ContainSingle(e => e.Id == el.Id);
        stack.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Push_ClearsRedoStack()
    {
        var doc   = EmptyDoc();
        var stack = new WireframeCommandStack();
        var el1   = MakeEl();
        var el2   = MakeEl();

        stack.Push(new AddElementCommand(doc, el1));
        stack.Undo();
        stack.Push(new AddElementCommand(doc, el2)); // new branch

        stack.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Stack_RespectsMaxDepth()
    {
        var doc   = EmptyDoc();
        var stack = new WireframeCommandStack(maxDepth: 3);

        for (var i = 0; i < 5; i++)
            stack.Push(new AddElementCommand(doc, MakeEl()));

        // Undo 3 times should work; 4th should not
        stack.Undo(); stack.Undo(); stack.Undo();
        stack.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void OnStackChanged_FiredOnPushUndoRedo()
    {
        var doc   = EmptyDoc();
        var stack = new WireframeCommandStack();
        var el    = MakeEl();
        var count = 0;
        stack.OnStackChanged += () => count++;

        stack.Push(new AddElementCommand(doc, el));
        stack.Undo();
        stack.Redo();

        count.Should().Be(3);
    }

    [Fact]
    public void Clear_EmptiesBothStacks()
    {
        var doc   = EmptyDoc();
        var stack = new WireframeCommandStack();
        stack.Push(new AddElementCommand(doc, MakeEl()));
        stack.Clear();

        stack.CanUndo.Should().BeFalse();
        stack.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void NextUndoName_ReturnsLastCommandName()
    {
        var doc   = EmptyDoc();
        var stack = new WireframeCommandStack();
        var el    = MakeEl("TmButton");
        stack.Push(new AddElementCommand(doc, el));

        stack.NextUndoName.Should().Be("Add TmButton");
    }

    // ── AddElementCommand ─────────────────────────────────────────────────────

    [Fact]
    public void AddElementCommand_ExecuteAddsElement()
    {
        var doc = EmptyDoc();
        var el  = MakeEl();
        var cmd = new AddElementCommand(doc, el);
        cmd.Execute();
        doc.Elements.Should().Contain(el);
    }

    [Fact]
    public void AddElementCommand_UndoRemovesElement()
    {
        var doc = EmptyDoc();
        var el  = MakeEl();
        var cmd = new AddElementCommand(doc, el);
        cmd.Execute();
        cmd.Undo();
        doc.Elements.Should().BeEmpty();
    }

    // ── RemoveElementsCommand ─────────────────────────────────────────────────

    [Fact]
    public void RemoveElementsCommand_ExecuteRemovesElements()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(); var el2 = MakeEl();
        doc.Elements.AddRange([el1, el2]);

        var cmd = new RemoveElementsCommand(doc, [el1.Id, el2.Id]);
        cmd.Execute();

        doc.Elements.Should().BeEmpty();
    }

    [Fact]
    public void RemoveElementsCommand_UndoRestoresElements()
    {
        var doc = EmptyDoc();
        var el  = MakeEl();
        doc.Elements.Add(el);

        var cmd = new RemoveElementsCommand(doc, [el.Id]);
        cmd.Execute();
        cmd.Undo();

        doc.Elements.Should().ContainSingle(e => e.Id == el.Id);
    }

    // ── MoveElementsCommand ───────────────────────────────────────────────────

    [Fact]
    public void MoveElementsCommand_ExecuteMovesElement()
    {
        var doc = EmptyDoc();
        var el  = MakeEl(x: 10, y: 20);
        doc.Elements.Add(el);

        var before = new Dictionary<string, (double X, double Y)> { [el.Id] = (10, 20) };
        var after  = new Dictionary<string, (double X, double Y)> { [el.Id] = (50, 60) };
        var cmd    = new MoveElementsCommand(doc, before, after);
        cmd.Execute();

        el.X.Should().Be(50); el.Y.Should().Be(60);
    }

    [Fact]
    public void MoveElementsCommand_UndoRestoresPosition()
    {
        var doc = EmptyDoc();
        var el  = MakeEl(x: 10, y: 20);
        doc.Elements.Add(el);

        var before = new Dictionary<string, (double X, double Y)> { [el.Id] = (10, 20) };
        var after  = new Dictionary<string, (double X, double Y)> { [el.Id] = (50, 60) };
        var cmd    = new MoveElementsCommand(doc, before, after);
        cmd.Execute();
        cmd.Undo();

        el.X.Should().Be(10); el.Y.Should().Be(20);
    }

    [Fact]
    public void MoveElementsCommand_CoalescesMergesAfterPositions()
    {
        var doc   = EmptyDoc();
        var el    = MakeEl(x: 0, y: 0);
        doc.Elements.Add(el);
        var stack = new WireframeCommandStack();

        var before1 = new Dictionary<string, (double X, double Y)> { [el.Id] = (0, 0) };
        var after1  = new Dictionary<string, (double X, double Y)> { [el.Id] = (10, 10) };
        stack.Push(new MoveElementsCommand(doc, before1, after1));

        // Immediately push a second move (within 100 ms) → should coalesce
        var before2 = new Dictionary<string, (double X, double Y)> { [el.Id] = (10, 10) };
        var after2  = new Dictionary<string, (double X, double Y)> { [el.Id] = (20, 20) };
        stack.Push(new MoveElementsCommand(doc, before2, after2));

        // Only one entry in undo stack
        stack.Undo();
        stack.CanUndo.Should().BeFalse("coalesced into single undo step");

        // After undo position should be original (0,0) not intermediate (10,10)
        el.X.Should().Be(0); el.Y.Should().Be(0);
    }

    // ── ResizeElementCommand ──────────────────────────────────────────────────

    [Fact]
    public void ResizeElementCommand_ExecuteAndUndo()
    {
        var doc = EmptyDoc();
        var el  = new WireframeElement { Type = "TmCard", X = 10, Y = 20, W = 100, H = 50 };
        doc.Elements.Add(el);

        var cmd = new ResizeElementCommand(doc, el.Id, 10, 20, 100, 50, 30, 40, 200, 80);
        cmd.Execute();
        el.X.Should().Be(30); el.Y.Should().Be(40); el.W.Should().Be(200); el.H.Should().Be(80);

        cmd.Undo();
        el.X.Should().Be(10); el.Y.Should().Be(20); el.W.Should().Be(100); el.H.Should().Be(50);
    }

    // ── UpdatePropsCommand ────────────────────────────────────────────────────

    [Fact]
    public void UpdatePropsCommand_ExecuteAndUndo()
    {
        var doc = EmptyDoc();
        var el  = MakeEl();
        el.Props["label"] = JsonSerializer.SerializeToElement("Old");
        doc.Elements.Add(el);

        var changes = new Dictionary<string, JsonElement?>
        {
            ["label"] = JsonSerializer.SerializeToElement("New")
        };
        var cmd = new UpdatePropsCommand(doc, el.Id, changes);
        cmd.Execute();
        el.Props.GetString("label").Should().Be("New");

        cmd.Undo();
        el.Props.GetString("label").Should().Be("Old");
    }

    [Fact]
    public void UpdatePropsCommand_UndoRestoresAbsentKey()
    {
        var doc = EmptyDoc();
        var el  = MakeEl();
        doc.Elements.Add(el);

        var changes = new Dictionary<string, JsonElement?>
        {
            ["newProp"] = JsonSerializer.SerializeToElement("value")
        };
        var cmd = new UpdatePropsCommand(doc, el.Id, changes);
        cmd.Execute();
        el.Props.Should().ContainKey("newProp");

        cmd.Undo();
        el.Props.Should().NotContainKey("newProp");
    }

    // ── BulkUpdateCommand ─────────────────────────────────────────────────────

    [Fact]
    public void BulkUpdateCommand_UpdatesAllElements()
    {
        var doc  = EmptyDoc();
        var el1  = MakeEl(); el1.Props["label"] = JsonSerializer.SerializeToElement("A");
        var el2  = MakeEl(); el2.Props["label"] = JsonSerializer.SerializeToElement("B");
        doc.Elements.AddRange([el1, el2]);

        var changes = new Dictionary<string, JsonElement?>
        {
            ["label"] = JsonSerializer.SerializeToElement("Shared")
        };
        var cmd = new BulkUpdateCommand(doc, [el1.Id, el2.Id], changes);
        cmd.Execute();

        el1.Props.GetString("label").Should().Be("Shared");
        el2.Props.GetString("label").Should().Be("Shared");
    }

    [Fact]
    public void BulkUpdateCommand_UndoRestoresIndividualValues()
    {
        var doc  = EmptyDoc();
        var el1  = MakeEl(); el1.Props["label"] = JsonSerializer.SerializeToElement("A");
        var el2  = MakeEl(); el2.Props["label"] = JsonSerializer.SerializeToElement("B");
        doc.Elements.AddRange([el1, el2]);

        var changes = new Dictionary<string, JsonElement?>
        {
            ["label"] = JsonSerializer.SerializeToElement("Shared")
        };
        var cmd = new BulkUpdateCommand(doc, [el1.Id, el2.Id], changes);
        cmd.Execute();
        cmd.Undo();

        el1.Props.GetString("label").Should().Be("A");
        el2.Props.GetString("label").Should().Be("B");
    }
}
