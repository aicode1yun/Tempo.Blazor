using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

/// <summary>TDD tests for TmCheckbox.</summary>
public class TmCheckboxTests : LocalizationTestBase
{
    [Fact]
    public void TmCheckbox_Renders_Checkbox_Input()
    {
        var cut = Render<TmCheckbox>();
        cut.Find("input[type='checkbox']").Should().NotBeNull();
    }

    [Fact]
    public void TmCheckbox_Has_Wrapper_CssClass()
    {
        var cut = Render<TmCheckbox>();
        cut.Find(".tm-checkbox-wrapper").Should().NotBeNull();
    }

    [Fact]
    public void TmCheckbox_Label_Renders_Label_Text()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.Label, "Accept terms"));
        cut.Find(".tm-checkbox-text").TextContent.Should().Contain("Accept terms");
    }

    [Fact]
    public void TmCheckbox_No_Label_Text_When_Null()
    {
        var cut = Render<TmCheckbox>();
        cut.FindAll(".tm-checkbox-text").Should().BeEmpty();
    }

    [Fact]
    public void TmCheckbox_Checked_When_Value_True()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.Value, true));
        cut.Find("input[type='checkbox']").HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public void TmCheckbox_Unchecked_When_Value_False()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.Value, false));
        cut.Find("input[type='checkbox']").HasAttribute("checked").Should().BeFalse();
    }

    [Fact]
    public void TmCheckbox_Disabled_Sets_Disabled_Attribute()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.Disabled, true));
        cut.Find("input[type='checkbox']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void TmCheckbox_Disabled_Adds_Disabled_CssClass_To_Wrapper()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.Disabled, true));
        cut.Find(".tm-checkbox-wrapper").ClassList.Should().Contain("tm-checkbox-disabled");
    }

    [Fact]
    public void TmCheckbox_HelpText_Shown()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.HelpText, "Optional field"));
        cut.Find("[data-testid='checkbox-help']").TextContent.Should().Contain("Optional field");
    }

    [Fact]
    public void TmCheckbox_ValueChanged_Fires_On_Change()
    {
        bool? captured = null;
        var cut = Render<TmCheckbox>(p => p
            .Add(c => c.Value, false)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<bool>(this, v => captured = v)));

        cut.Find("input[type='checkbox']").Change(true);

        captured.Should().BeTrue();
    }

    // ── Required (accessibility) ─────────────────────────────────

    [Fact]
    public void TmCheckbox_Required_SetsAriaRequiredOnInput()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.Required, true));
        cut.Find("input[type='checkbox']").GetAttribute("aria-required").Should().Be("true");
    }

    [Fact]
    public void TmCheckbox_Required_AddsRequiredMarkerClassToLabelText()
    {
        var cut = Render<TmCheckbox>(p => p
            .Add(c => c.Label, "Accept terms")
            .Add(c => c.Required, true));
        cut.Find(".tm-checkbox-text").ClassList.Should().Contain("tm-input-label-required");
    }

    [Fact]
    public void TmCheckbox_NotRequired_HasNoAriaRequiredAndNoMarker()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.Label, "Accept terms"));
        cut.Find("input[type='checkbox']").HasAttribute("aria-required").Should().BeFalse();
        cut.Find(".tm-checkbox-text").ClassList.Should().NotContain("tm-input-label-required");
    }

    // ── Indeterminate (mixed) state ──────────────────────────────
    // The box used to be styled through `.tm-checkbox-input:indeterminate`, a pseudo-class that can
    // only match when someone sets the input's indeterminate DOM PROPERTY — nobody ever does. The
    // box therefore stayed unfilled and the white dash was invisible on the light surface, and the
    // state was indistinguishable from unchecked for assistive technology.

    [Fact]
    public void TmCheckbox_Indeterminate_RendersTheDashGlyph()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.Indeterminate, true));
        cut.FindAll(".tm-checkbox-indeterminate").Should().ContainSingle();
    }

    [Fact]
    public void TmCheckbox_Indeterminate_MarksTheBoxSoItGetsTheFilledStyling()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.Indeterminate, true));
        cut.Find(".tm-checkbox-custom").ClassList.Should().Contain("tm-checkbox-custom-indeterminate");
    }

    [Fact]
    public void TmCheckbox_Indeterminate_SetsAriaCheckedMixed()
    {
        var cut = Render<TmCheckbox>(p => p.Add(c => c.Indeterminate, true));
        cut.Find("input[type='checkbox']").GetAttribute("aria-checked").Should().Be("mixed");
    }

    [Fact]
    public void TmCheckbox_Checked_WinsOverIndeterminate()
    {
        var cut = Render<TmCheckbox>(p => p
            .Add(c => c.Value, true)
            .Add(c => c.Indeterminate, true));

        cut.FindAll(".tm-checkbox-check").Should().ContainSingle("a real check beats the mixed state");
        cut.FindAll(".tm-checkbox-indeterminate").Should().BeEmpty();
        cut.Find(".tm-checkbox-custom").ClassList.Should().NotContain("tm-checkbox-custom-indeterminate");
        cut.Find("input[type='checkbox']").HasAttribute("aria-checked").Should().BeFalse(
            "aria-checked=mixed would contradict the checked input");
    }

    [Fact]
    public void TmCheckbox_NotIndeterminate_HasNoMixedMarkers()
    {
        var cut = Render<TmCheckbox>();
        cut.Find(".tm-checkbox-custom").ClassList.Should().NotContain("tm-checkbox-custom-indeterminate");
        cut.Find("input[type='checkbox']").HasAttribute("aria-checked").Should().BeFalse();
    }
}
