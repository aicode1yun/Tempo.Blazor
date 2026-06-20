using System.Text.Json;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Mcp;
using Tempo.Blazor.Mcp.Wireframe;

namespace Tempo.Blazor.Mcp.Tests;

/// <summary>Tests for the component catalog tools (list + get schema with did-you-mean).</summary>
public class WireframeCatalogToolsTests
{
    private static WireframeSchemaRegistry Registry()
        => new([new BuiltInComponentSchemas()]);

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ListComponents_Compact_ReturnsTypeCategoryDisplayName_Only()
    {
        var root = Parse(WireframeComponentCatalogTools.ListComponents(Registry(), compact: true));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        var first = root.GetProperty("items").EnumerateArray().First();
        first.TryGetProperty("type", out _).Should().BeTrue();
        first.TryGetProperty("displayName", out _).Should().BeTrue();
        first.TryGetProperty("props", out _).Should().BeFalse();
    }

    [Fact]
    public void ListComponents_Full_IncludesProps()
    {
        var root = Parse(WireframeComponentCatalogTools.ListComponents(Registry(), compact: false));

        var withProps = root.GetProperty("items").EnumerateArray()
            .Any(i => i.TryGetProperty("props", out _));
        withProps.Should().BeTrue();
    }

    [Fact]
    public void ListComponents_CategoryFilter_NarrowsResults()
    {
        var registry = Registry();
        var category = registry.GetCategories().First();

        var root = Parse(WireframeComponentCatalogTools.ListComponents(registry, compact: true, category: category));

        root.GetProperty("items").EnumerateArray()
            .Should().OnlyContain(i => i.GetProperty("category").GetString() == category);
    }

    [Fact]
    public void ListComponents_Paging_LimitsItems_ButReportsTotal()
    {
        var registry = Registry();
        var total = registry.GetAll().Count();

        var root = Parse(WireframeComponentCatalogTools.ListComponents(registry, compact: true, take: 1));

        root.GetProperty("totalCount").GetInt32().Should().Be(total);
        root.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void GetComponentSchema_KnownType_ReturnsFullContract()
    {
        var registry = Registry();
        var type = registry.GetAll().First().Type;

        var root = Parse(WireframeComponentCatalogTools.GetComponentSchema(registry, type));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("component").GetProperty("type").GetString().Should().Be(type);
    }

    [Fact]
    public void GetComponentSchema_UnknownType_ReturnsNotFound_WithSuggestion()
    {
        var registry = Registry();
        var known = registry.GetAll().First().Type;
        var typo = known + "X";

        var root = Parse(WireframeComponentCatalogTools.GetComponentSchema(registry, typo));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
        root.GetProperty("message").GetString().Should().Contain(known);
    }
}
