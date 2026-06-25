using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class DiagramToolboxAccessibilityPhase10Tests : LocalizationTestBase
{
    private const string LibraryJson = """
        {
          "setId": "phase10.diagram",
          "nameResourceKey": "DiagramLibrary_Phase10",
          "palettes": [
            {
              "paletteId": "phase10.diagram.core",
              "nameResourceKey": "DiagramPalette_Phase10Core",
              "order": 10,
              "stencils": [
                {
                  "id": "phase10.diagram.core.node",
                  "nameResourceKey": "DiagramStencil_Phase10Node",
                  "category": "phase10.diagram",
                  "origin": "tempoOriginal"
                }
              ]
            }
          ]
        }
        """;

    public DiagramToolboxAccessibilityPhase10Tests()
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
            ["DiagramLibrary_Phase10"] = "Phase 10 Library",
            ["DiagramPalette_Phase10Core"] = "Core",
            ["DiagramStencil_Phase10Node"] = "Accessible Node"
        });

        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new JsonDiagramStencilProvider(
            [JsonDiagramStencilLibrarySource.Required("phase10", () => LibraryJson)]));
        Services.AddSingleton(registry);
    }

    [Fact]
    public void Search_Input_Has_Localized_Aria_Label()
    {
        var cut = RenderComponent<TmDiagramToolbox>();

        var search = cut.Find(".tm-diagram-toolbox__search input");

        search.GetAttribute("aria-label").Should().Be("Search diagram stencils");
        search.GetAttribute("placeholder").Should().Be("Search stencils");
    }

    [Fact]
    public void Stencil_Item_Has_Localized_Aria_Label_Without_Duplicating_Visible_Label()
    {
        var cut = RenderComponent<TmDiagramToolbox>();

        var item = cut.Find("[data-stencil-id='phase10.diagram.core.node']");
        var label = item.QuerySelector(".tm-diagram-toolbox__label");

        item.GetAttribute("aria-label").Should().Be("Insert Accessible Node onto the canvas");
        item.GetAttribute("title").Should().Be("Drag Accessible Node onto the canvas");
        label.Should().NotBeNull();
        label!.GetAttribute("aria-hidden").Should().Be("true");
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Stencil_Item_Keyboard_Insert_Invokes_Callback(string key)
    {
        string? insertedStencilId = null;
        var cut = RenderComponent<TmDiagramToolbox>(parameters => parameters
            .Add(component => component.StencilKeyboardInsert, EventCallback.Factory.Create<string>(this, id => insertedStencilId = id)));

        cut.Find("[data-stencil-id='phase10.diagram.core.node']").KeyDown(key);

        insertedStencilId.Should().Be("phase10.diagram.core.node");
    }

    [Fact]
    public void Toolbox_Css_Defines_Focus_Visible_Rings_For_Items_And_Palette_Headers()
    {
        var css = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src/Tempo.Blazor.DiagramEditor/wwwroot/css/components/_diagram-editor.css"));

        css.Should().Contain(".tm-diagram-toolbox__item:focus-visible");
        css.Should().Contain(".tm-diagram-toolbox__category-header:focus-visible");
        css.Should().Contain("outline");
        css.Should().Contain("--tm-color-primary");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TempoBlazor.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
