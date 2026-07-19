using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Components.DocumentEditor.Registry;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentCommandPaletteTests : LocalizationTestBase
{
    [Fact]
    public void Closed_DoesNotRender()
    {
        var cut = Render<TmDocumentCommandPalette>(parameters =>
            parameters.Add(p => p.IsOpen, false)
                      .Add(p => p.Commands, MakeCommands()));

        cut.FindAll("[data-testid='document-command-palette']").Should().BeEmpty();
    }

    [Fact]
    public void Open_RendersOnlyVisibleCommands()
    {
        var cut = Render<TmDocumentCommandPalette>(parameters =>
            parameters.Add(p => p.IsOpen, true)
                      .Add(p => p.Commands, MakeCommands()));

        cut.FindAll("[data-testid='document-command-palette-item']")
            .Should()
            .HaveCount(2);
        cut.FindAll("[data-command='hidden']").Should().BeEmpty();
    }

    [Fact]
    public void Search_FiltersByLabelCategoryAndName()
    {
        var cut = Render<TmDocumentCommandPalette>(parameters =>
            parameters.Add(p => p.IsOpen, true)
                      .Add(p => p.Commands, MakeCommands()));

        cut.Find("[data-testid='document-command-palette-search']").Input("Review");

        var items = cut.FindAll("[data-testid='document-command-palette-item']");
        items.Should().ContainSingle();
        items[0].GetAttribute("data-command").Should().Be("comment");
    }

    [Fact]
    public void DisabledCommand_RendersReasonAndDoesNotExecute()
    {
        string? executed = null;
        var cut = Render<TmDocumentCommandPalette>(parameters =>
            parameters.Add(p => p.IsOpen, true)
                      .Add(p => p.Commands, MakeCommands())
                      .Add(p => p.OnExecuteCommand, EventCallback.Factory.Create<string>(this, name => executed = name)));

        var disabledButton = cut.Find("[data-command='comment'] button");

        disabledButton.HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-command='comment'] [data-testid='document-command-palette-disabled-reason']")
            .TextContent
            .Should()
            .Contain("Command is unavailable");
        disabledButton.Click();

        executed.Should().BeNull();
    }

    [Fact]
    public void EnabledCommand_ClickExecutesCommand()
    {
        string? executed = null;
        var cut = Render<TmDocumentCommandPalette>(parameters =>
            parameters.Add(p => p.IsOpen, true)
                      .Add(p => p.Commands, MakeCommands())
                      .Add(p => p.OnExecuteCommand, EventCallback.Factory.Create<string>(this, name => executed = name)));

        cut.Find("[data-command='bold'] button").Click();

        executed.Should().Be("bold");
    }

    [Fact]
    public void CloseButton_FiresCloseCallback()
    {
        var closed = false;
        var cut = Render<TmDocumentCommandPalette>(parameters =>
            parameters.Add(p => p.IsOpen, true)
                      .Add(p => p.Commands, MakeCommands())
                      .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => closed = true)));

        cut.Find("[data-testid='document-command-palette-close']").Click();

        closed.Should().BeTrue();
    }

    private static Dictionary<string, DocumentEditorCommandState> MakeCommands() =>
        new()
        {
            ["bold"] = new DocumentEditorCommandState
            {
                Name = "bold",
                IsEnabled = true,
                IsVisible = true,
                DescriptionKey = "TmDocumentEditor_Bold",
                Category = "Home",
                DefaultShortcut = "Ctrl+B",
                Icon = "bold"
            },
            ["comment"] = new DocumentEditorCommandState
            {
                Name = "comment",
                IsEnabled = false,
                IsVisible = true,
                DescriptionKey = "TmDocumentEditor_AddComment",
                Category = "Review",
                DisabledReasonKey = "TmDocumentEditor_CommandDisabledUnavailable"
            },
            ["hidden"] = new DocumentEditorCommandState
            {
                Name = "hidden",
                IsEnabled = false,
                IsVisible = false,
                DescriptionKey = "TmDocumentEditor_InsertFootnote"
            }
        };
}
