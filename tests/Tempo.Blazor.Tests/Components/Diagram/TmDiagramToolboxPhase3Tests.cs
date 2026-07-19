using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class TmDiagramToolboxPhase3Tests : LocalizationTestBase
{
    private const string LibraryJson = """
        {
          "setId": "test.diagram",
          "nameResourceKey": "DiagramLibrary_Test",
          "palettes": [
            {
              "paletteId": "test.diagram.beta",
              "nameResourceKey": "DiagramPalette_Beta",
              "order": 20,
              "stencils": [
                {
                  "id": "test.diagram.beta.second",
                  "nameResourceKey": "DiagramStencil_Second",
                  "category": "test.diagram",
                  "origin": "tempoOriginal",
                  "tags": ["process"],
                  "keywords": ["workflow"]
                }
              ]
            },
            {
              "paletteId": "test.diagram.alpha",
              "nameResourceKey": "DiagramPalette_Alpha",
              "order": 10,
              "stencils": [
                {
                  "id": "test.diagram.alpha.first",
                  "nameResourceKey": "DiagramStencil_First",
                  "category": "test.diagram",
                  "origin": "tempoOriginal",
                  "tags": ["classifier"],
                  "keywords": ["domain"]
                }
              ]
            }
          ]
        }
        """;

    public TmDiagramToolboxPhase3Tests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["TmDiagramToolbox_Toggle"] = "Toggle toolbox",
            ["TmDiagramToolbox_Title"] = "Toolbox",
            ["TmDiagramToolbox_SearchPlaceholder"] = "Search stencils",
            ["TmDiagramToolbox_NoResults"] = "No matching stencils",
            ["TmDiagramToolbox_DragStencil"] = "Drag {0} onto the canvas",
            ["TmDiagramToolbox_InsertStencil"] = "Insert {0} onto the canvas",
            ["DiagramLibrary_Test"] = "Architecture Library",
            ["DiagramPalette_Alpha"] = "Alpha Palette",
            ["DiagramPalette_Beta"] = "Beta Palette",
            ["DiagramStencil_First"] = "First Localized",
            ["DiagramStencil_Second"] = "Second Localized"
        });

        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new JsonDiagramStencilProvider(
            [JsonDiagramStencilLibrarySource.Required("phase3", () => LibraryJson)]));
        Services.AddSingleton(registry);
    }

    [Fact]
    public void Renders_Localized_Library_Name()
    {
        var cut = Render<TmDiagramToolbox>();

        cut.Find(".tm-diagram-toolbox__library-header")
            .TextContent
            .Should()
            .Contain("Architecture Library");
    }

    [Fact]
    public void Renders_Palettes_In_Configured_Order()
    {
        var cut = Render<TmDiagramToolbox>();

        cut.FindAll(".tm-diagram-toolbox__category-header")
            .Select(header => header.TextContent.Trim())
            .Should()
            .Equal("Alpha Palette", "Beta Palette");
    }

    [Fact]
    public void Palette_Can_Be_Collapsed_And_Expanded()
    {
        var cut = Render<TmDiagramToolbox>();
        var alphaHeader = cut.FindAll(".tm-diagram-toolbox__category-header")[0];

        alphaHeader.Click();
        cut.FindAll("[data-stencil-id='test.diagram.alpha.first']").Should().BeEmpty();
        cut.FindAll(".tm-diagram-toolbox__category-header")[0].GetAttribute("aria-expanded").Should().Be("false");

        cut.FindAll(".tm-diagram-toolbox__category-header")[0].Click();
        cut.FindAll("[data-stencil-id='test.diagram.alpha.first']").Should().ContainSingle();
        cut.FindAll(".tm-diagram-toolbox__category-header")[0].GetAttribute("aria-expanded").Should().Be("true");
    }

    [Fact]
    public void Search_Uses_Tags_And_Keywords()
    {
        var cut = Render<TmDiagramToolbox>();

        cut.Find(".tm-diagram-toolbox__search input").Input("workflow");

        cut.FindAll(".tm-diagram-toolbox__item")
            .Should()
            .ContainSingle(item => item.GetAttribute("data-stencil-id") == "test.diagram.beta.second");
        cut.FindAll(".tm-diagram-toolbox__item")
            .Should()
            .NotContain(item => item.GetAttribute("data-stencil-id") == "test.diagram.alpha.first");
    }

    [Fact]
    public void Empty_Search_Result_Uses_Localized_Text()
    {
        var cut = Render<TmDiagramToolbox>();

        cut.Find(".tm-diagram-toolbox__search input").Input("missing");

        cut.Find(".tm-diagram-toolbox__no-results")
            .TextContent
            .Should()
            .Contain("No matching stencils");
    }

    [Fact]
    public void Stencil_Items_Are_Keyboard_Focusable_With_Localized_Tooltip()
    {
        var cut = Render<TmDiagramToolbox>();

        var item = cut.Find("[data-stencil-id='test.diagram.alpha.first']");

        item.GetAttribute("tabindex").Should().Be("0");
        item.GetAttribute("role").Should().Be("button");
        item.GetAttribute("title").Should().Be("Drag First Localized onto the canvas");
        item.GetAttribute("aria-label").Should().Be("Insert First Localized onto the canvas");
    }
}
