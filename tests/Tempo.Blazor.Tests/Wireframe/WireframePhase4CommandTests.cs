using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Commands;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class WireframePhase4CommandTests
{
    private static WireframeDocument EmptyDoc() => new()
    {
        Title = "Test", Width = 800, Height = 600
    };

    private static WireframeElement MakeEl(string type = "TypeA", double x = 0, double y = 0, double w = 120, double h = 36)
        => new() { Type = type, X = x, Y = y, W = w, H = h, Props = [] };

    private static WireframeComponentRegistry BuildRegistry(params (string Type, string[] Props)[] defs)
    {
        var registry = new WireframeComponentRegistry();
        foreach (var (type, props) in defs)
        {
            registry.RegisterDefinition(new WireframeComponentDef
            {
                Type = type,
                Category = "Test",
                DisplayName = type,
                DefaultWidth = 100,
                DefaultHeight = 40,
                Props = props.Select(p => new PropDef { Name = p, Category = "Test", DisplayName = p, Type = PropType.String }).ToList(),
                RenderSvg = (_, __) => { }
            });
        }
        return registry;
    }

    private static JsonElement Json(string value) => JsonSerializer.Deserialize<JsonElement>("\"" + value + "\"");
    private static JsonElement JsonNum(double value) => JsonSerializer.Deserialize<JsonElement>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    // ── WireframeClipboard ────────────────────────────────────────────────────

    [Fact]
    public void WireframeClipboard_HasStyle_ReturnsFalseInitially()
    {
        WireframeClipboard.StyleProps = null;
        WireframeClipboard.Width = null;
        WireframeClipboard.Height = null;
        WireframeClipboard.HasStyle.Should().BeFalse();
    }

    [Fact]
    public void WireframeClipboard_HasStyle_ReturnsTrueAfterCopy()
    {
        WireframeClipboard.StyleProps = new Dictionary<string, JsonElement> { ["color"] = Json("red") };
        WireframeClipboard.HasStyle.Should().BeTrue();
    }

    // ── CopyStyleCommand ──────────────────────────────────────────────────────

    [Fact]
    public void CopyStyleCommand_CopiesPropsToClipboard()
    {
        WireframeClipboard.StyleProps = null;
        var el = MakeEl();
        el.Props["color"] = Json("blue");
        el.Props["size"] = JsonNum(12);

        new CopyStyleCommand(el, includeSize: false).Execute();

        WireframeClipboard.StyleProps.Should().NotBeNull();
        WireframeClipboard.StyleProps!["color"].GetString().Should().Be("blue");
        WireframeClipboard.StyleProps!["size"].GetDouble().Should().Be(12);
        WireframeClipboard.Width.Should().BeNull();
        WireframeClipboard.Height.Should().BeNull();
    }

    [Fact]
    public void CopyStyleCommand_WithIncludeSize_CopiesWidthHeight()
    {
        WireframeClipboard.StyleProps = null;
        WireframeClipboard.Width = null;
        WireframeClipboard.Height = null;
        var el = MakeEl(w: 200, h: 80);
        el.Props["color"] = Json("green");

        new CopyStyleCommand(el, includeSize: true).Execute();

        WireframeClipboard.StyleProps.Should().NotBeNull();
        WireframeClipboard.Width.Should().Be(200);
        WireframeClipboard.Height.Should().Be(80);
    }

    // ── PasteStyleCommand ─────────────────────────────────────────────────────

    [Fact]
    public void PasteStyleCommand_AppliesStylePropsToTarget()
    {
        var doc = EmptyDoc();
        var source = MakeEl("TypeA");
        source.Props["color"] = Json("red");
        var target = MakeEl("TypeA");
        doc.Elements.AddRange([source, target]);

        var registry = BuildRegistry(("TypeA", ["color"]));
        new CopyStyleCommand(source, includeSize: false).Execute();
        new PasteStyleCommand(doc, [target.Id], registry).Execute();

        target.Props["color"].GetString().Should().Be("red");
    }

    [Fact]
    public void PasteStyleCommand_FiltersPropsByTargetSchema()
    {
        var doc = EmptyDoc();
        var source = MakeEl("TypeA");
        source.Props["color"] = Json("red");
        source.Props["border"] = Json("solid");
        var target = MakeEl("TypeB");
        doc.Elements.AddRange([source, target]);

        var registry = BuildRegistry(
            ("TypeA", ["color", "border"]),
            ("TypeB", ["color"]));
        new CopyStyleCommand(source, includeSize: false).Execute();
        new PasteStyleCommand(doc, [target.Id], registry).Execute();

        target.Props["color"].GetString().Should().Be("red");
        target.Props.ContainsKey("border").Should().BeFalse();
    }

    [Fact]
    public void PasteStyleCommand_DoesNothingWhenClipboardEmpty()
    {
        var doc = EmptyDoc();
        var target = MakeEl("TypeA");
        target.Props["color"] = Json("blue");
        doc.Elements.Add(target);

        WireframeClipboard.StyleProps = null;
        var registry = BuildRegistry(("TypeA", ["color"]));
        var cmd = new PasteStyleCommand(doc, [target.Id], registry);
        cmd.Execute();

        target.Props["color"].GetString().Should().Be("blue");
    }

    [Fact]
    public void PasteStyleCommand_SkipsLockedElements()
    {
        var doc = EmptyDoc();
        var source = MakeEl("TypeA");
        source.Props["color"] = Json("red");
        var target = MakeEl("TypeA");
        target.IsLocked = true;
        doc.Elements.AddRange([source, target]);

        var registry = BuildRegistry(("TypeA", ["color"]));
        new CopyStyleCommand(source, includeSize: false).Execute();
        new PasteStyleCommand(doc, [target.Id], registry).Execute();

        target.Props.ContainsKey("color").Should().BeFalse();
    }

    [Fact]
    public void PasteStyleCommand_UndoRestoresOriginalProps()
    {
        var doc = EmptyDoc();
        var source = MakeEl("TypeA");
        source.Props["color"] = Json("red");
        var target = MakeEl("TypeA");
        target.Props["color"] = Json("blue");
        target.Props["size"] = JsonNum(10);
        doc.Elements.AddRange([source, target]);

        var registry = BuildRegistry(("TypeA", ["color", "size"]));
        new CopyStyleCommand(source, includeSize: false).Execute();
        var cmd = new PasteStyleCommand(doc, [target.Id], registry);
        cmd.Execute();

        target.Props["color"].GetString().Should().Be("red");

        cmd.Undo();

        target.Props["color"].GetString().Should().Be("blue");
        target.Props["size"].GetDouble().Should().Be(10);
    }

    [Fact]
    public void PasteStyleCommand_MultiTarget_AppliesToAll()
    {
        var doc = EmptyDoc();
        var source = MakeEl("TypeA");
        source.Props["color"] = Json("red");
        var t1 = MakeEl("TypeA");
        var t2 = MakeEl("TypeA");
        doc.Elements.AddRange([source, t1, t2]);

        var registry = BuildRegistry(("TypeA", ["color"]));
        new CopyStyleCommand(source, includeSize: false).Execute();
        new PasteStyleCommand(doc, [t1.Id, t2.Id], registry).Execute();

        t1.Props["color"].GetString().Should().Be("red");
        t2.Props["color"].GetString().Should().Be("red");
    }

    // ── PasteSizeCommand ──────────────────────────────────────────────────────

    [Fact]
    public void PasteSizeCommand_AppliesWidthHeight()
    {
        var doc = EmptyDoc();
        var source = MakeEl("TypeA", w: 200, h: 80);
        var target = MakeEl("TypeA", w: 100, h: 40);
        doc.Elements.AddRange([source, target]);

        new CopyStyleCommand(source, includeSize: true).Execute();
        new PasteSizeCommand(doc, [target.Id]).Execute();

        target.W.Should().Be(200);
        target.H.Should().Be(80);
    }

    [Fact]
    public void PasteSizeCommand_DoesNothingWhenClipboardHasNoSize()
    {
        var doc = EmptyDoc();
        var target = MakeEl("TypeA", w: 100, h: 40);
        doc.Elements.Add(target);

        WireframeClipboard.Width = null;
        WireframeClipboard.Height = null;
        new PasteSizeCommand(doc, [target.Id]).Execute();

        target.W.Should().Be(100);
        target.H.Should().Be(40);
    }

    [Fact]
    public void PasteSizeCommand_UndoRestoresOriginalSize()
    {
        var doc = EmptyDoc();
        var source = MakeEl("TypeA", w: 200, h: 80);
        var target = MakeEl("TypeA", w: 100, h: 40);
        doc.Elements.AddRange([source, target]);

        new CopyStyleCommand(source, includeSize: true).Execute();
        var cmd = new PasteSizeCommand(doc, [target.Id]);
        cmd.Execute();

        target.W.Should().Be(200);
        target.H.Should().Be(80);

        cmd.Undo();

        target.W.Should().Be(100);
        target.H.Should().Be(40);
    }

    [Fact]
    public void PasteSizeCommand_SkipsLockedElements()
    {
        var doc = EmptyDoc();
        var source = MakeEl("TypeA", w: 200, h: 80);
        var target = MakeEl("TypeA", w: 100, h: 40);
        target.IsLocked = true;
        doc.Elements.AddRange([source, target]);

        new CopyStyleCommand(source, includeSize: true).Execute();
        new PasteSizeCommand(doc, [target.Id]).Execute();

        target.W.Should().Be(100);
        target.H.Should().Be(40);
    }

    [Fact]
    public void PasteSizeCommand_MultiTarget_AppliesToAll()
    {
        var doc = EmptyDoc();
        var source = MakeEl("TypeA", w: 200, h: 80);
        var t1 = MakeEl("TypeA", w: 100, h: 40);
        var t2 = MakeEl("TypeA", w: 150, h: 60);
        doc.Elements.AddRange([source, t1, t2]);

        new CopyStyleCommand(source, includeSize: true).Execute();
        new PasteSizeCommand(doc, [t1.Id, t2.Id]).Execute();

        t1.W.Should().Be(200);
        t1.H.Should().Be(80);
        t2.W.Should().Be(200);
        t2.H.Should().Be(80);
    }
}
