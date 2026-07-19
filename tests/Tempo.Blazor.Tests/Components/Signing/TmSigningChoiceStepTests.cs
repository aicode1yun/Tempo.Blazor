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
        var cut = Render<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, CreateChoiceField(SigningFieldType.Select)));

        cut.Find(".tm-signing-choice-step__select").Should().NotBeNull();
        cut.FindAll("option").Select(option => option.TextContent).Should().Contain(["One", "Two"]);
    }

    [Fact]
    public void Render_Select_RendersLocalizedOptionsAndKeepsStableValue()
    {
        object? value = null;
        var field = CreateChoiceField(SigningFieldType.Select);
        field.Options[0].Labels.Translations["cs"] = "Jedna";
        field.Options[1].Labels.Translations["cs"] = "Dvě";

        var cut = Render<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, field)
            .Add(p => p.Culture, "cs-CZ")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<object?>(this, changed => value = changed)));

        cut.FindAll("option").Select(option => option.TextContent).Should().Contain(["Jedna", "Dvě"]);

        cut.Find("select").Change("two");

        value.Should().Be("two");
    }

    [Fact]
    public void Render_Radio_RendersRadioOptions()
    {
        var cut = Render<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, CreateChoiceField(SigningFieldType.Radio)));

        cut.FindAll("input[type='radio']").Should().HaveCount(2);
    }

    [Fact]
    public void Render_Radio_RendersLocalizedOptionsAndKeepsStableValue()
    {
        object? value = null;
        var field = CreateChoiceField(SigningFieldType.Radio);
        field.Options[0].Labels.Translations["cs"] = "Jedna";
        field.Options[1].Labels.Translations["cs"] = "Dvě";

        var cut = Render<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, field)
            .Add(p => p.Culture, "cs-CZ")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<object?>(this, changed => value = changed)));

        cut.Find(".tm-signing-choice-step__options").TextContent.Should().Contain("Jedna");

        cut.Find("input[type='radio'][value='two']").Change("two");

        value.Should().Be("two");
    }

    [Fact]
    public void Render_Multiple_RendersCheckboxOptions()
    {
        var cut = Render<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, CreateChoiceField(SigningFieldType.Multiple)));

        cut.FindAll("input[type='checkbox']").Should().HaveCount(2);
    }

    [Fact]
    public void Render_Multiple_RendersLocalizedOptionsAndKeepsStableValues()
    {
        object? value = null;
        var field = CreateChoiceField(SigningFieldType.Multiple);
        field.Options[0].Labels.Translations["cs"] = "Jedna";
        field.Options[1].Labels.Translations["cs"] = "Dvě";

        var cut = Render<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, field)
            .Add(p => p.Culture, "cs-CZ")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<object?>(this, changed => value = changed)));

        cut.Find(".tm-signing-choice-step__options").TextContent.Should().Contain("Dvě");

        cut.Find("input[type='checkbox'][value='two']").Change(true);

        value.Should().BeAssignableTo<string[]>();
        value.As<string[]>().Should().Equal("two");
    }

    [Fact]
    public void Render_CheckboxGroup_RendersFieldsAsCheckboxes()
    {
        var cut = Render<TmSigningChoiceStep>(parameters => parameters
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
        var cut = Render<TmSigningChoiceStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Internal", Type = SigningFieldType.Checkbox })
            .Add(p => p.AnonymousCheckbox, true));

        cut.Find(".tm-signing-choice-step__option").TextContent.Should().Contain("Check to confirm");
    }

    [Fact]
    public void RequiredChoice_EmptySelect_ShowsValidation()
    {
        var cut = Render<TmSigningChoiceStep>(parameters => parameters
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
