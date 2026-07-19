using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmConditionBuilderTests : LocalizationTestBase
{
    [Fact]
    public void Render_EmptyConditions_RendersEmptyConditionRow()
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target"));

        cut.Find(".tm-condition-builder__row").Should().NotBeNull();
        cut.Find(".tm-condition-builder__field").GetAttribute("value").Should().BeEmpty();
    }

    [Fact]
    public void Render_Fields_ShowsSupportedSourceFieldSelectOptions()
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target"));

        cut.FindAll(".tm-condition-builder__field option")
            .Select(option => option.TextContent)
            .Should()
            .Contain(["Consent", "Delivery", "Country", "Approvals", "Amount", "Name"]);
    }

    [Fact]
    public void Render_Fields_FiltersCurrentField()
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target"));

        cut.FindAll(".tm-condition-builder__field option")
            .Select(option => option.GetAttribute("value"))
            .Should()
            .NotContain("target");
    }

    [Fact]
    public void Render_Fields_FiltersUnsupportedStaticFieldTypes()
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target"));

        cut.FindAll(".tm-condition-builder__field option")
            .Select(option => option.GetAttribute("value"))
            .Should()
            .NotContain(["heading", "strikethrough"]);
    }

    [Fact]
    public void Change_Field_InvokesConditionsChanged()
    {
        IReadOnlyList<SigningFieldCondition>? captured = null;
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.ConditionsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningFieldCondition>>(this, value => captured = value)));

        cut.Find(".tm-condition-builder__field").Change("name");

        captured.Should().NotBeNull();
        captured!.Should().ContainSingle().Which.FieldUuid.Should().Be("name");
    }

    [Theory]
    [InlineData("consent", SigningConditionAction.Checked, SigningConditionAction.Unchecked)]
    [InlineData("delivery", SigningConditionAction.Equal, SigningConditionAction.NotEqual)]
    [InlineData("country", SigningConditionAction.Equal, SigningConditionAction.NotEqual)]
    [InlineData("approvals", SigningConditionAction.Contains, SigningConditionAction.DoesNotContain)]
    [InlineData("name", SigningConditionAction.Empty, SigningConditionAction.NotEmpty)]
    public void Actions_FieldType_RendersExpectedActions(string fieldUuid, params SigningConditionAction[] expectedActions)
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [new SigningFieldCondition { FieldUuid = fieldUuid }]));

        cut.FindAll(".tm-condition-builder__action option")
            .Select(option => Enum.Parse<SigningConditionAction>(option.GetAttribute("value")!))
            .Should()
            .Equal(expectedActions);
    }

    [Fact]
    public void Actions_NumberField_RendersNumericActions()
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [new SigningFieldCondition { FieldUuid = "amount" }]));

        cut.FindAll(".tm-condition-builder__action option")
            .Select(option => Enum.Parse<SigningConditionAction>(option.GetAttribute("value")!))
            .Should()
            .Equal(
                SigningConditionAction.Empty,
                SigningConditionAction.NotEmpty,
                SigningConditionAction.Equal,
                SigningConditionAction.NotEqual,
                SigningConditionAction.GreaterThan,
                SigningConditionAction.LessThan);
    }

    [Theory]
    [InlineData("delivery")]
    [InlineData("country")]
    [InlineData("approvals")]
    public void Value_ChoiceField_RendersOptionDropdown(string fieldUuid)
    {
        var action = fieldUuid == "approvals"
            ? SigningConditionAction.Contains
            : SigningConditionAction.Equal;

        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [new SigningFieldCondition { FieldUuid = fieldUuid, Action = action }]));

        cut.Find(".tm-condition-builder__value-select").Should().NotBeNull();
        cut.FindAll(".tm-condition-builder__value-select option")
            .Select(option => option.TextContent)
            .Should()
            .Contain(["One", "Two"]);
    }

    [Fact]
    public void Value_NumberField_RendersNumberInput()
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [new SigningFieldCondition
                      {
                          FieldUuid = "amount",
                          Action = SigningConditionAction.GreaterThan
                      }]));

        cut.Find("input.tm-condition-builder__value-input[type='number']").Should().NotBeNull();
    }

    [Fact]
    public void Value_EmptyAction_DoesNotRenderValueInput()
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [new SigningFieldCondition
                      {
                          FieldUuid = "name",
                          Action = SigningConditionAction.Empty
                      }]));

        cut.FindAll(".tm-condition-builder__value-input").Should().BeEmpty();
        cut.FindAll(".tm-condition-builder__value-select").Should().BeEmpty();
        cut.FindAll(".tm-condition-builder__validation").Should().BeEmpty();
    }

    [Fact]
    public void Value_RequiredActionWithoutValue_ShowsValidation()
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [new SigningFieldCondition
                      {
                          FieldUuid = "country",
                          Action = SigningConditionAction.Equal
                      }]));

        cut.Find(".tm-condition-builder").ClassList.Should().Contain("tm-condition-builder--invalid");
        cut.Find(".tm-condition-builder__validation").TextContent.Should().Contain("Choose a value");
    }

    [Fact]
    public void AddCondition_AddsSecondConditionRow()
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target"));

        cut.Find(".tm-condition-builder__add").Click();

        cut.FindAll(".tm-condition-builder__row").Should().HaveCount(2);
    }

    [Fact]
    public void Operation_SecondCondition_CanSwitchToOr()
    {
        IReadOnlyList<SigningFieldCondition>? captured = null;
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [
                          new SigningFieldCondition { FieldUuid = "name", Action = SigningConditionAction.NotEmpty },
                          new SigningFieldCondition { FieldUuid = "consent", Action = SigningConditionAction.Checked }
                      ])
                      .Add(p => p.ConditionsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningFieldCondition>>(this, value => captured = value)));

        cut.Find(".tm-condition-builder__operation").Change(SigningConditionOperation.Or.ToString());

        captured.Should().NotBeNull();
        captured![1].Operation.Should().Be(SigningConditionOperation.Or);
    }

    [Fact]
    public void Operation_DraftCondition_DoesNotNotifyBeforeFieldIsSelected()
    {
        IReadOnlyList<SigningFieldCondition>? captured = null;
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.ConditionsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningFieldCondition>>(this, value => captured = value)));

        cut.Find(".tm-condition-builder__add").Click();
        cut.Find(".tm-condition-builder__operation").Change(SigningConditionOperation.Or.ToString());

        cut.FindAll(".tm-condition-builder__row").Should().HaveCount(2);
        captured.Should().BeNull();
    }

    [Fact]
    public void Operation_DraftCondition_IsPreservedWhenFieldIsSelected()
    {
        IReadOnlyList<SigningFieldCondition>? captured = null;
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.ConditionsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningFieldCondition>>(this, value => captured = value)));

        cut.Find(".tm-condition-builder__add").Click();
        cut.Find(".tm-condition-builder__operation").Change(SigningConditionOperation.Or.ToString());
        cut.FindAll(".tm-condition-builder__field")[1].Change("consent");

        captured.Should().NotBeNull();
        captured!.Should().ContainSingle();
        captured[0].FieldUuid.Should().Be("consent");
        captured[0].Operation.Should().Be(SigningConditionOperation.Or);
    }

    [Fact]
    public void RemoveCondition_RemovesConditionAndNotifies()
    {
        IReadOnlyList<SigningFieldCondition>? captured = null;
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [
                          new SigningFieldCondition { FieldUuid = "name", Action = SigningConditionAction.NotEmpty },
                          new SigningFieldCondition { FieldUuid = "consent", Action = SigningConditionAction.Checked }
                      ])
                      .Add(p => p.ConditionsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningFieldCondition>>(this, value => captured = value)));

        cut.FindAll(".tm-condition-builder__remove")[0].Click();

        captured.Should().NotBeNull();
        captured!.Should().ContainSingle().Which.FieldUuid.Should().Be("consent");
    }

    [Fact]
    public void CultureChange_DoesNotChangeExistingConditionFieldUuid()
    {
        IReadOnlyList<SigningFieldCondition>? captured = null;
        var fields = CreateFields();
        fields.First(field => field.Uuid == "country").Labels.Translations["cs"] = "Země";
        var conditions = new[]
        {
            new SigningFieldCondition
            {
                FieldUuid = "country",
                Action = SigningConditionAction.Equal,
                Value = "one"
            }
        };

        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, fields)
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, conditions)
                      .Add(p => p.Culture, "en-US")
                      .Add(p => p.ConditionsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningFieldCondition>>(this, value => captured = value)));

        cut.Render(parameters => parameters.Add(p => p.Culture, "cs-CZ"));

        captured.Should().BeNull();
        cut.Find(".tm-condition-builder__field").GetAttribute("value").Should().Be("country");
        cut.Find(".tm-condition-builder__field").TextContent.Should().Contain("Země");
    }

    [Fact]
    public void Cycle_DirectDependencyOnCurrentField_ShowsValidation()
    {
        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [new SigningFieldCondition
                      {
                          FieldUuid = "target",
                          Action = SigningConditionAction.NotEmpty
                      }]));

        cut.Find(".tm-condition-builder__validation").TextContent.Should().Contain("cycle");
    }

    [Fact]
    public void Cycle_IndirectDependencyAcrossFields_ShowsValidation()
    {
        var fields = CreateFields();
        fields.First(field => field.Uuid == "name").Conditions.Add(new SigningFieldCondition
        {
            FieldUuid = "amount",
            Action = SigningConditionAction.NotEmpty
        });
        fields.First(field => field.Uuid == "amount").Conditions.Add(new SigningFieldCondition
        {
            FieldUuid = "target",
            Action = SigningConditionAction.NotEmpty
        });

        var cut = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, fields)
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [new SigningFieldCondition
                      {
                          FieldUuid = "name",
                          Action = SigningConditionAction.NotEmpty
                      }]));

        cut.Find(".tm-condition-builder__validation").TextContent.Should().Contain("cycle");
    }

    private static List<SigningField> CreateFields()
    {
        return
        [
            CreateField("target", "Target", SigningFieldType.Text),
            CreateField("name", "Name", SigningFieldType.Text),
            CreateField("consent", "Consent", SigningFieldType.Checkbox),
            CreateChoiceField("delivery", "Delivery", SigningFieldType.Radio),
            CreateChoiceField("country", "Country", SigningFieldType.Select),
            CreateChoiceField("approvals", "Approvals", SigningFieldType.Multiple),
            CreateField("amount", "Amount", SigningFieldType.Number),
            CreateField("heading", "Heading", SigningFieldType.Heading),
            CreateField("strikethrough", "Strikethrough", SigningFieldType.Strikethrough)
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

    private static SigningField CreateChoiceField(string uuid, string name, SigningFieldType type)
    {
        var field = CreateField(uuid, name, type);
        field.Options =
        [
            new SigningFieldOption { Uuid = "one", Value = "One" },
            new SigningFieldOption { Uuid = "two", Value = "Two" }
        ];

        return field;
    }
}
