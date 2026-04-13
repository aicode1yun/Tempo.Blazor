using FluentAssertions;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class WireframeComponentRegistryTests
{
    private static WireframeComponentDef MakeDef(string type, string category = "Test",
        int defaultWidth = 100, int defaultHeight = 40, string? displayName = null)
        => new()
        {
            Type = type,
            DisplayName = displayName ?? type,
            Category = category,
            DefaultWidth = defaultWidth,
            DefaultHeight = defaultHeight,
            Props = [],
            RenderSvg = (_, _) => { }
        };

    [Fact]
    public void RegisterDefinition_AddsNewDef()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterDefinition(MakeDef("TypeA"));

        registry.Count.Should().Be(1);
        registry.GetDef("TypeA").Should().NotBeNull();
    }

    [Fact]
    public void RegisterDefinition_HigherPriorityOverridesLower()
    {
        var registry = new WireframeComponentRegistry();
        var low = MakeDef("TypeA", displayName: "Low");
        var high = MakeDef("TypeA", displayName: "High");

        registry.RegisterDefinition(low, priority: 0);
        registry.RegisterDefinition(high, priority: 10);

        registry.GetDef("TypeA")!.DisplayName.Should().Be("High");
    }

    [Fact]
    public void RegisterDefinition_LowerPriorityDoesNotOverrideHigher()
    {
        var registry = new WireframeComponentRegistry();
        var high = MakeDef("TypeA", displayName: "High");
        var low = MakeDef("TypeA", displayName: "Low");

        registry.RegisterDefinition(high, priority: 10);
        registry.RegisterDefinition(low, priority: 0);

        registry.GetDef("TypeA")!.DisplayName.Should().Be("High");
    }

    [Fact]
    public void RegisterDefinition_EqualPriorityKeepsExisting()
    {
        var registry = new WireframeComponentRegistry();
        var first = MakeDef("TypeA", displayName: "First");
        var second = MakeDef("TypeA", displayName: "Second");

        registry.RegisterDefinition(first, priority: 5);
        registry.RegisterDefinition(second, priority: 5);

        registry.GetDef("TypeA")!.DisplayName.Should().Be("First");
    }

    [Fact]
    public void GetDef_ReturnsNullForUnknownType()
    {
        var registry = new WireframeComponentRegistry();
        registry.GetDef("Unknown").Should().BeNull();
    }

    [Fact]
    public void GetAll_ReturnsSortedByCategoryThenDisplayName()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterDefinition(MakeDef("B", "Zeta", displayName: "B"));
        registry.RegisterDefinition(MakeDef("A", "Alpha", displayName: "A"));
        registry.RegisterDefinition(MakeDef("C", "Alpha", displayName: "C"));

        var all = registry.GetAll().ToList();
        all[0].Category.Should().Be("Alpha");
        all[1].Category.Should().Be("Alpha");
        all[2].Category.Should().Be("Zeta");
        all[0].DisplayName.Should().Be("A");
        all[1].DisplayName.Should().Be("C");
    }

    [Fact]
    public void GetCategories_ReturnsDistinctSorted()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterDefinition(MakeDef("X", "Zeta"));
        registry.RegisterDefinition(MakeDef("Y", "Alpha"));
        registry.RegisterDefinition(MakeDef("Z", "Alpha"));

        var cats = registry.GetCategories();
        cats.Should().BeEquivalentTo(["Alpha", "Zeta"], o => o.WithStrictOrdering());
    }

    [Fact]
    public void GetByCategory_FiltersCorrectly()
    {
        var registry = new WireframeComponentRegistry();
        registry.RegisterDefinition(MakeDef("X", "A"));
        registry.RegisterDefinition(MakeDef("Y", "B"));
        registry.RegisterDefinition(MakeDef("Z", "A"));

        var inA = registry.GetByCategory("A").Select(d => d.Type).ToList();
        inA.Should().BeEquivalentTo(["X", "Z"]);
        registry.GetByCategory("C").Should().BeEmpty();
    }

    [Fact]
    public void RegisterProvider_LoadsAllDefsFromProvider()
    {
        var registry = new WireframeComponentRegistry();
        var provider = new TestProvider("P1", 0, [MakeDef("P1A"), MakeDef("P1B")]);
        registry.RegisterProvider(provider);

        registry.Count.Should().Be(2);
    }

    [Fact]
    public void RegisterProvider_HigherPriorityProviderWins()
    {
        var registry = new WireframeComponentRegistry();
        var low = new TestProvider("Low", 0, [MakeDef("TypeA", displayName: "Low")]);
        var high = new TestProvider("High", 10, [MakeDef("TypeA", displayName: "High")]);

        registry.RegisterProvider(low);
        registry.RegisterProvider(high);

        registry.GetDef("TypeA")!.DisplayName.Should().Be("High");
    }

    // ── Test helper ───────────────────────────────────────────────────────────

    private sealed class TestProvider(string id, int priority, IEnumerable<WireframeComponentDef> defs)
        : IWireframeComponentProvider
    {
        public string ProviderId => id;
        public int Priority => priority;
        public IEnumerable<WireframeComponentDef> GetDefinitions() => defs;
    }
}
