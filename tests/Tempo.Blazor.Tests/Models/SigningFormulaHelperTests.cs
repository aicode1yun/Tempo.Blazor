using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Tests.Models;

public class SigningFormulaHelperTests
{
    [Fact]
    public void Humanize_ReplacesUuidTokensWithFieldNames()
    {
        var fields = CreateFields();

        var formula = SigningFormulaHelper.Humanize("{{subtotal}} + {{tax}}", fields);

        formula.Should().Be("{{Subtotal}} + {{Tax}}");
    }

    [Fact]
    public void Normalize_ReplacesFieldNamesWithUuidTokens()
    {
        var fields = CreateFields();

        var result = SigningFormulaHelper.Normalize("{{Subtotal}} + {{Tax}}", fields);

        result.IsValid.Should().BeTrue();
        result.Formula.Should().Be("{{subtotal}} + {{tax}}");
    }

    [Fact]
    public void Normalize_UnknownField_ReturnsError()
    {
        var fields = CreateFields();

        var result = SigningFormulaHelper.Normalize("{{Missing}} + 1", fields);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("Missing", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_DirectCycle_ReturnsError()
    {
        var fields = CreateFields();

        var result = SigningFormulaHelper.Validate("{{total}} + 1", fields, "total");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_IndirectCycle_ReturnsError()
    {
        var fields = CreateFields();
        fields.First(field => field.Uuid == "subtotal").Preferences.Formula = "{{discount}}";
        fields.First(field => field.Uuid == "discount").Preferences.Formula = "{{total}}";

        var result = SigningFormulaHelper.Validate("{{Subtotal}} + 1", fields, "total");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    private static List<SigningField> CreateFields()
    {
        return
        [
            CreateField("subtotal", "Subtotal", SigningFieldType.Number),
            CreateField("tax", "Tax", SigningFieldType.Number),
            CreateField("discount", "Discount", SigningFieldType.Number),
            CreateField("total", "Total", SigningFieldType.Number)
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
}
