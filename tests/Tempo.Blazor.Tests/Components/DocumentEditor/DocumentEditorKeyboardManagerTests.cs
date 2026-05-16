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
}
