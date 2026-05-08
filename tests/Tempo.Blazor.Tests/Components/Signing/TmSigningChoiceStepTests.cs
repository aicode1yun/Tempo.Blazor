using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSigningChoiceStepTests : LocalizationTestBase
{
    [Fact]
    public void Render_Select_RendersSelectOptions()
    {
        var cut = RenderComponent<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, CreateChoiceField(SigningFieldType.Select)));

        cut.Find(".tm-signing-choice-step__select").Should().NotBeNull();
        cut.FindAll("option").Select(option => option.TextContent).Should().Contain(["One", "Two"]);
    }

    [Fact]
    public void Render_Radio_RendersRadioOptions()
    {
        var cut = RenderComponent<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, CreateChoiceField(SigningFieldType.Radio)));

        cut.FindAll("input[type='radio']").Should().HaveCount(2);
    }

    [Fact]
    public void Render_Multiple_RendersCheckboxOptions()
    {
        var cut = RenderComponent<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, CreateChoiceField(SigningFieldType.Multiple)));

        cut.FindAll("input[type='checkbox']").Should().HaveCount(2);
    }

    [Fact]
    public void Render_CheckboxGroup_RendersFieldsAsCheckboxes()
    {
        var cut = RenderComponent<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Uuid = "group", Type = SigningFieldType.Checkbox })
            .Add(p => p.Fields, [
                new SigningField { Uuid = "a", Name = "Consent A", Type = SigningFieldType.Checkbox },
                new SigningField { Uuid = "b", Name = "Consent B", Type = SigningFieldType.Checkbox }
            ]));

        cut.FindAll("input[type='checkbox']").Should().HaveCount(2);
        cut.Markup.Should().Contain("Consent A");
    }

    [Fact]
    public void Render_AnonymousCheckbox_UsesInstruction()
    {
        var cut = RenderComponent<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Internal", Type = SigningFieldType.Checkbox })
            .Add(p => p.AnonymousCheckbox, true));

        cut.Find(".tm-signing-choice-step__option").TextContent.Should().Contain("Check to confirm");
    }

    [Fact]
    public void RequiredChoice_EmptySelect_ShowsValidation()
    {
        var cut = RenderComponent<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, CreateChoiceField(SigningFieldType.Select, required: true)));

        cut.Find("select").Change(string.Empty);

        cut.Find(".tm-signing-step-shell__validation").TextContent.Should().Contain("choice");
    }

    private static SigningField CreateChoiceField(SigningFieldType type, bool required = false)
    {
        return new SigningField
        {
            Uuid = "choice",
            Name = "Choice",
            Type = type,
            Required = required,
            Options =
            [
                new SigningFieldOption { Uuid = "one", Value = "One" },
                new SigningFieldOption { Uuid = "two", Value = "Two" }
            ]
        };
    }
}
