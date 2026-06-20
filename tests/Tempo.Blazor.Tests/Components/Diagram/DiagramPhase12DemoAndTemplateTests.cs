using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Components.Diagram.Templates;

namespace Tempo.Blazor.Tests.Components.Diagram;

public sealed class DiagramPhase12DemoAndTemplateTests
{
    [Fact]
    public void DemoPage_Uses_Localized_Text_For_Visible_Phase12_Copy()
    {
        var source = File.ReadAllText(Path.Combine(
            SolutionRoot,
            "src/Tempo.Blazor.Demo.SharedUI/Pages/DiagramEditorPage.razor"));

        string[] hardcodedCopy =
        [
            "<PageTitle>Diagram Editor",
            ">Diagram Editor<",
            "Declarative diagram editor with stencil-based nodes",
            ">Interactive Editor<",
            "The canvas below is pre-loaded with a UML Class stencil",
            ">Load UML sample<",
            ">Load grouped sample<",
            ">New document<",
            "Nodes:",
            "Edges:",
            "Title:",
            "Untitled diagram",
            "Grouped bounds sample",
            "UML Class Diagram"
        ];

        foreach (var text in hardcodedCopy)
            source.Should().NotContain(text);
    }

    [Fact]
    public async Task ExtendedProvider_Provides_Uml_Bpmn_And_Archimate_Working_Templates()
    {
        var templates = await LoadExtendedTemplatesAsync();

        templates.Select(t => t.Id).Should().Contain([
            "uml25-class-baseline",
            "bpmn2-process-baseline",
            "archimate3-layered-baseline"
        ]);

        foreach (var templateId in new[] { "uml25-class-baseline", "bpmn2-process-baseline", "archimate3-layered-baseline" })
        {
            var template = templates.Single(t => t.Id == templateId);
            DiagramSerializer.TryDeserialize(template.DocumentJson, out var document).Should().BeTrue();
            document.Should().NotBeNull();
            document!.Nodes.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task ExtendedTemplates_Use_Only_Registered_TempoOriginal_Stencils()
    {
        var stencilRegistry = BuildStencilRegistry();
        var templates = await LoadExtendedTemplatesAsync();

        foreach (var template in templates.Where(t => t.Id.StartsWith("uml25-", StringComparison.Ordinal)
            || t.Id.StartsWith("bpmn2-", StringComparison.Ordinal)
            || t.Id.StartsWith("archimate3-", StringComparison.Ordinal)))
        {
            var result = DiagramTemplateStencilValidator.Validate(template, stencilRegistry);
            result.IsValid.Should().BeTrue(string.Join(", ", result.Errors));
        }
    }

    [Fact]
    public void Readme_Documents_Custom_Stencil_Provider_Registration()
    {
        var readme = File.ReadAllText(Path.Combine(SolutionRoot, "README.md"));

        readme.Should().Contain("IDiagramStencilProvider");
        readme.Should().Contain("builder.Services.TryAddEnumerable");
        readme.Should().Contain("ServiceDescriptor.Singleton<IDiagramStencilProvider");
        readme.Should().Contain("DiagramStencilOrigin.TempoOriginal");
    }

    private static async Task<List<DiagramTemplate>> LoadExtendedTemplatesAsync()
    {
        var provider = new ExtendedDiagramTemplateProvider();
        var categories = await provider.GetTemplateCategoriesAsync();
        return categories.SelectMany(c => c.Templates).ToList();
    }

    private static DiagramStencilRegistry BuildStencilRegistry()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
        registry.RegisterProvider(new Uml25DiagramStencilProvider());
        registry.RegisterProvider(new Bpmn2DiagramStencilProvider());
        registry.RegisterProvider(new Archimate3DiagramStencilProvider());
        registry.RegisterProvider(new ExtendedDiagramStencilProvider());
        return registry;
    }

    private static string SolutionRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
                directory = directory.Parent;

            return directory?.FullName
                ?? throw new DirectoryNotFoundException("Could not locate TempoBlazor.slnx from test output directory.");
        }
    }
}
