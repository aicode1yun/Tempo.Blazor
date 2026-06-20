using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Tests.Models;

public class SigningFieldModelTests
{
    [Fact]
    public void SigningFieldType_DefinesDocusealCompatibleValues()
    {
        Enum.GetNames<SigningFieldType>().Should().BeEquivalentTo(
            "Heading",
            "Strikethrough",
            "Text",
            "Signature",
            "Initials",
            "Date",
            "DateNow",
            "Number",
            "Image",
            "File",
            "Select",
            "Checkbox",
            "Multiple",
            "Radio",
            "Cells",
            "Stamp",
            "Payment",
            "Phone",
            "Verification",
            "Kba");
    }

    [Fact]
    public void SigningField_Defaults_AreReadyForTemplateEditing()
    {
        var field = new SigningField();

        field.Uuid.Should().NotBeNullOrWhiteSpace();
        field.Type.Should().Be(SigningFieldType.Text);
        field.Required.Should().BeFalse();
        field.ReadOnly.Should().BeFalse();
        field.Prefillable.Should().BeFalse();
        field.Options.Should().BeEmpty();
        field.Areas.Should().BeEmpty();
        field.Conditions.Should().BeEmpty();
        field.Preferences.Should().NotBeNull();
    }

    [Fact]
    public void SigningField_Serializes_WithNestedOptionsAreasAndConditions()
    {
        var field = new SigningField
        {
            Uuid = "field-1",
            SubmitterUuid = "role-1",
            Name = "Customer signature",
            Type = SigningFieldType.Signature,
            Required = true,
            Preferences = new SigningFieldPreferences
            {
                Color = "#111111",
                Align = "center",
                Format = "drawn_or_typed",
                WithSignatureId = true
            },
            Validation = new SigningFieldValidation
            {
                Pattern = ".{2,}",
                Message = "Too short"
            },
            Options =
            [
                new SigningFieldOption { Uuid = "option-1", Value = "Approved" }
            ],
            Areas =
            [
                new SigningFieldArea
                {
                    Uuid = "area-1",
                    AttachmentUuid = "doc-1",
                    Page = 2,
                    X = 0.1,
                    Y = 0.2,
                    Width = 0.3,
                    Height = 0.4
                }
            ],
            Conditions =
            [
                new SigningFieldCondition
                {
                    FieldUuid = "field-0",
                    Action = SigningConditionAction.NotEmpty,
                    Operation = SigningConditionOperation.And
                }
            ]
        };

        var json = JsonSerializer.Serialize(field);
        var roundtrip = JsonSerializer.Deserialize<SigningField>(json);

        roundtrip.Should().NotBeNull();
        roundtrip!.Uuid.Should().Be("field-1");
        roundtrip.Type.Should().Be(SigningFieldType.Signature);
        roundtrip.Options.Should().ContainSingle().Which.Value.Should().Be("Approved");
        roundtrip.Areas.Should().ContainSingle().Which.AttachmentUuid.Should().Be("doc-1");
        roundtrip.Conditions.Should().ContainSingle().Which.Action.Should().Be(SigningConditionAction.NotEmpty);
        roundtrip.Preferences.WithSignatureId.Should().BeTrue();
        roundtrip.Validation!.Message.Should().Be("Too short");
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(0.125, 0.25, 0.5, 0.125)]
    public void SigningFieldArea_AcceptsNormalizedCoordinates(double x, double y, double width, double height)
    {
        var area = new SigningFieldArea
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

        area.X.Should().BeInRange(0, 1);
        area.Y.Should().BeInRange(0, 1);
        area.Width.Should().BeInRange(0, 1);
        area.Height.Should().BeInRange(0, 1);
    }
}
