using System.Text.Json;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Mcp.Tests.Fixtures;
using Tempo.Blazor.Mcp.Wireframe;

namespace Tempo.Blazor.Mcp.Tests;

public class WireframeDocumentLinterTests
{
    private static WireframeSchemaRegistry Registry() => new([new BuiltInComponentSchemas()]);
    private static WireframeSchemaRegistry Registry(params WireframeComponentSchema[] schemas)
        => new([new BuiltInComponentSchemas(), new TestSchemaSource("Test", 10, schemas)]);

    private static JsonElement Json(object value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement Parse(string json)
        => JsonDocument.Parse(json).RootElement;

    private static WireframeDocument Doc(params WireframeElement[] elements)
    {
        var doc = new WireframeDocument { Title = "Lint" };
        doc.EnsureActivePage();
        doc.ActivePage!.Width = 500;
        doc.ActivePage.Height = 400;
        doc.Elements.AddRange(elements);
        return doc;
    }

    private static WireframeElement Button(
        string id,
        double x = 0,
        double y = 0,
        double w = 120,
        double h = 36,
        string? label = "Button")
    {
        var element = new WireframeElement
        {
            Id = id,
            Type = "TmButton",
            X = x,
            Y = y,
            W = w,
            H = h
        };
        if (label is not null)
        {
            element.Props["label"] = Json(label);
        }

        return element;
    }

    private static WireframeElement Card(
        string id,
        double x = 0,
        double y = 0,
        double w = 280,
        double h = 180)
        => new()
        {
            Id = id,
            Type = "TmCard",
            X = x,
            Y = y,
            W = w,
            H = h
        };

    [Fact]
    public void Lint_OffCanvasElement_ReturnsOffCanvasCode()
    {
        var warnings = WireframeDocumentLinter.Lint(Doc(Button("btn", x: 450, w: 80)), Registry());

        warnings.Should().ContainSingle(w =>
            w.ElementId == "btn"
            && w.Code == "off-canvas"
            && w.Hint.Contains("500x400"));
    }

    [Fact]
    public void Lint_DefaultSizedTmCard_ReturnsDefaultSizeCode()
    {
        var warnings = WireframeDocumentLinter.Lint(Doc(new WireframeElement
        {
            Id = "card",
            Type = "TmCard",
            X = 20,
            Y = 20,
            W = 280,
            H = 180
        }), Registry());

        warnings.Should().ContainSingle(w =>
            w.ElementId == "card"
            && w.Code == "default-size"
            && w.Hint.Contains("280x180"));
    }

    [Fact]
    public void Lint_DefaultSizedTmButton_NoWarning()
    {
        var warnings = WireframeDocumentLinter.Lint(Doc(Button("btn")), Registry());

        warnings.Should().NotContain(w => w.Code == "default-size");
    }

    [Fact]
    public void Lint_TwoOverlappingElements_ReturnsOverlapCode()
    {
        var warnings = WireframeDocumentLinter.Lint(Doc(
            Button("a", x: 20, y: 20, w: 100, h: 40),
            Button("b", x: 80, y: 35, w: 120, h: 40)), Registry());

        warnings.Should().Contain(w => w.ElementId == "a" && w.Code == "overlap");
        warnings.Should().Contain(w => w.ElementId == "b" && w.Code == "overlap");
    }

    [Fact]
    public void Lint_ElementFullyInsideContainer_DoesNotReturnOverlapCode()
    {
        var warnings = WireframeDocumentLinter.Lint(Doc(
            Card("card", x: 20, y: 20, w: 240, h: 160),
            Button("button", x: 48, y: 76, w: 120, h: 36)), Registry());

        warnings.Should().NotContain(w => w.Code == "overlap");
    }

    [Fact]
    public void Lint_PartialOverlapWithContainer_ReturnsOverlapCode()
    {
        var warnings = WireframeDocumentLinter.Lint(Doc(
            Card("card", x: 20, y: 20, w: 160, h: 100),
            Button("button", x: 150, y: 80, w: 120, h: 36)), Registry());

        warnings.Should().Contain(w => w.ElementId == "card" && w.Code == "overlap");
        warnings.Should().Contain(w => w.ElementId == "button" && w.Code == "overlap");
    }

    [Fact]
    public void Lint_CustomSchemaContainerSuppressesContainedOverlap()
    {
        var registry = Registry(new WireframeComponentSchema
        {
            Type = "AppPanel",
            Category = "Custom",
            DisplayName = "App Panel",
            IsContainer = true
        });

        var warnings = WireframeDocumentLinter.Lint(Doc(
            new WireframeElement { Id = "panel", Type = "AppPanel", X = 20, Y = 20, W = 240, H = 160 },
            Button("button", x: 48, y: 76, w: 120, h: 36)), registry);

        warnings.Should().NotContain(w => w.Code == "overlap");
    }

    [Fact]
    public void Lint_LongTextInNarrowBox_ReturnsTextOverflowCode()
    {
        var warnings = WireframeDocumentLinter.Lint(Doc(
            Button("btn", w: 40, label: "This label is intentionally too long")), Registry());

        warnings.Should().ContainSingle(w =>
            w.ElementId == "btn"
            && w.Code == "text-overflow"
            && w.Hint.Contains("props.label"));
    }

    [Fact]
    public void Lint_MissingRequiredContent_ReturnsEmptyRequiredContentCode()
    {
        var warnings = WireframeDocumentLinter.Lint(Doc(Button("btn", label: null)), Registry());

        warnings.Should().ContainSingle(w =>
            w.ElementId == "btn"
            && w.Code == "empty-required-content"
            && w.Hint.Contains("props.label"));
    }

    [Fact]
    public void Lint_EmptyDocument_ReturnsNoWarnings()
    {
        var warnings = WireframeDocumentLinter.Lint(Doc(), Registry());

        warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyOperations_ReturnsDocumentLintWarnings()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Design", "/");

        var root = Parse(await WireframeOperationTools.ApplyOperations(
            backend,
            backend,
            Registry(),
            id,
            """[{"op":"addElement","id":"btn","type":"TmButton","x":1250,"y":0,"w":80,"h":36,"props":{"label":"Go"}}]"""));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("applied").GetInt32().Should().Be(1);
        root.GetProperty("warnings").EnumerateArray()
            .Should().Contain(w => w.GetProperty("code").GetString() == "off-canvas");
    }

    [Fact]
    public async Task ApplyOperations_StrictTrue_DocumentLintWarningsDoNotBlock()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Design", "/");

        var root = Parse(await WireframeOperationTools.ApplyOperations(
            backend,
            backend,
            Registry(),
            id,
            """[{"op":"addElement","id":"btn","type":"TmButton","x":1250,"y":0,"w":80,"h":36,"props":{"label":"Go"}}]""",
            strict: true));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("warnings").EnumerateArray()
            .Should().Contain(w => w.GetProperty("code").GetString() == "off-canvas");
        (await backend.GetWireframeDocumentAsync(id))!.Elements.Should().ContainSingle();
    }

    [Fact]
    public void ValidateDocument_ReturnsDocumentLintWarnings()
    {
        var doc = Doc(Button("btn", x: 450, w: 80));

        var root = Parse(WireframeValidationTools.ValidateDocument(Registry(), WireframeSerializer.Serialize(doc)));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("valid").GetBoolean().Should().BeTrue();
        root.GetProperty("validationErrors").GetArrayLength().Should().Be(0);
        root.GetProperty("warnings").EnumerateArray()
            .Should().Contain(w => w.GetProperty("code").GetString() == "off-canvas");
    }

    private sealed class TestSchemaSource(string id, int priority, params WireframeComponentSchema[] schemas)
        : IWireframeSchemaSource
    {
        public string SourceId => id;
        public int Priority => priority;
        public IEnumerable<WireframeComponentSchema> GetSchemas() => schemas;
    }
}
