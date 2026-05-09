using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmFormulaBuilderTests : LocalizationTestBase
{
    [Fact]
    public void Render_RendersTextarea()
    {
        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, CreateFields()));

        cut.Find("textarea.tm-formula-builder__textarea").Should().NotBeNull();
    }

    [Fact]
    public void Render_RendersTokenButtonsForNumberFields()
    {
        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, CreateFields()));

        cut.FindAll(".tm-formula-builder__token")
            .Select(button => button.TextContent.Trim())
            .Should()
            .Contain(["Subtotal", "Tax"]);
    }

    [Fact]
    public void Render_RendersNumericSelectAndRadioFieldsWhenOptionsAreNumeric()
    {
        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, CreateFields()));

        cut.FindAll(".tm-formula-builder__token")
            .Select(button => button.TextContent.Trim())
            .Should()
            .Contain(["Plan price", "Multiplier"]);
    }

    [Fact]
    public void Render_DoesNotRenderCurrentFieldToken()
    {
        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, CreateFields()));

        cut.FindAll(".tm-formula-builder__token")
            .Select(button => button.GetAttribute("data-field-uuid"))
            .Should()
            .NotContain("total");
    }

    [Fact]
    public void Render_DoesNotRenderFieldThatWouldCreateCycle()
    {
        var fields = CreateFields();
        fields.First(field => field.Uuid == "dependent").Preferences.Formula = "{{total}}";

        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, fields));

        cut.FindAll(".tm-formula-builder__token")
            .Select(button => button.GetAttribute("data-field-uuid"))
            .Should()
            .NotContain("dependent");
    }

    [Fact]
    public void ClickTokenButton_InsertsToken()
    {
        string? captured = null;
        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, CreateFields())
                      .Add(p => p.ValueChanged, EventCallback.Factory.Create<string>(this, value => captured = value)));

        cut.Find("[data-field-uuid='subtotal']").Click();

        captured.Should().Contain("{{subtotal}}");
        cut.Find(".tm-formula-builder__textarea").GetAttribute("value").Should().Contain("{{subtotal}}");
    }

    [Fact]
    public void Render_TokenPickerUsesLocalizedFieldLabel()
    {
        var fields = CreateFields();
        fields.First(field => field.Uuid == "subtotal").Labels = new SigningLocalizedText
        {
            Default = "Mezisoučet",
            Translations = { ["en-US"] = "Subtotal localized" }
        };

        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, fields)
                      .Add(p => p.Culture, "en-US")
                      .Add(p => p.FallbackCulture, "cs-CZ"));

        cut.Find("[data-field-uuid='subtotal']").TextContent.Trim().Should().Be("Subtotal localized");
    }

    [Fact]
    public void ClickTokenButton_WithLocalizedLabelStillInsertsStableToken()
    {
        string? captured = null;
        var fields = CreateFields();
        fields.First(field => field.Uuid == "subtotal").Labels = new SigningLocalizedText
        {
            Default = "Mezisoučet",
            Translations = { ["en-US"] = "Subtotal localized" }
        };

        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, fields)
                      .Add(p => p.Culture, "en-US")
                      .Add(p => p.ValueChanged, EventCallback.Factory.Create<string>(this, value => captured = value)));

        cut.Find("[data-field-uuid='subtotal']").Click();

        captured.Should().Be("{{subtotal}}");
    }

    [Theory]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("*")]
    [InlineData("/")]
    public void OperatorButton_InsertsOperator(string op)
    {
        string? captured = null;
        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, CreateFields())
                      .Add(p => p.ValueChanged, EventCallback.Factory.Create<string>(this, value => captured = value)));

        cut.Find($"[data-operator='{op}']").Click();

        captured.Should().Contain(op);
    }

    [Theory]
    [InlineData("round(n, d)")]
    [InlineData("abs(n)")]
    public void FunctionButton_InsertsFunction(string function)
    {
        string? captured = null;
        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, CreateFields())
                      .Add(p => p.ValueChanged, EventCallback.Factory.Create<string>(this, value => captured = value)));

        cut.Find($"[data-function='{function}']").Click();

        captured.Should().Contain(function);
    }

    [Fact]
    public void Save_WithUnknownToken_ShowsValidationAndDoesNotSave()
    {
        var saved = false;
        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, CreateFields())
                      .Add(p => p.Value, "{{Missing}} + 1")
                      .Add(p => p.Saved, EventCallback.Factory.Create<string>(this, _ => saved = true)));

        cut.Find(".tm-formula-builder__save").Click();

        saved.Should().BeFalse();
        cut.Find(".tm-formula-builder__error").TextContent.Should().Contain("Missing");
    }

    [Fact]
    public void Save_WithValidFormula_SetsFieldReadonlyAndSavesNormalizedFormula()
    {
        string? saved = null;
        var field = CreateCurrentField();
        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, field)
                      .Add(p => p.Fields, CreateFields())
                      .Add(p => p.Value, "{{Subtotal}} + {{Tax}}")
                      .Add(p => p.Saved, EventCallback.Factory.Create<string>(this, value => saved = value)));

        cut.Find(".tm-formula-builder__save").Click();

        saved.Should().Be("{{subtotal}} + {{tax}}");
        field.ReadOnly.Should().BeTrue();
        field.Preferences.Formula.Should().Be("{{subtotal}} + {{tax}}");
    }

    [Fact]
    public void CultureChange_DoesNotChangeExistingFormulaToken()
    {
        var fields = CreateFields();
        fields.First(field => field.Uuid == "subtotal").Labels.Translations["cs"] = "Mezisoučet";

        var cut = RenderComponent<TmFormulaBuilder>(parameters =>
            parameters.Add(p => p.Field, CreateCurrentField())
                      .Add(p => p.Fields, fields)
                      .Add(p => p.Value, "{{subtotal}}")
                      .Add(p => p.Culture, "en-US"));

        cut.SetParametersAndRender(parameters => parameters.Add(p => p.Culture, "cs-CZ"));

        cut.Find(".tm-formula-builder__textarea").GetAttribute("value").Should().Be("{{Subtotal}}");
        cut.Find("[data-field-uuid='subtotal']").TextContent.Should().Contain("Mezisoučet");
    }

    private static SigningField CreateCurrentField()
    {
        return CreateField("total", "Total", SigningFieldType.Number);
    }

    private static List<SigningField> CreateFields()
    {
        return
        [
            CreateField("total", "Total", SigningFieldType.Number),
            CreateField("subtotal", "Subtotal", SigningFieldType.Number),
            CreateField("tax", "Tax", SigningFieldType.Number),
            CreateField("dependent", "Dependent", SigningFieldType.Number),
            CreateChoiceField("plan", "Plan price", SigningFieldType.Select, "10", "20"),
            CreateChoiceField("multiplier", "Multiplier", SigningFieldType.Radio, "1", "2"),
            CreateChoiceField("region", "Region", SigningFieldType.Select, "EU", "US"),
            CreateField("notes", "Notes", SigningFieldType.Text)
        ];
    }

    private static SigningField CreateField(string uuid, string name, SigningFieldType type)
    {
        return new SigningField
        {
            Uuid = uuid,
            Name = name,
            Type = type
        };
    }

    private static SigningField CreateChoiceField(string uuid, string name, SigningFieldType type, params string[] values)
    {
        var field = CreateField(uuid, name, type);
        field.Options = values
            .Select(value => new SigningFieldOption { Uuid = value, Value = value })
            .ToList();

        return field;
    }
}
