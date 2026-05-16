using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Inputs;

public class TmColorPickerTests : LocalizationTestBase
{
    [Fact]
    public void TmColorPicker_Renders_Trigger()
    {
        var cut = RenderComponent<TmColorPicker>();

        cut.Find(".tm-color-picker-trigger").Should().NotBeNull();
        cut.Find(".tm-color-picker-trigger-bg").Should().NotBeNull();
    }

    [Fact]
    public void TmColorPicker_Empty_Value_Shows_Placeholder()
    {
        var cut = RenderComponent<TmColorPicker>(parameters =>
        {
            parameters.Add(p => p.Placeholder, "Pick a color");
        });

        cut.Find(".tm-color-picker-trigger-text").TextContent.Trim().Should().Be("Pick a color");
    }

    [Fact]
    public void TmColorPicker_Value_Shows_Value()
    {
        var cut = RenderComponent<TmColorPicker>(parameters =>
        {
            parameters.Add(p => p.Value, "#FF5733");
        });

        var text = cut.Find(".tm-color-picker-trigger-text").TextContent.Trim();
        text.Should().Be("#FF5733");
    }

    [Fact]
    public void TmColorPicker_Click_Opens_Dropdown()
    {
        var cut = RenderComponent<TmColorPicker>();

        cut.FindAll(".tm-color-picker-dropdown").Should().BeEmpty();

        cut.Find(".tm-color-picker-trigger").Click();

        cut.FindAll(".tm-color-picker-dropdown").Should().HaveCount(1);
        cut.Find(".tm-flat-color-picker").Should().NotBeNull();
    }

    [Fact]
    public void TmColorPicker_Open_Adds_Open_Class()
    {
        var cut = RenderComponent<TmColorPicker>();

        cut.Find(".tm-color-picker").ClassList.Should().NotContain("tm-color-picker--open");

        cut.Find(".tm-color-picker-trigger").Click();

        cut.Find(".tm-color-picker").ClassList.Should().Contain("tm-color-picker--open");
    }

    [Fact]
    public void TmColorPicker_Selection_Closes_Dropdown()
    {
        var selectedValue = string.Empty;
        var cut = RenderComponent<TmColorPicker>(parameters =>
        {
            parameters.Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, v => selectedValue = v ?? string.Empty));
        });

        cut.Find(".tm-color-picker-trigger").Click();
        cut.Find(".tm-color-palette-swatch").Click();

        cut.FindAll(".tm-color-picker-dropdown").Should().BeEmpty();
    }

    [Fact]
    public void TmColorPicker_ShowAlpha_False_Passed_To_Gradient()
    {
        var cut = RenderComponent<TmColorPicker>(parameters =>
        {
            parameters.Add(p => p.ShowAlpha, false);
        });

        cut.Find(".tm-color-picker-trigger").Click();

        var gradient = cut.FindComponent<TmFlatColorPicker>();
        gradient.Instance.ShowAlpha.Should().BeFalse();
    }

    [Fact]
    public void TmColorPicker_ShowApplyButton_Renders_Apply_Button()
    {
        var cut = RenderComponent<TmColorPicker>(parameters =>
        {
            parameters.Add(p => p.ShowApplyButton, true);
        });

        cut.Find(".tm-color-picker-trigger").Click();

        var applyBtn = cut.Find(".tm-color-picker-apply");
        applyBtn.Should().NotBeNull();
        applyBtn.TextContent.Trim().Should().Be("Apply");
    }

    [Fact]
    public void TmColorPicker_ShowApplyButton_Selection_Does_Not_Close_Dropdown()
    {
        var cut = RenderComponent<TmColorPicker>(parameters =>
        {
            parameters.Add(p => p.ShowApplyButton, true);
        });

        cut.Find(".tm-color-picker-trigger").Click();
        cut.Find(".tm-color-palette-swatch").Click();

        cut.FindAll(".tm-color-picker-dropdown").Should().HaveCount(1);
    }

    [Fact]
    public void TmColorPicker_ShowApplyButton_Apply_Closes_Dropdown_And_Fires_ValueChanged()
    {
        string? changed = null;
        var cut = RenderComponent<TmColorPicker>(parameters =>
        {
            parameters.Add(p => p.ShowApplyButton, true);
            parameters.Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, v => changed = v));
        });

        cut.Find(".tm-color-picker-trigger").Click();
        cut.Find(".tm-color-palette-swatch").Click();
        cut.Find(".tm-color-picker-apply").Click();

        cut.FindAll(".tm-color-picker-dropdown").Should().BeEmpty();
        changed.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TmColorPicker_ShowCancelButton_ClosesDropdownWithoutFiringValueChanged()
    {
        string? changed = null;
        var cut = RenderComponent<TmColorPicker>(parameters =>
        {
            parameters.Add(p => p.Value, "#112233");
            parameters.Add(p => p.ShowApplyButton, true);
            parameters.Add(p => p.ShowCancelButton, true);
            parameters.Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, v => changed = v));
        });

        cut.Find(".tm-color-picker-trigger").Click();
        cut.Find(".tm-color-palette-swatch").Click();
        cut.Find(".tm-color-picker-cancel").Click();

        cut.FindAll(".tm-color-picker-dropdown").Should().BeEmpty();
        changed.Should().BeNull();
        cut.Find(".tm-color-picker-trigger-text").TextContent.Trim().Should().Be("#112233");
    }

    [Fact]
    public void TmColorPicker_Disabled_DoesNotOpen()
    {
        var cut = RenderComponent<TmColorPicker>(parameters =>
        {
            parameters.Add(p => p.Disabled, true);
        });

        cut.Find(".tm-color-picker-trigger").Click();

        cut.FindAll(".tm-color-picker-dropdown").Should().BeEmpty();
        cut.Find(".tm-color-picker-trigger").GetAttribute("aria-disabled").Should().Be("true");
    }
}
