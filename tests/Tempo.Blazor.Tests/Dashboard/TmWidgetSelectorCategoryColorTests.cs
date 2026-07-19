using Bunit;
using FluentAssertions;
using NSubstitute;
using Tempo.Blazor.Components.Dashboard;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Dashboard;

/// <summary>
/// Built-in widget categories must render their colour through the theme-aware categorical
/// tokens (a <c>tm-dashboard-category--{id}</c> class that maps to <c>var(--tm-category-N)</c>),
/// so dark mode gets proper values. Custom/host-supplied categories keep their inline model
/// <c>.Color</c> — the model's public <c>Color</c> defaults are intentionally left as raw hex.
/// </summary>
public class TmWidgetSelectorCategoryColorTests : LocalizationTestBase
{
    private const string CustomCategoryId = "MyCustom";
    private const string CustomCategoryColor = "#123456";
    private const string UnknownCategoryId = "GhostCategory";

    private static IWidgetRegistry CreateRegistry()
    {
        var registry = Substitute.For<IWidgetRegistry>();

        var widgets = new List<WidgetDefinition>
        {
            new() { Id = "w-analytics", Name = "Metric", Category = WidgetCategories.Analytics, Icon = "bar-chart-2" },
            new() { Id = "w-custom", Name = "Custom Widget", Category = CustomCategoryId, Icon = "box" },
            new() { Id = "w-ghost", Name = "Ghost Widget", Category = UnknownCategoryId, Icon = "box" },
        };

        registry.GetAllWidgets().Returns(widgets);
        registry.GetCategories().Returns(new List<WidgetCategory>
        {
            new() { Id = WidgetCategories.Analytics, Name = "Analytics & KPIs", Icon = "bar-chart-2", Order = 1, Color = "#3b82f6" },
            new() { Id = CustomCategoryId, Name = "My Custom", Icon = "box", Order = 2, Color = CustomCategoryColor },
        });
        registry.GetWidgetsByCategory(WidgetCategories.Analytics).Returns(new[] { widgets[0] });
        registry.GetWidgetsByCategory(CustomCategoryId).Returns(new[] { widgets[1] });
        registry.GetWidgetsByCategory(UnknownCategoryId).Returns(new[] { widgets[2] });

        return registry;
    }

    private IRenderedComponent<TmWidgetSelector> RenderSelector()
        => Render<TmWidgetSelector>(p => p.Add(x => x.WidgetRegistry, CreateRegistry()));

    [Fact]
    public void WidgetSelector_BuiltInCategory_UsesTokenClass_NotRawHex()
    {
        var cut = RenderSelector();

        var analyticsBtn = cut.FindAll(".tm-widget-selector-category")
            .Single(b => (b.GetAttribute("class") ?? "").Contains("tm-dashboard-category--analytics"));

        // The built-in category is styled by the token-backed class, not a raw hex inline colour.
        (analyticsBtn.GetAttribute("style") ?? "").Should().NotContain("#3b82f6",
            "built-in categories render via var(--tm-category-N) tokens, not a hardcoded hex");

        // Its widget card icon must also use the token class rather than an inline hex background.
        var analyticsIcon = cut.FindAll(".tm-widget-card-icon")
            .Single(i => (i.GetAttribute("class") ?? "").Contains("tm-dashboard-category--analytics"));
        (analyticsIcon.GetAttribute("style") ?? "").Should().NotContain("#3b82f6");
    }

    [Fact]
    public void WidgetSelector_CustomCategory_UsesInlineModelColor()
    {
        var cut = RenderSelector();

        // Custom (host-supplied) category button keeps its own .Color inline, no token class.
        var customBtn = cut.FindAll(".tm-widget-selector-category")
            .Single(b => (b.GetAttribute("style") ?? "").Contains(CustomCategoryColor));

        (customBtn.GetAttribute("class") ?? "").Should().NotContain("tm-dashboard-category--",
            "custom categories are not built-in, so they must not borrow a token class");
        (customBtn.GetAttribute("style") ?? "").Should().Contain($"--category-color: {CustomCategoryColor}");

        // The custom widget card icon likewise carries the inline model colour.
        var customIcon = cut.FindAll(".tm-widget-card-icon")
            .Single(i => (i.GetAttribute("style") ?? "").Contains(CustomCategoryColor));
        (customIcon.GetAttribute("class") ?? "").Should().NotContain("tm-dashboard-category--");
    }

    [Fact]
    public void WidgetSelector_UnknownCategory_UsesFallbackToken_NotRawHex()
    {
        var cut = RenderSelector();

        // A widget whose category is neither built-in nor host-registered falls back to the
        // tokenised fallback (previously the hardcoded "#6366f1").
        var ghostIcon = cut.FindAll(".tm-widget-card-icon")
            .Single(i => (i.GetAttribute("style") ?? "").Contains("var(--tm-category-fallback)"));

        (ghostIcon.GetAttribute("style") ?? "").Should().NotContain("#6366f1");
    }
}
