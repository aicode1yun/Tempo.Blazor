using Bunit.Rendering;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.DocumentEditor.Registry;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Fáze 17: deklarativní render cesta produkovala nefunkční controls — select bez options/value/
/// onchange, color picker bez value/onchange, toggle s aria-pressed="false" natvrdo. Kontrakty:
/// options z metadat (DocumentToolbarItem.Options), aktuální hodnota + enabled z CommandState,
/// change/click wiring přes context.Execute.
/// </summary>
public class DocumentEditorToolbarRendererTests : LocalizationTestBase
{
    // ─── Select ──────────────────────────────────────────────────────────────

    [Fact]
    public void SelectRenderer_RendersOptionsFromItemMetadata()
    {
        var cut = RenderSelect(BuildSelectItem(), state: null);

        var options = cut.FindAll("select option");
        options.Should().HaveCount(2);
        options[0].GetAttribute("value").Should().Be("1");
        options[0].TextContent.Should().Be("Single");
        options[1].GetAttribute("value").Should().Be("1.5");
        options[1].TextContent.Should().Be("One and half");
    }

    [Fact]
    public void SelectRenderer_SetsCurrentValueFromCommandState()
    {
        var cut = RenderSelect(BuildSelectItem(), State(value: "1.5"));

        cut.Find("select").GetAttribute("value").Should().Be("1.5");
    }

    [Fact]
    public void SelectRenderer_ChangeInvokesExecuteWithSelectedValue()
    {
        object? received = null;
        var cut = RenderSelect(BuildSelectItem(), State(value: "1"), value => received = value);

        cut.Find("select").Change("1.5");

        received.Should().Be("1.5");
    }

    [Fact]
    public void SelectRenderer_DisabledWhenCommandStateDisabled()
    {
        var cut = RenderSelect(BuildSelectItem(), State(value: "1", enabled: false));

        cut.Find("select").HasAttribute("disabled").Should().BeTrue();
    }

    // ─── ColorPicker ─────────────────────────────────────────────────────────

    [Fact]
    public void ColorPickerRenderer_SetsValueAndInvokesExecuteOnChange()
    {
        object? received = null;
        var cut = RenderColorPicker(State(value: "#ff0000"), value => received = value);

        var input = cut.Find("input[type=color]");
        input.GetAttribute("value").Should().Be("#ff0000");

        input.Change("#00ff00");
        received.Should().Be("#00ff00");
    }

    [Fact]
    public void ColorPickerRenderer_DisabledWhenCommandStateDisabled()
    {
        var cut = RenderColorPicker(State(value: "#ff0000", enabled: false));

        cut.Find("input[type=color]").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ColorPickerRenderer_WithoutState_RendersWithoutCrash()
    {
        var cut = RenderColorPicker(state: null);

        cut.Find("input[type=color]").Should().NotBeNull();
    }

    // ─── Toggle ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("active", "true")]
    [InlineData("mixed", "mixed")]
    [InlineData("inactive", "false")]
    [InlineData(null, "false")]
    public void ToggleRenderer_AriaPressedReflectsCommandState(string? value, string expected)
    {
        var cut = RenderToggle(value is null ? State(value: null) : State(value: value));

        cut.Find("button").GetAttribute("aria-pressed").Should().Be(expected);
    }

    [Fact]
    public void ToggleRenderer_WithoutCommandState_KeepsFalse()
    {
        var cut = RenderToggle(state: null);

        cut.Find("button").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public void ToggleRenderer_ClickInvokesExecute_AndDisabledBlocksRender()
    {
        var invoked = false;
        var cut = RenderToggle(State(value: "inactive"), _ => invoked = true);
        cut.Find("button").Click();
        invoked.Should().BeTrue();

        var cutDisabled = RenderToggle(State(value: "inactive", enabled: false));
        cutDisabled.Find("button").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ButtonRenderer_ClickInvokesExecute()
    {
        // Button byl jediný z funkčních rendererů bez napojeného Execute — klik nedělal nic
        // (Select/ColorPicker/Toggle byly dofunkčněny ve Fázi 17, Button zůstal pozadu).
        object? payload = new();
        var cut = RenderButton(State(value: null), value => payload = value);

        cut.Find("button").Click();

        payload.Should().BeNull("button exekuuje bez payloadu (akční příkaz)");
    }

    [Fact]
    public void ButtonRenderer_DisabledCommandState_RendersDisabled()
    {
        var cut = RenderButton(State(value: null, enabled: false));

        cut.Find("button").HasAttribute("disabled").Should().BeTrue(
            "IsEnabled=false z command registry musí button vypnout stejně jako u ostatních rendererů");
    }

    [Fact]
    public void ButtonRenderer_WithoutCommandState_StaysEnabled()
    {
        // Konzistence s Toggle rendererem: bez CommandState je button aktivní, bez Execute
        // delegáta se onclick vůbec neemituje (deklarativní markup zůstává čistý).
        var cut = RenderButton(state: null);

        cut.Find("button").HasAttribute("disabled").Should().BeFalse();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static DocumentToolbarItem BuildSelectItem() => new()
    {
        Id = "lineSpacing",
        CommandName = "lineSpacing",
        Kind = DocumentToolbarItemKind.Select,
        Options =
        [
            new DocumentToolbarItemOption("1", "Single"),
            new DocumentToolbarItemOption("1.5", "One and half"),
        ]
    };

    private static DocumentEditorCommandState State(string? value, bool enabled = true) => new()
    {
        Name = "test",
        IsEnabled = enabled,
        Value = value
    };

    private IRenderedComponent<ContainerFragment> RenderSelect(DocumentToolbarItem item, DocumentEditorCommandState? state, Action<object?>? onExecute = null)
        => RenderFragmentFor(new DocumentToolbarSelectRenderer().Render(BuildContext(item, state, onExecute)));

    private IRenderedComponent<ContainerFragment> RenderColorPicker(DocumentEditorCommandState? state, Action<object?>? onExecute = null)
        => RenderFragmentFor(new DocumentToolbarColorPickerRenderer().Render(BuildContext(
            new DocumentToolbarItem { Id = "textColor", CommandName = "textColor", Kind = DocumentToolbarItemKind.ColorPicker },
            state,
            onExecute)));

    private IRenderedComponent<ContainerFragment> RenderButton(DocumentEditorCommandState? state, Action<object?>? onExecute = null)
        => RenderFragmentFor(new DocumentToolbarButtonRenderer().Render(BuildContext(
            new DocumentToolbarItem { Id = "save", CommandName = "save", Kind = DocumentToolbarItemKind.Button },
            state,
            onExecute)));

    private IRenderedComponent<ContainerFragment> RenderToggle(DocumentEditorCommandState? state, Action<object?>? onExecute = null)
        => RenderFragmentFor(new DocumentToolbarToggleRenderer().Render(BuildContext(
            new DocumentToolbarItem { Id = "bold", CommandName = "bold", Kind = DocumentToolbarItemKind.Toggle },
            state,
            onExecute)));

    private DocumentToolbarRenderContext BuildContext(DocumentToolbarItem item, DocumentEditorCommandState? state, Action<object?>? onExecute)
        => new(
            item,
            Values: null,
            Execute: onExecute is null ? default : EventCallback.Factory.Create<object?>(this, onExecute),
            CommandState: state);

    private IRenderedComponent<ContainerFragment> RenderFragmentFor(RenderFragment fragment) => Render(fragment);
}
