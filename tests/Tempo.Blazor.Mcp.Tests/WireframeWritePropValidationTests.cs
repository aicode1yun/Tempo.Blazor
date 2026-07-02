using System.Text.Json;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Mcp.Tests.Fixtures;
using Tempo.Blazor.Mcp.Wireframe;

namespace Tempo.Blazor.Mcp.Tests;

public class WireframeWritePropValidationTests
{
    private static WireframeSchemaRegistry Registry() => new([new BuiltInComponentSchemas()]);

    private static JsonElement Json(object value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement Parse(string json)
        => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Variant_WrongCasing_IsNormalized()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();

        var result = WireframeOperationEngine.Apply(
            doc,
            """[{"op":"addElement","id":"btn","type":"TmButton","props":{"variant":"Primary"}}]""",
            Registry());

        result.Success.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Code == "enum-normalized");
        doc.Elements.Single().Props["variant"].GetString().Should().Be("primary");
    }

    [Fact]
    public void ChildContent_OnTmButton_WarnsSuggestLabel()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();

        var result = WireframeOperationEngine.Apply(
            doc,
            """[{"op":"addElement","id":"btn","type":"TmButton","props":{"childContent":"Save"}}]""",
            Registry());

        result.Success.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w =>
            w.Code == "unknown-prop"
            && w.Hint.Contains("props.childContent")
            && w.Hint.Contains("Did you mean 'label'"));
        doc.Elements.Single().Props.Should().ContainKey("childContent");
    }

    [Fact]
    public void BoolProp_GivenString_Warns()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();

        var result = WireframeOperationEngine.Apply(
            doc,
            """[{"op":"addElement","id":"btn","type":"TmButton","props":{"disabled":"true"}}]""",
            Registry());

        result.Success.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w =>
            w.Code == "type-mismatch"
            && w.Hint.Contains("props.disabled")
            && w.Hint.Contains("expected Bool"));
    }

    [Fact]
    public void Apply_TwoArgApply_SkipsPropLint()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();

        var result = WireframeOperationEngine.Apply(
            doc,
            """[{"op":"addElement","id":"btn","type":"TmButton","props":{"variant":"Primary"}}]""");

        result.Success.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
        doc.Elements.Single().Props["variant"].GetString().Should().Be("Primary");
    }

    [Fact]
    public void Apply_UpdateElement_StillApplies_WithWarnings()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Id = "btn", Type = "TmButton", W = 120, H = 36 });

        var result = WireframeOperationEngine.Apply(
            doc,
            """[{"op":"updateElement","id":"btn","props":{"variant":"Primary","disabled":"true"}}]""",
            Registry());

        result.Success.Should().BeTrue();
        result.Applied.Should().Be(1);
        result.Warnings.Select(w => w.Code).Should().Contain(["enum-normalized", "type-mismatch"]);
        doc.Elements.Single().Props["variant"].GetString().Should().Be("primary");
    }

    [Fact]
    public void ValidateProps_UsesSharedSuggestionAndWarnsForTypeMismatch()
    {
        var el = new WireframeElement { Id = "btn", Type = "TmButton", W = 120, H = 36 };
        el.Props["childContent"] = Json("Save");
        el.Props["disabled"] = Json("true");
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(el);

        var result = WireframeValidationEngine.Validate(doc, Registry());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().Contain(w =>
            w.Code == "unknown-prop"
            && w.Hint.Contains("Did you mean 'label'"));
        result.Warnings.Should().Contain(w =>
            w.Code == "type-mismatch"
            && w.Hint.Contains("props.disabled"));
    }

    [Fact]
    public void ValidateProps_WarnsForEnumNormalizationWithoutMutatingDocument()
    {
        var el = new WireframeElement { Id = "btn", Type = "TmButton", W = 120, H = 36 };
        el.Props["variant"] = Json("Primary");
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        doc.Elements.Add(el);

        var result = WireframeValidationEngine.Validate(doc, Registry());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().ContainSingle(w => w.Code == "enum-normalized");
        el.Props["variant"].GetString().Should().Be("Primary");
    }

    [Fact]
    public async Task ApplyOperations_DefaultAppliesAndReturnsWarnings()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Design", "/");

        var root = Parse(await WireframeOperationTools.ApplyOperations(
            backend,
            backend,
            Registry(),
            id,
            """[{"op":"addElement","id":"btn","type":"TmButton","props":{"label":"Go","variant":"Primary"}}]"""));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("applied").GetInt32().Should().Be(1);
        root.GetProperty("warnings").GetArrayLength().Should().Be(1);
        (await backend.GetWireframeDocumentAsync(id))!.Elements.Single()
            .Props["variant"].GetString().Should().Be("primary");
    }

    [Fact]
    public async Task ApplyOperations_StrictTrue_ReturnsValidationFailed()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Design", "/");

        var root = Parse(await WireframeOperationTools.ApplyOperations(
            backend,
            backend,
            Registry(),
            id,
            """[{"op":"addElement","id":"btn","type":"TmButton","props":{"label":"Go","variant":"Primary"}}]""",
            strict: true));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
        root.GetProperty("warnings").GetArrayLength().Should().Be(1);
        (await backend.GetWireframeDocumentAsync(id))!.Elements.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTool_ReturnsWarningsArray()
    {
        var doc = new WireframeDocument();
        doc.EnsureActivePage();
        var el = new WireframeElement { Id = "btn", Type = "TmButton", W = 120, H = 36 };
        el.Props["label"] = Json("Go");
        el.Props["variant"] = Json("Primary");
        doc.Elements.Add(el);

        var root = Parse(WireframeValidationTools.ValidateDocument(Registry(), WireframeSerializer.Serialize(doc)));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("valid").GetBoolean().Should().BeTrue();
        root.GetProperty("warnings").GetArrayLength().Should().Be(1);
    }
}
