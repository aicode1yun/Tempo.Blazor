using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilPackComponentProviderTests
{
    [Fact]
    public void Provider_RegistersDefsIntoRegistry()
    {
        var pack = Pack("app:demo", "app:demo", Component("Card"));
        var registry = new WireframeComponentRegistry();

        registry.RegisterProvider(new StencilPackComponentProvider(pack, "app:demo", priority: 50));

        registry.GetDef("app:demo:Card").Should().NotBeNull();
        registry.GetDef("Card", WireframeComponentScope.ForApp("demo")).Should().NotBeNull();
    }

    [Fact]
    public void TwoPacks_SameLocalType_DoNotCollide()
    {
        var one = Pack("app:one", "app:one", Component("Card", displayName: "One Card"));
        var two = Pack("app:two", "app:two", Component("Card", displayName: "Two Card"));
        var registry = new WireframeComponentRegistry();

        registry.RegisterProvider(new StencilPackComponentProvider(one, "app:one", priority: 50));
        registry.RegisterProvider(new StencilPackComponentProvider(two, "app:two", priority: 50));

        registry.Count.Should().Be(2);
        var oneDef = registry.GetDef("app:one:Card");
        var twoDef = registry.GetDef("app:two:Card");
        oneDef.Should().NotBeNull();
        twoDef.Should().NotBeNull();
        oneDef.Should().NotBeSameAs(twoDef);
        registry.GetDef("Card", WireframeComponentScope.ForApp("one"))!.DisplayName.Should().Be("One Card");
        registry.GetDef("Card", WireframeComponentScope.ForApp("two"))!.DisplayName.Should().Be("Two Card");
    }

    private static StencilPack Pack(string id, string ns, params StencilComponent[] components)
        => new()
        {
            Format = "tempo-stencil",
            FormatVersion = 1,
            Id = id,
            Namespace = ns,
            Components = components
        };

    private static StencilComponent Component(string type, string? displayName = null)
        => new()
        {
            Type = type,
            DisplayName = displayName ?? type,
            Category = "Tests",
            DefaultSize = new StencilSize(160, 80),
            Render = new RenderNode
            {
                Kind = RenderNodeKind.Rect,
                Attributes = new Dictionary<string, object?>
                {
                    ["w"] = "{size.w}",
                    ["h"] = "{size.h}"
                }
            }
        };
}
