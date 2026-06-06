using System.Xml.Linq;
using AngleSharp.Dom;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class DiagramLocalizationPhase9Tests : LocalizationTestBase
{
    private static readonly IDiagramStencilProvider[] CustomStencilProviders =
    [
        new Uml25DiagramStencilProvider(),
        new Bpmn2DiagramStencilProvider(),
        new Archimate3DiagramStencilProvider(),
        new ExtendedDiagramStencilProvider()
    ];

    [Fact]
    public void Custom_Toolbox_Libraries_Have_English_Resource_Keys()
    {
        var resourceKeys = LoadResourceKeys("src/Tempo.Blazor/Resources/TmResources.resx");

        GetCustomToolboxResourceKeys()
            .Should()
            .OnlyContain(key => resourceKeys.Contains(key), "custom stencil sets, palettes and toolbox names must be localizable in English");
    }

    [Fact]
    public void Custom_Toolbox_Libraries_Have_Czech_Resource_Keys()
    {
        var resourceKeys = LoadResourceKeys("src/Tempo.Blazor/Resources/TmResources.cs.resx");

        GetCustomToolboxResourceKeys()
            .Should()
            .OnlyContain(key => resourceKeys.Contains(key), "custom stencil sets, palettes and toolbox names must be localizable in Czech");
    }

    [Fact]
    public void Toolbox_Renders_Extended_Libraries_With_Mock_Localizer()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["TmDiagramToolbox_Toggle"] = "Toggle toolbox",
            ["TmDiagramToolbox_Title"] = "Toolbox",
            ["TmDiagramToolbox_SearchPlaceholder"] = "Search stencils",
            ["TmDiagramToolbox_NoResults"] = "No matching stencils",
            ["TmDiagramToolbox_DragStencil"] = "Drag {0} onto the canvas",
            ["DiagramStencilSet_TempoFlowchart"] = "Localized Flowchart",
            ["DiagramStencilPalette_TempoFlowchartCore"] = "Localized Core Flow",
            ["DiagramStencil_TempoFlowchartProcess"] = "Localized Process",
            ["DiagramStencilSet_CloudArchitecture"] = "Localized Cloud",
            ["DiagramStencilPalette_CloudCompute"] = "Localized Compute",
            ["DiagramStencil_CloudCompute"] = "Localized Compute Node",
            ["DiagramStencilSet_Kubernetes"] = "Localized Kubernetes",
            ["DiagramStencilPalette_KubernetesWorkloads"] = "Localized Workloads",
            ["DiagramStencil_KubernetesPod"] = "Localized Pod"
        });

        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new ExtendedDiagramStencilProvider());

        var cut = RenderComponent<TmDiagramToolbox>();
        var text = cut.Markup;

        text.Should().Contain("Localized Flowchart");
        text.Should().Contain("Localized Core Flow");
        text.Should().Contain("Localized Process");
        text.Should().Contain("Localized Cloud");
        text.Should().Contain("Localized Compute");
        text.Should().Contain("Localized Compute Node");
        text.Should().Contain("Localized Kubernetes");
        text.Should().Contain("Localized Workloads");
        text.Should().Contain("Localized Pod");
    }

    [Fact]
    public void PropertiesPanel_Uses_Localized_Text_For_Data_And_Action_Labels()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["TmDiagramProperties_Toggle"] = "Toggle properties",
            ["TmDiagramProperties_Title"] = "Properties",
            ["TmDiagramProperties_Data_Label"] = "Localized Data Label",
            ["TmDiagramProperties_Link"] = "Localized Link",
            ["TmDiagramProperties_LinkPlaceholder"] = "https://localized.example",
            ["TmDiagramProperties_Style"] = "Localized Style",
            ["TmDiagramProperties_Fill"] = "Localized Fill",
            ["TmDiagramProperties_Stroke"] = "Localized Stroke",
            ["TmDiagramProperties_StrokeWidth"] = "Localized Stroke Width",
            ["TmDiagramProperties_Opacity"] = "Localized Opacity",
            ["TmDiagramProperties_Radius"] = "Localized Radius",
            ["TmDiagramProperties_Shadow"] = "Localized Shadow",
            ["TmDiagramProperties_ReplaceShape"] = "Localized Replace",
            ["TmDiagramProperties_ReplaceShapeSearch"] = "Localized Search",
            ["TmDiagramProperties_Text"] = "Localized Text",
            ["TmDiagramProperties_FontFamily"] = "Localized Font",
            ["TmDiagramProperties_FontSize"] = "Localized Size",
            ["TmDiagramProperties_Color"] = "Localized Color",
            ["TmDiagramProperties_TextAlign"] = "Localized Align",
            ["TmDiagramProperties_VerticalAlign"] = "Localized Vertical Align",
            ["TmDiagram_EnableMathJax"] = "Localized Math",
            ["TmDiagramProperties_Formatting"] = "Localized Formatting",
            ["TmDiagramProperties_Bold"] = "Localized Bold",
            ["TmDiagramProperties_Italic"] = "Localized Italic",
            ["TmDiagramProperties_Underline"] = "Localized Underline",
            ["TmDiagramProperties_Arrange"] = "Localized Arrange",
            ["TmDiagramProperties_PositionX"] = "Localized X",
            ["TmDiagramProperties_PositionY"] = "Localized Y",
            ["TmDiagramProperties_SizeWidth"] = "Localized W",
            ["TmDiagramProperties_SizeHeight"] = "Localized H",
            ["TmDiagramProperties_ZIndex"] = "Localized Z",
            ["TmDiagramProperties_Layer"] = "Localized Layer",
            ["TmDiagramProperties_Order"] = "Localized Order",
            ["TmDiagramProperties_BringToFront"] = "Localized Front",
            ["TmDiagramProperties_SendToBack"] = "Localized Back",
            ["TmDiagramProperties_Collapse"] = "Localized Collapse",
            ["TmDiagramProperties_Expand"] = "Localized Expand",
            ["TmDiagramProperties_Lock"] = "Localized Lock",
            ["TmDiagramProperties_Unlock"] = "Localized Unlock"
        });

        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new ExtendedDiagramStencilProvider());

        var node = new DiagramNode
        {
            StencilId = "tempo-flowchart.process",
            IsCollapsible = true,
            Data = new Dictionary<string, object> { ["label"] = "Visible node value" }
        };
        var doc = new DiagramDocument();
        doc.Nodes.Add(node);

        var cut = RenderComponent<TmDiagramPropertiesPanel>(parameters => parameters
            .Add(component => component.Document, doc)
            .Add(component => component.SelectedIds, [node.Id])
            .Add(component => component.ReadOnly, false));

        cut.FindAll(".tm-diagram-properties__field label")
            .Select(label => label.TextContent.Trim())
            .Should()
            .Contain([
                "Localized Data Label",
                "Localized Link",
                "Localized Formatting",
                "Localized X",
                "Localized Y",
                "Localized W",
                "Localized H",
                "Localized Order",
                "Localized Collapse",
                "Localized Lock"
            ]);

        cut.FindAll(".tm-diagram-properties__segmented-btn")
            .Select(button => button.GetAttribute("title"))
            .Should()
            .Contain(["Localized Bold", "Localized Italic", "Localized Underline"]);

        cut.FindAll(".tm-btn")
            .Select(button => button.TextContent.Trim())
            .Should()
            .Contain(["Localized Collapse", "Localized Lock"]);
    }

    private static IEnumerable<string> GetCustomToolboxResourceKeys()
        => CustomStencilProviders
            .SelectMany(provider => provider.GetStencilSets())
            .SelectMany(set => new[] { set.NameResourceKey }
                .Concat(set.Stencils.SelectMany(stencil => new[]
                {
                    stencil.SetNameResourceKey,
                    stencil.PaletteNameResourceKey,
                    stencil.NameResourceKey
                })))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal);

    private static HashSet<string> LoadResourceKeys(string relativePath)
    {
        var root = FindRepositoryRoot();
        var fullPath = Path.Combine(root, relativePath);
        return XDocument.Load(fullPath)
            .Descendants("data")
            .Select(element => element.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
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
