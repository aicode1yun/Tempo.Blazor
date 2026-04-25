using FluentAssertions;
using Tempo.Blazor.Components.Wireframe.Commands;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class WireframePhase2CommandTests
{
    private static WireframeDocument EmptyDoc() => new()
    {
        Title = "Test", Width = 800, Height = 600
    };

    private static WireframeElement MakeEl(string type = "TmButton", double x = 0, double y = 0)
        => new() { Type = type, X = x, Y = y, W = 120, H = 36 };

    // ── GroupElementsCommand ──────────────────────────────────────────────────

    [Fact]
    public void GroupElementsCommand_CreatesGroupWithCorrectBounds()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl(x: 10, y: 10);
        var el2 = MakeEl(x: 50, y: 50);
        doc.Elements.AddRange([el1, el2]);

        var cmd = new GroupElementsCommand(doc, [el1.Id, el2.Id]);
        cmd.Execute();

        doc.Elements.Should().HaveCount(3);
        var group = doc.Elements.First(e => e.Type == "__group__");
        group.X.Should().BeApproximately(-2, 0.1);  // min(10,50) - 12
        group.Y.Should().BeApproximately(-2, 0.1);  // min(10,50) - 12
        group.W.Should().BeApproximately(184, 0.1); // (50+120) - 10 + 24
        group.H.Should().BeApproximately(100, 0.1); // (50+36) - 10 + 24 = 100
        group.ZIndex.Should().BeLessThan(el1.ZIndex);
    }

    [Fact]
    public void GroupElementsCommand_SetsGroupIdOnChildren()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl();
        var el2 = MakeEl();
        doc.Elements.AddRange([el1, el2]);

        var cmd = new GroupElementsCommand(doc, [el1.Id, el2.Id]);
        cmd.Execute();

        var group = doc.Elements.First(e => e.Type == "__group__");
        el1.GroupId.Should().Be(group.Id);
        el2.GroupId.Should().Be(group.Id);
    }

    [Fact]
    public void GroupElementsCommand_UndoRestoresPreviousGroupIds()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl();
        var el2 = MakeEl();
        el1.GroupId = "old-group";
        doc.Elements.AddRange([el1, el2]);

        var cmd = new GroupElementsCommand(doc, [el1.Id, el2.Id]);
        cmd.Execute();
        cmd.Undo();

        el1.GroupId.Should().Be("old-group");
        el2.GroupId.Should().BeNull();
        doc.Elements.Should().NotContain(e => e.Type == "__group__");
    }

    [Fact]
    public void GroupElementsCommand_SingleElement_CreatesGroup()
    {
        var doc = EmptyDoc();
        var el = MakeEl(x: 20, y: 20);
        doc.Elements.Add(el);

        var cmd = new GroupElementsCommand(doc, [el.Id]);
        cmd.Execute();

        doc.Elements.Should().HaveCount(2);
        el.GroupId.Should().NotBeNull();
    }

    // ── UngroupElementsCommand ────────────────────────────────────────────────

    [Fact]
    public void UngroupElementsCommand_RemovesGroupAndClearsGroupId()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl();
        var el2 = MakeEl();
        doc.Elements.AddRange([el1, el2]);

        var groupCmd = new GroupElementsCommand(doc, [el1.Id, el2.Id]);
        groupCmd.Execute();
        var group = doc.Elements.First(e => e.Type == "__group__");

        var ungroupCmd = new UngroupElementsCommand(doc, group.Id);
        ungroupCmd.Execute();

        doc.Elements.Should().HaveCount(2);
        doc.Elements.Should().NotContain(e => e.Type == "__group__");
        el1.GroupId.Should().BeNull();
        el2.GroupId.Should().BeNull();
    }

    [Fact]
    public void UngroupElementsCommand_UndoRestoresGroupAndGroupIds()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl();
        var el2 = MakeEl();
        doc.Elements.AddRange([el1, el2]);

        var groupCmd = new GroupElementsCommand(doc, [el1.Id, el2.Id]);
        groupCmd.Execute();
        var group = doc.Elements.First(e => e.Type == "__group__");

        var ungroupCmd = new UngroupElementsCommand(doc, group.Id);
        ungroupCmd.Execute();
        ungroupCmd.Undo();

        doc.Elements.Should().Contain(e => e.Type == "__group__" && e.Id == group.Id);
        el1.GroupId.Should().Be(group.Id);
        el2.GroupId.Should().Be(group.Id);
    }

    [Fact]
    public void UngroupElementsCommand_NonGroupId_DoesNothing()
    {
        var doc = EmptyDoc();
        var el = MakeEl();
        doc.Elements.Add(el);

        var cmd = new UngroupElementsCommand(doc, el.Id);
        cmd.Execute();

        doc.Elements.Should().Contain(el);
    }

    // ── RemoveElementsCommand with group cascade ──────────────────────────────

    [Fact]
    public void RemoveElementsCommand_DeletesGroup_CascadesToChildren()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl();
        var el2 = MakeEl();
        doc.Elements.AddRange([el1, el2]);

        var groupCmd = new GroupElementsCommand(doc, [el1.Id, el2.Id]);
        groupCmd.Execute();
        var group = doc.Elements.First(e => e.Type == "__group__");

        var removeCmd = new RemoveElementsCommand(doc, [group.Id]);
        removeCmd.Execute();

        doc.Elements.Should().BeEmpty();
    }

    [Fact]
    public void RemoveElementsCommand_DeletesGroup_UndoRestoresAll()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl();
        var el2 = MakeEl();
        doc.Elements.AddRange([el1, el2]);

        var groupCmd = new GroupElementsCommand(doc, [el1.Id, el2.Id]);
        groupCmd.Execute();
        var group = doc.Elements.First(e => e.Type == "__group__");

        var removeCmd = new RemoveElementsCommand(doc, [group.Id]);
        removeCmd.Execute();
        removeCmd.Undo();

        doc.Elements.Should().HaveCount(3);
        doc.Elements.Should().Contain(e => e.Id == el1.Id);
        doc.Elements.Should().Contain(e => e.Id == el2.Id);
        doc.Elements.Should().Contain(e => e.Type == "__group__");
    }

    [Fact]
    public void RemoveElementsCommand_DeletesChildOnly_GroupRemains()
    {
        var doc = EmptyDoc();
        var el1 = MakeEl();
        var el2 = MakeEl();
        doc.Elements.AddRange([el1, el2]);

        var groupCmd = new GroupElementsCommand(doc, [el1.Id, el2.Id]);
        groupCmd.Execute();

        var removeCmd = new RemoveElementsCommand(doc, [el1.Id]);
        removeCmd.Execute();

        doc.Elements.Should().HaveCount(2);
        doc.Elements.Should().Contain(e => e.Type == "__group__");
        doc.Elements.Should().Contain(e => e.Id == el2.Id);
    }
}
