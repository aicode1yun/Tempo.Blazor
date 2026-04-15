using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Tests.Diagram;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class TmDiagramToolboxTests : DiagramTestBase
{
    [Fact]
    public void Renders_All_Categories_By_Default()
    {
        var cut = RenderComponent<TmDiagramToolbox>();

        var categories = cut.FindAll(".tm-diagram-toolbox__category-header");
        categories.Should().NotBeEmpty();
    }

    [Fact]
    public void Search_Filters_Stencils_In_Realtime()
    {
        var cut = RenderComponent<TmDiagramToolbox>();

        // Type "Rectangle" into search input
        var searchInput = cut.Find(".tm-diagram-toolbox__search input");
        searchInput.Input("Rectangle");

        // Should show only matching stencils
        var items = cut.FindAll(".tm-diagram-toolbox__item");
        items.Should().Contain(i => i.TextContent.Contains("Rectangle", StringComparison.OrdinalIgnoreCase));
        items.Should().NotContain(i => i.TextContent.Contains("Cloud", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_Shows_NoResults_When_No_Match()
    {
        var cut = RenderComponent<TmDiagramToolbox>();

        var searchInput = cut.Find(".tm-diagram-toolbox__search input");
        searchInput.Input("xyznonexistent");

        cut.FindAll(".tm-diagram-toolbox__no-results").Should().ContainSingle();
        cut.FindAll(".tm-diagram-toolbox__category").Should().BeEmpty();
    }

    [Fact]
    public void Search_Shows_Only_Categories_With_Matching_Stencils()
    {
        var cut = RenderComponent<TmDiagramToolbox>();

        var searchInput = cut.Find(".tm-diagram-toolbox__search input");
        searchInput.Input("Cloud");

        var categories = cut.FindAll(".tm-diagram-toolbox__category-header");
        categories.Should().ContainSingle();

        var items = cut.FindAll(".tm-diagram-toolbox__item");
        items.Should().OnlyContain(i => i.TextContent.Contains("Cloud", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Clear_Search_Restores_All_Categories()
    {
        var cut = RenderComponent<TmDiagramToolbox>();

        var initialCategories = cut.FindAll(".tm-diagram-toolbox__category-header").Count;

        var searchInput = cut.Find(".tm-diagram-toolbox__search input");
        searchInput.Input("Cloud");

        // Clear the search
        searchInput.Input(string.Empty);

        var restoredCategories = cut.FindAll(".tm-diagram-toolbox__category-header").Count;
        restoredCategories.Should().Be(initialCategories);
    }
}
