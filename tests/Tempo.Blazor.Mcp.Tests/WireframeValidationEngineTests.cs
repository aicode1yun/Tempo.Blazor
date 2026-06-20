using System.Text.Json;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Mcp.Wireframe;

namespace Tempo.Blazor.Mcp.Tests;

/// <summary>Tests for the wireframe validation engine and the validate tool.</summary>
public class WireframeValidationEngineTests
{
    private static WireframeSchemaRegistry Registry() => new([new BuiltInComponentSchemas()]);

    private static WireframeDocument DocWith(params WireframeElement[] elements)
    {
        var doc = new WireframeDocument { Title = "T" };
        doc.EnsureActivePage();
        doc.Elements.AddRange(elements);
        return doc;
    }

    private static string KnownType() => new WireframeSchemaRegistry([new BuiltInComponentSchemas()]).GetAll().First().Type;

    private static JsonElement Json(object value)
        => JsonSerializer.SerializeToElement(value);

    [Fact]
    public void Valid_Document_HasNoErrors()
    {
        var doc = DocWith(new WireframeElement { Type = KnownType(), X = 0, Y = 0, W = 100, H = 40 });

        var result = WireframeValidationEngine.Validate(doc, Registry());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void UnknownType_IsReported_WithSuggestion()
    {
        var known = KnownType();
        var doc = DocWith(new WireframeElement { Type = known + "X", W = 100, H = 40 });

        var result = WireframeValidationEngine.Validate(doc, Registry());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("unknown component type").And.Contain(known);
    }

    [Fact]
    public void NonPositiveSize_IsReported()
    {
        var doc = DocWith(new WireframeElement { Type = KnownType(), W = 0, H = -5 });

        var result = WireframeValidationEngine.Validate(doc, Registry());

        result.Errors.Should().Contain(e => e.Contains(".w:"));
        result.Errors.Should().Contain(e => e.Contains(".h:"));
    }

    [Fact]
    public void UnknownProperty_IsReported_WithPath()
    {
        var el = new WireframeElement { Type = KnownType(), W = 100, H = 40 };
        el.Props["definitelyNotAProp"] = Json("x");
        var doc = DocWith(el);

        var result = WireframeValidationEngine.Validate(doc, Registry());

        result.Errors.Should().Contain(e =>
            e.Contains("props.definitelyNotAProp") && e.Contains("unknown property"));
    }

    [Fact]
    public void DuplicateElementId_IsReported()
    {
        var doc = DocWith(
            new WireframeElement { Id = "dup", Type = KnownType(), W = 100, H = 40 },
            new WireframeElement { Id = "dup", Type = KnownType(), W = 100, H = 40 });

        var result = WireframeValidationEngine.Validate(doc, Registry());

        result.Errors.Should().Contain(e => e.Contains("duplicate element id"));
    }

    [Fact]
    public void ConnectorToMissingElement_IsReported()
    {
        var doc = DocWith(new WireframeElement { Id = "a", Type = KnownType(), W = 100, H = 40 });
        doc.Connectors.Add(new WireframeConnector { FromId = "a", ToId = "ghost" });

        var result = WireframeValidationEngine.Validate(doc, Registry());

        result.Errors.Should().Contain(e => e.Contains("toId") && e.Contains("ghost"));
    }

    [Fact]
    public void EnumProperty_OutOfRange_IsReported_WhenSchemaHasEnumProp()
    {
        var registry = Registry();
        var enumSchema = registry.GetAll()
            .FirstOrDefault(s => s.Props.Any(p => p.Type == PropType.Enum && p.Options is { Length: > 0 }));
        if (enumSchema is null)
        {
            return; // no enum-typed component in the built-in set; nothing to assert
        }

        var enumProp = enumSchema.Props.First(p => p.Type == PropType.Enum && p.Options is { Length: > 0 });
        var el = new WireframeElement { Type = enumSchema.Type, W = 100, H = 40 };
        el.Props[enumProp.Name] = Json("___not_a_valid_option___");
        var doc = DocWith(el);

        var result = WireframeValidationEngine.Validate(doc, registry);

        result.Errors.Should().Contain(e => e.Contains($"props.{enumProp.Name}") && e.Contains("not a valid value"));
    }

    [Fact]
    public void ValidateTool_ReturnsValidFalse_NotAnErrorEnvelope()
    {
        var known = KnownType();
        var doc = DocWith(new WireframeElement { Type = known + "X", W = 100, H = 40 });
        var json = WireframeSerializer.Serialize(doc);

        var root = JsonDocument.Parse(WireframeValidationTools.ValidateDocument(Registry(), json)).RootElement;

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("valid").GetBoolean().Should().BeFalse();
        root.GetProperty("validationErrors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void ValidateTool_MalformedJson_ReturnsValidationFailedEnvelope()
    {
        var root = JsonDocument.Parse(WireframeValidationTools.ValidateDocument(Registry(), "{ not json")).RootElement;

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }
}
