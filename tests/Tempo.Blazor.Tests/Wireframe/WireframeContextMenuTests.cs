using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class WireframeContextMenuTests : LocalizationTestBase
{
    [Fact]
    public void CanvasMenu_Renders_WhenOpen()
    {
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.X, 100)
            .Add(p => p.Y, 200)
            .Add(p => p.MenuType, WireframeContextMenuType.Canvas));

        cut.Find(".tm-wd-editor__context-menu").Should().NotBeNull();
        cut.FindAll("button").Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CanvasMenu_NotRendered_WhenClosed()
    {
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.MenuType, WireframeContextMenuType.Canvas));

        cut.FindAll(".tm-wd-editor__context-menu").Count.Should().Be(0);
    }

    [Fact]
    public void CanvasMenu_HasExpectedItems()
    {
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Canvas)
            .Add(p => p.CanUndo, true)
            .Add(p => p.CanRedo, true)
            .Add(p => p.HasClipboardStyle, true));

        var buttons = cut.FindAll("button");
        buttons.Count.Should().Be(8); // Undo, Redo, Select All, Paste Style, Paste Size, Toggle Grid, Snap, Fit
    }

    [Fact]
    public void ElementMenu_HasExpectedItems()
    {
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Element)
            .Add(p => p.SelectedCount, 1)
            .Add(p => p.HasGroupInSelection, false)
            .Add(p => p.IsSelectionLocked, false)
            .Add(p => p.HasClipboardStyle, true));

        var buttons = cut.FindAll("button");
        // CopyStyle, Duplicate, PasteStyle, PasteSize, BringToFront, SendToBack, 6x Align, Lock, Delete = 14
        // (No Group because SelectedCount=1)
        buttons.Count.Should().Be(14);
    }

    [Fact]
    public void MultiSelectMenu_HasExpectedItems()
    {
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.MultiSelect)
            .Add(p => p.SelectedCount, 3)
            .Add(p => p.HasGroupInSelection, false)
            .Add(p => p.IsSelectionLocked, false)
            .Add(p => p.HasClipboardStyle, true));

        var buttons = cut.FindAll("button");
        // Group, 6x Align, 2x Distribute, Lock, PasteStyle, PasteSize, Delete = 13
        buttons.Count.Should().Be(13);
    }

    [Fact]
    public void Undo_Disabled_WhenCanUndoIsFalse()
    {
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Canvas)
            .Add(p => p.CanUndo, false));

        var undoBtn = cut.FindAll("button")[0];
        undoBtn.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Undo_InvokesCallback_WhenClicked()
    {
        var invoked = false;
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Canvas)
            .Add(p => p.CanUndo, true)
            .Add(p => p.OnUndo, () => invoked = true));

        cut.FindAll("button")[0].Click();
        invoked.Should().BeTrue();
    }

    [Fact]
    public void Close_InvokedBeforeAction()
    {
        var order = new List<string>();
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Canvas)
            .Add(p => p.CanUndo, true)
            .Add(p => p.OnClose, () => order.Add("close"))
            .Add(p => p.OnUndo, () => order.Add("undo")));

        cut.FindAll("button")[0].Click();
        order.Should().Equal("close", "undo");
    }

    [Fact]
    public void Align_InvokesCallbackWithCorrectValue()
    {
        WireframeAlignment? received = null;
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Element)
            .Add(p => p.SelectedCount, 1)
            .Add(p => p.IsSelectionLocked, false)
            .Add(p => p.OnAlign, (WireframeAlignment a) => received = a));

        // Align buttons start after Duplicate, PasteStyle, PasteSize, BringToFront, SendToBack (5 buttons + 3 separators)
        var buttons = cut.FindAll("button");
        // Find the Align Left button by text content
        var alignLeftBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("Align Left") || b.TextContent.Contains("Left"));
        alignLeftBtn.Should().NotBeNull();
        alignLeftBtn!.Click();
        received.Should().Be(WireframeAlignment.Left);
    }

    [Fact]
    public void Delete_InvokesCallback()
    {
        var invoked = false;
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Element)
            .Add(p => p.SelectedCount, 1)
            .Add(p => p.IsSelectionLocked, false)
            .Add(p => p.OnDelete, () => invoked = true));

        var buttons = cut.FindAll("button");
        var deleteBtn = buttons.FirstOrDefault(b => b.ClassList.Contains("tm-wd-editor__context-item--danger"));
        deleteBtn.Should().NotBeNull();
        deleteBtn!.Click();
        invoked.Should().BeTrue();
    }

    [Fact]
    public void LockUnlock_Toggles_BasedOnIsSelectionLocked()
    {
        // Unlocked selection shows Lock
        var cut1 = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Element)
            .Add(p => p.SelectedCount, 1)
            .Add(p => p.IsSelectionLocked, false));

        cut1.Markup.Should().Contain("Lock");
        cut1.Markup.Should().NotContain("Unlock");

        // Locked selection shows Unlock
        var cut2 = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Element)
            .Add(p => p.SelectedCount, 1)
            .Add(p => p.IsSelectionLocked, true));

        cut2.Markup.Should().Contain("Unlock");
        cut2.Markup.Should().NotContain("Lock");
    }

    [Fact]
    public void Group_ButtonShown_WhenCanGroup()
    {
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.MultiSelect)
            .Add(p => p.SelectedCount, 2)
            .Add(p => p.HasGroupInSelection, false));

        cut.Markup.Should().Contain("Group");
    }

    [Fact]
    public void Group_ButtonHidden_WhenCannotGroup()
    {
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Element)
            .Add(p => p.SelectedCount, 1));

        cut.Markup.Should().NotContain("Group");
    }

    [Fact]
    public void PasteStyle_Disabled_WhenNoClipboardStyle()
    {
        var cut = RenderComponent<TmWireframeContextMenu>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.MenuType, WireframeContextMenuType.Canvas)
            .Add(p => p.HasClipboardStyle, false));

        var buttons = cut.FindAll("button");
        // Paste Style is the 4th button in canvas menu (after Undo, Redo, Select All)
        buttons.Count.Should().BeGreaterThanOrEqualTo(4);
        var pasteStyleBtn = buttons[3];
        pasteStyleBtn.HasAttribute("disabled").Should().BeTrue();
    }
}
