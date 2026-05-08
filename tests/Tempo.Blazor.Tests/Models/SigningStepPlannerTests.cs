using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Tests.Models;

public class SigningStepPlannerTests
{
    [Fact]
    public void Plan_OrdersFieldsByDocumentPageYAndX()
    {
        var pages = new[]
        {
            new SigningDocumentPage { AttachmentUuid = "b", PageIndex = 0 },
            new SigningDocumentPage { AttachmentUuid = "a", PageIndex = 0 }
        };
        var fields = new[]
        {
            CreateField("third", SigningFieldType.Text, "a", 0, 0.2, 0.1),
            CreateField("first", SigningFieldType.Text, "b", 0, 0.4, 0.1),
            CreateField("second", SigningFieldType.Text, "b", 0, 0.2, 0.8)
        };

        var plan = SigningStepPlanner.Plan(fields, pages);

        plan.Steps.Select(step => step.Field.Uuid).Should().Equal("second", "first", "third");
    }

    [Fact]
    public void Plan_GroupsAdjacentCheckboxFields()
    {
        var fields = new[]
        {
            CreateField("consent-a", SigningFieldType.Checkbox, y: 0.1),
            CreateField("consent-b", SigningFieldType.Checkbox, y: 0.2),
            CreateField("name", SigningFieldType.Text, y: 0.3)
        };

        var plan = SigningStepPlanner.Plan(fields);

        plan.Steps.Should().HaveCount(2);
        plan.Steps[0].IsCheckboxGroup.Should().BeTrue();
        plan.Steps[0].Fields.Select(field => field.Uuid).Should().Equal("consent-a", "consent-b");
    }

    [Fact]
    public void Plan_SkipsReadonlyHeadingAndStrikethroughFromSteps()
    {
        var fields = new[]
        {
            CreateField("heading", SigningFieldType.Heading, readOnly: true),
            CreateField("strike", SigningFieldType.Strikethrough, readOnly: true),
            CreateField("name", SigningFieldType.Text)
        };

        var plan = SigningStepPlanner.Plan(fields);

        plan.Steps.Select(step => step.Field.Uuid).Should().Equal("name");
        plan.OverlayFields.Select(item => item.Field.Uuid).Should().Contain(["heading", "strike"]);
    }

    [Fact]
    public void Plan_HidesFieldWhenConditionsDoNotMatch()
    {
        var source = CreateField("country", SigningFieldType.Select);
        var dependent = CreateField("state", SigningFieldType.Text);
        dependent.Conditions.Add(new SigningFieldCondition
        {
            FieldUuid = "country",
            Action = SigningConditionAction.Equal,
            Value = "us"
        });

        var hidden = SigningStepPlanner.Plan([source, dependent], values: new Dictionary<string, object?> { ["country"] = "cz" });
        var visible = SigningStepPlanner.Plan([source, dependent], values: new Dictionary<string, object?> { ["country"] = "us" });

        hidden.Steps.Select(step => step.Field.Uuid).Should().NotContain("state");
        visible.Steps.Select(step => step.Field.Uuid).Should().Contain("state");
    }

    [Fact]
    public void Plan_IncludesReadonlyFormulaFieldsInOverlayButNotSteps()
    {
        var formula = CreateField("total", SigningFieldType.Number, readOnly: true);
        formula.Preferences.Formula = "{{subtotal}} * 1.21";
        var signer = CreateField("name", SigningFieldType.Text);

        var plan = SigningStepPlanner.Plan([formula, signer]);

        plan.Steps.Select(step => step.Field.Uuid).Should().Equal("name");
        plan.OverlayFields.Select(item => item.Field.Uuid).Should().Contain("total");
    }

    private static SigningField CreateField(
        string uuid,
        SigningFieldType type,
        string attachment = "doc",
        int page = 0,
        double y = 0.1,
        double x = 0.1,
        bool readOnly = false)
    {
        return new SigningField
        {
            Uuid = uuid,
            Name = uuid,
            Type = type,
            ReadOnly = readOnly,
            Areas =
            [
                new SigningFieldArea
                {
                    AttachmentUuid = attachment,
                    Page = page,
                    X = x,
                    Y = y,
                    Width = 0.2,
                    Height = 0.05
                }
            ]
        };
    }
}
