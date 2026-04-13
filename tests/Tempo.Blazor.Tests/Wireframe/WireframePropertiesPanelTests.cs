using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// Tests the pure logic helpers on TmWireframePropertiesPanel.
/// We call the internal static methods directly – no Blazor host or JS interop needed.
/// </summary>
public class WireframePropertiesPanelTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WireframeElement MakeEl(string type = "TmButton",
        double x = 0, double y = 0, double w = 120, double h = 36)
        => new() { Type = type, X = x, Y = y, W = w, H = h };

    private static WireframeElement WithProp(WireframeElement el, string key, object val)
    {
        el.Props[key] = JsonSerializer.SerializeToElement(val);
        return el;
    }

    private static PropDef Prop(string name, PropType type = PropType.String, object? def = null,
        string[]? options = null)
        => new() { Name = name, DisplayName = name, Type = type, Default = def, Options = options };

    // ── MixedMarker ───────────────────────────────────────────────────────────

    [Fact]
    public void MixedMarker_IsDistinctFromCommonValues()
    {
        TmWireframePropertiesPanel.MixedMarker.Should().NotBeNullOrEmpty();
        TmWireframePropertiesPanel.MixedMarker.Should().NotBe("0");
        TmWireframePropertiesPanel.MixedMarker.Should().NotBe("true");
        TmWireframePropertiesPanel.MixedMarker.Should().NotBe("");
    }

    // ── IsMixedValue (static) ─────────────────────────────────────────────────

    [Fact]
    public void IsMixedValue_FalseForSingleElement()
    {
        var el = WithProp(MakeEl(), "label", "X");
        TmWireframePropertiesPanel.IsMixedValue([el], "label").Should().BeFalse();
    }

    [Fact]
    public void IsMixedValue_FalseWhenAllElementsHaveSameValue()
    {
        var el1 = WithProp(MakeEl(), "label", "Same");
        var el2 = WithProp(MakeEl(), "label", "Same");
        TmWireframePropertiesPanel.IsMixedValue([el1, el2], "label").Should().BeFalse();
    }

    [Fact]
    public void IsMixedValue_TrueWhenValuesDiffer()
    {
        var el1 = WithProp(MakeEl(), "label", "A");
        var el2 = WithProp(MakeEl(), "label", "B");
        TmWireframePropertiesPanel.IsMixedValue([el1, el2], "label").Should().BeTrue();
    }

    [Fact]
    public void IsMixedValue_TrueWhenOneElementMissingProp()
    {
        var el1 = WithProp(MakeEl(), "label", "A");
        var el2 = MakeEl(); // no label prop
        TmWireframePropertiesPanel.IsMixedValue([el1, el2], "label").Should().BeTrue();
    }

    [Fact]
    public void IsMixedValue_FalseForEmptyList()
    {
        TmWireframePropertiesPanel.IsMixedValue([], "label").Should().BeFalse();
    }

    // ── GetDisplayValue (static) ──────────────────────────────────────────────

    [Fact]
    public void GetDisplayValue_ReturnsStringProp()
    {
        var el   = WithProp(MakeEl(), "label", "Click me");
        var prop = Prop("label");
        TmWireframePropertiesPanel.GetDisplayValue([el], "label", prop)
            .Should().Be("Click me");
    }

    [Fact]
    public void GetDisplayValue_ReturnsFallbackDefault_WhenPropAbsent()
    {
        var el   = MakeEl();
        var prop = Prop("label", def: "Default text");
        TmWireframePropertiesPanel.GetDisplayValue([el], "label", prop)
            .Should().Be("Default text");
    }

    [Fact]
    public void GetDisplayValue_ReturnsBoolTrue()
    {
        var el   = WithProp(MakeEl(), "disabled", true);
        var prop = Prop("disabled", PropType.Bool);
        TmWireframePropertiesPanel.GetDisplayValue([el], "disabled", prop)
            .Should().Be("true");
    }

    [Fact]
    public void GetDisplayValue_ReturnsBoolFalse()
    {
        var el   = WithProp(MakeEl(), "disabled", false);
        var prop = Prop("disabled", PropType.Bool);
        TmWireframePropertiesPanel.GetDisplayValue([el], "disabled", prop)
            .Should().Be("false");
    }

    [Fact]
    public void GetDisplayValue_ReturnsIntAsString()
    {
        var el   = WithProp(MakeEl(), "count", 42);
        var prop = Prop("count", PropType.Int);
        TmWireframePropertiesPanel.GetDisplayValue([el], "count", prop)
            .Should().Be("42");
    }

    [Fact]
    public void GetDisplayValue_JoinsStringArrayWithComma()
    {
        var el   = WithProp(MakeEl(), "items", new[] { "A", "B", "C" });
        var prop = Prop("items", PropType.StringList);
        TmWireframePropertiesPanel.GetDisplayValue([el], "items", prop)
            .Should().Be("A, B, C");
    }

    [Fact]
    public void GetDisplayValue_ReturnsEmptyStringForEmptyList()
    {
        TmWireframePropertiesPanel.GetDisplayValue([], "label", Prop("label"))
            .Should().Be("");
    }

    [Fact]
    public void GetDisplayValue_UsesFirstElementWhenMultiple()
    {
        var el1 = WithProp(MakeEl(), "label", "First");
        var el2 = WithProp(MakeEl(), "label", "Second");
        var prop = Prop("label");
        TmWireframePropertiesPanel.GetDisplayValue([el1, el2], "label", prop)
            .Should().Be("First");
    }
}
