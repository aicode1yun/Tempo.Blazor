using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class DiagramWorkflowPhase11Tests : LocalizationTestBase
{
    private const string LibraryJson = """
        {
          "setId": "phase11.diagram",
          "nameResourceKey": "DiagramLibrary_Phase11",
          "palettes": [
            {
              "paletteId": "phase11.diagram.general",
              "nameResourceKey": "DiagramPalette_Phase11General",
              "order": 0,
              "stencils": [
                {
                  "id": "phase11.diagram.node",
                  "nameResourceKey": "DiagramStencil_Phase11Node",
                  "category": "phase11",
                  "origin": "tempoOriginal",
                  "tags": ["node"],
                  "keywords": ["node"]
                }
              ]
            }
          ]
        }
        """;

    public DiagramWorkflowPhase11Tests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["TmDiagramToolbox_Toggle"] = "Toggle toolbox",
            ["TmDiagramToolbox_Title"] = "Toolbox",
            ["TmDiagramToolbox_SearchPlaceholder"] = "Search stencils",
            ["TmDiagramToolbox_SearchAriaLabel"] = "Search diagram stencils",
            ["TmDiagramToolbox_NoResults"] = "No matching stencils",
            ["TmDiagramToolbox_DragStencil"] = "Drag {0} onto the canvas",
            ["TmDiagramToolbox_InsertStencil"] = "Insert {0} onto the canvas",
            ["DiagramLibrary_Phase11"] = "Phase 11 Library",
            ["DiagramPalette_Phase11General"] = "General",
            ["DiagramStencil_Phase11Node"] = "Phase 11 Node"
        });

        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new JsonDiagramStencilProvider(
            [JsonDiagramStencilLibrarySource.Required("phase11", () => LibraryJson)]));
        Services.AddSingleton(registry);
    }

    [Fact]
    public void Toolbox_SearchInput_RendersEmptyInitialValue()
    {
        var cut = RenderComponent<TmDiagramToolbox>();

        var search = cut.Find(".tm-diagram-toolbox__search input");

        search.GetAttribute("value").Should().BeNullOrEmpty();
        search.GetAttribute("placeholder").Should().Be("Search stencils");
    }
}
