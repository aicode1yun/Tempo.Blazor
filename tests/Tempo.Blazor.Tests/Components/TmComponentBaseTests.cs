using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components;
using Xunit;

namespace Tempo.Blazor.Tests.Components;

/// <summary>K9: the shared data-testid convention (TestIdPrefix namespacing + DataTestId root override).</summary>
public class TmComponentBaseTests : TestContext
{
    private sealed class Probe : TmComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", RootTestId("root"));
            builder.OpenElement(2, "span");
            builder.AddAttribute(3, "data-testid", TestId("child"));
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    [Fact]
    public void NoPrefix_KeepsBareTestIds_BackwardCompatible()
    {
        var cut = RenderComponent<Probe>();
        cut.Find("[data-testid='root']").Should().NotBeNull();
        cut.Find("[data-testid='child']").Should().NotBeNull();
    }

    [Fact]
    public void TestIdPrefix_NamespacesRootAndInternalIds()
    {
        var cut = RenderComponent<Probe>(p => p.Add(c => c.TestIdPrefix, "alpha"));
        cut.Find("[data-testid='alpha-root']").Should().NotBeNull();
        cut.Find("[data-testid='alpha-child']").Should().NotBeNull();
    }

    [Fact]
    public void DataTestId_OverridesRootOnly_ChildStillPrefixed()
    {
        var cut = RenderComponent<Probe>(p => p
            .Add(c => c.DataTestId, "custom-root")
            .Add(c => c.TestIdPrefix, "alpha"));
        cut.Find("[data-testid='custom-root']").Should().NotBeNull();
        cut.Find("[data-testid='alpha-child']").Should().NotBeNull();
    }
}
