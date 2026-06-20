using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.DocumentEditor;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class DocumentEditorKeyboardManagerTests
{
    [Fact]
    public void GetCommand_CtrlAltVOpensVersions()
    {
        var manager = new DocumentEditorKeyboardManager();

        var command = manager.GetCommand(new KeyboardEventArgs
        {
            Key = "v",
            CtrlKey = true,
            AltKey = true
        });

        command.Should().Be(DocumentEditorKeyboardCommand.OpenVersions);
    }

    [Theory]
    [InlineData("F10", false)]
    [InlineData("Alt", true)]
    public void GetCommand_ActivatesRibbonKeyboardMode(string key, bool altKey)
    {
        var manager = new DocumentEditorKeyboardManager();

        var command = manager.GetCommand(new KeyboardEventArgs
        {
            Key = key,
            AltKey = altKey
        });

        command.Should().Be(DocumentEditorKeyboardCommand.ActivateRibbon);
    }

    [Theory]
    [InlineData("b", "bold")]
    [InlineData("i", "italic")]
    [InlineData("u", "underline")]
    [InlineData("s", "save")]
    [InlineData("k", "link")]
    [InlineData("y", "redo")]
    public void GetRegistryCommandName_MapsCtrlShortcutsToCommandNames(string key, string expectedName)
    {
        var manager = new DocumentEditorKeyboardManager();

        var name = manager.GetRegistryCommandName(new KeyboardEventArgs
        {
            Key = key,
            CtrlKey = true
        });

        name.Should().Be(expectedName);
    }

    [Theory]
    [InlineData("f", "find")]
    [InlineData("h", "replace")]
    [InlineData("p", "commandPalette")]
    public void GetRegistryCommandName_MapsProductivityShortcutsToCommandNames(string key, string expectedName)
    {
        var manager = new DocumentEditorKeyboardManager();

        var name = manager.GetRegistryCommandName(new KeyboardEventArgs
        {
            Key = key,
            CtrlKey = true,
            ShiftKey = key == "p"
        });

        name.Should().Be(expectedName);
    }

    [Fact]
    public void GetRegistryCommandName_CtrlZMapsToUndo()
    {
        var manager = new DocumentEditorKeyboardManager();

        var name = manager.GetRegistryCommandName(new KeyboardEventArgs
        {
            Key = "z",
            CtrlKey = true
        });

        name.Should().Be("undo");
    }

    [Fact]
    public void GetRegistryCommandName_CtrlShiftZMapsToRedo()
    {
        var manager = new DocumentEditorKeyboardManager();

        var name = manager.GetRegistryCommandName(new KeyboardEventArgs
        {
            Key = "z",
            CtrlKey = true,
            ShiftKey = true
        });

        name.Should().Be("redo");
    }

    [Fact]
    public void GetRegistryCommandName_NonRegistryShortcutsReturnNull()
    {
        var manager = new DocumentEditorKeyboardManager();

        manager.GetRegistryCommandName(new KeyboardEventArgs { Key = "F10" }).Should().BeNull();
        manager.GetRegistryCommandName(new KeyboardEventArgs { Key = "Escape" }).Should().BeNull();
        manager.GetRegistryCommandName(new KeyboardEventArgs { Key = "v", CtrlKey = true, AltKey = true }).Should().BeNull();
    }

    // ─── Find & Replace shortcuts ────────────────────────────────────────────

    [Fact]
    public void GetCommand_CtrlF_ReturnsOpenFind()
    {
        var manager = new DocumentEditorKeyboardManager();
        var command = manager.GetCommand(new KeyboardEventArgs { Key = "f", CtrlKey = true });
        command.Should().Be(DocumentEditorKeyboardCommand.OpenFind);
    }

    [Fact]
    public void GetCommand_CtrlH_ReturnsOpenReplace()
    {
        var manager = new DocumentEditorKeyboardManager();
        var command = manager.GetCommand(new KeyboardEventArgs { Key = "h", CtrlKey = true });
        command.Should().Be(DocumentEditorKeyboardCommand.OpenReplace);
    }
}
