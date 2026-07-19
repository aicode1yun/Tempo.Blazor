using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

/// <summary>TDD tests for TmMaskedTextBox.</summary>
public class TmMaskedTextBoxTests : LocalizationTestBase
{
    [Fact]
    public void TmMaskedTextBox_Renders_Input_Element()
    {
        var cut = Render<TmMaskedTextBox>();
        cut.Find("input").Should().NotBeNull();
    }

    [Fact]
    public void TmMaskedTextBox_WithMask_Renders_Prompt_Chars()
    {
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Mask, "(000) 000-0000")
            .Add(x => x.PromptChar, '_'));

        var input = cut.Find("input");
        input.GetAttribute("value").Should().Be("(___) ___-____");
    }

    [Fact]
    public void TmMaskedTextBox_Entering_Digits_Updates_Value()
    {
        string? capturedValue = null;
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Mask, "(000) 000-0000")
            .Add(x => x.PromptChar, '_')
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => capturedValue = v)));

        var input = cut.Find("input");
        input.Input("5");

        capturedValue.Should().Be("(5__) ___-____");
    }

    [Fact]
    public void TmMaskedTextBox_UnmaskedValue_Excludes_Literals()
    {
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Mask, "(000) 000-0000")
            .Add(x => x.PromptChar, '_')
            .Add(x => x.Value, "(555) 123-4567"));

        cut.Instance.UnmaskedValue.Should().Be("5551234567");
    }

    [Fact]
    public void TmMaskedTextBox_IncludeLiterals_True_Value_Includes_Literals()
    {
        string? capturedValue = null;
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Mask, "(000) 000-0000")
            .Add(x => x.PromptChar, '_')
            .Add(x => x.IncludeLiterals, true)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => capturedValue = v)));

        var input = cut.Find("input");
        input.Input("5");

        capturedValue.Should().Be("(5__) ___-____");
    }

    [Fact]
    public void TmMaskedTextBox_Disabled_Renders_Disabled_Input()
    {
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Disabled, true));

        var input = cut.Find("input");
        input.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void TmMaskedTextBox_ReadOnly_Renders_ReadOnly_Input()
    {
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.ReadOnly, true));

        var input = cut.Find("input");
        input.HasAttribute("readonly").Should().BeTrue();
    }

    [Fact]
    public void TmMaskedTextBox_Placeholder_Is_Displayed()
    {
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Placeholder, "Enter phone number"));

        var input = cut.Find("input");
        input.GetAttribute("placeholder").Should().Be("Enter phone number");
    }

    [Fact]
    public void TmMaskedTextBox_Invalid_Char_Is_Ignored()
    {
        string? capturedValue = null;
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Mask, "(000) 000-0000")
            .Add(x => x.PromptChar, '_')
            .Add(x => x.Value, "(5__) ___-____")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => capturedValue = v)));

        var input = cut.Find("input");
        // Simulate browser value with invalid char inserted
        input.Change("(5a__) ___-____");

        // Value should remain unchanged (invalid char stripped), callback not fired
        capturedValue.Should().BeNull();
    }

    [Fact]
    public void TmMaskedTextBox_Backspace_Removes_Last_Valid_Char()
    {
        string? capturedValue = null;
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Mask, "(000) 000-0000")
            .Add(x => x.PromptChar, '_')
            .Add(x => x.Value, "(55_) ___-____")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => capturedValue = v)));

        var input = cut.Find("input");
        input.KeyDown("Backspace");

        capturedValue.Should().Be("(5__) ___-____");
    }

    [Fact]
    public void TmMaskedTextBox_Custom_Prompt_Char_Is_Used()
    {
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Mask, "0000")
            .Add(x => x.PromptChar, '*'));

        var input = cut.Find("input");
        input.GetAttribute("value").Should().Be("****");
    }

    [Fact]
    public void TmMaskedTextBox_MaskedValue_With_Letter_Mask_Only_Accepts_Letters()
    {
        string? capturedValue = null;
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Mask, "LLL")
            .Add(x => x.PromptChar, '_')
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => capturedValue = v)));

        var input = cut.Find("input");
        input.Change("1");

        capturedValue.Should().BeNull();

        input.Change("A");
        capturedValue.Should().Be("A__");
    }

    [Fact]
    public void TmMaskedTextBox_Empty_Mask_Renders_Empty_Value()
    {
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Mask, string.Empty)
            .Add(x => x.Value, "test"));

        var input = cut.Find("input");
        input.GetAttribute("value").Should().Be("test");
    }

    [Fact]
    public void TmMaskedTextBox_IncludeLiterals_False_UnmaskedValue_Equals_Value_Without_Literals()
    {
        var cut = Render<TmMaskedTextBox>(p => p
            .Add(x => x.Mask, "(000) 000-0000")
            .Add(x => x.PromptChar, '_')
            .Add(x => x.IncludeLiterals, false)
            .Add(x => x.Value, "(555) 123-4567"));

        cut.Instance.Value.Should().Be("5551234567");
        cut.Instance.UnmaskedValue.Should().Be("5551234567");
    }
}
