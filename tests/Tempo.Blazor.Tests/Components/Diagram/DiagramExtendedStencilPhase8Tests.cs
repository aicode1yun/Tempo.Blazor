using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Components.Diagram.Templates;
using Tempo.Blazor.Configuration;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class DiagramExtendedStencilPhase8Tests : LocalizationTestBase
{
    [Fact]
    public void Provider_Exposes_Extended_Custom_Sets_With_Expected_Palettes()
    {
        var sets = new ExtendedDiagramStencilProvider()
            .GetStencilSets()
            .ToDictionary(set => set.Id, StringComparer.Ordinal);

        sets.Keys.Should().Contain([
            "tempo-flowchart",
            "tempo-erd",
            "c4",
            "cloud-architecture",
            "kubernetes"
        ]);

        sets["tempo-flowchart"].Stencils.Select(stencil => stencil.PaletteId)
            .Should().Contain("tempo-flowchart.core");
        sets["tempo-erd"].Stencils.Select(stencil => stencil.PaletteId)
            .Should().Contain("tempo-erd.core");
        sets["c4"].Stencils.Select(stencil => stencil.PaletteId)
            .Should().Contain(["c4.software-systems", "c4.containers", "c4.relationships"]);
        sets["cloud-architecture"].Stencils.Select(stencil => stencil.PaletteId)
            .Should().Contain(["cloud-architecture.compute", "cloud-architecture.network", "cloud-architecture.data"]);
        sets["kubernetes"].Stencils.Select(stencil => stencil.PaletteId)
            .Should().Contain(["kubernetes.workloads", "kubernetes.network", "kubernetes.storage"]);

        sets.Values.SelectMany(set => set.Stencils)
            .Should().OnlyContain(stencil => stencil.Origin == DiagramStencilOrigin.TempoOriginal);
    }

    [Fact]
    public void Flowchart_Palette_Contains_Extended_Core_Shapes()
    {
        var stencils = GetStencils("tempo-flowchart.core");

        stencils.Keys.Should().Contain([
            "tempo-flowchart.process",
            "tempo-flowchart.decision",
            "tempo-flowchart.terminator",
            "tempo-flowchart.data",
            "tempo-flowchart.document",
            "tempo-flowchart.offpage"
        ]);
        stencils["tempo-flowchart.decision"].Layout.BackgroundShape.Should().Be("diamond");
        stencils["tempo-flowchart.document"].Layout.BackgroundShape.Should().Be("document");
        stencils["tempo-flowchart.offpage"].Layout.ShapeSvg.Should().Contain("tm-ext-marker-offpage");
    }

    [Fact]
    public void Erd_Palette_Contains_Sql_Import_Compatible_Shapes()
    {
        var stencils = GetStencils("tempo-erd.core");

        stencils.Keys.Should().Contain([
            "tempo-erd.entity",
            "tempo-erd.weak-entity",
            "tempo-erd.relationship",
            "tempo-erd.identifying-relationship",
            "tempo-erd.attribute",
            "tempo-erd.key-attribute"
        ]);
        stencils["tempo-erd.entity"].Keywords.Should().Contain(["sql", "table", "ddl"]);
        stencils["tempo-erd.relationship"].Kind.Should().Be(DiagramStencilKind.Edge);
        stencils["tempo-erd.relationship"].EdgeDefaults!.ConnectorType.Should().Be("erd-relationship");
        stencils["tempo-erd.identifying-relationship"].EdgeDefaults!.Style!.StrokeDashPattern.Should().Be("solid");
    }

    [Fact]
    public void C4_Palette_Contains_Core_Elements_And_Relationships()
    {
        var stencils = GetAllStencils();

        stencils.Keys.Should().Contain([
            "c4.person",
            "c4.software-system",
            "c4.container",
            "c4.component",
            "c4.database",
            "c4.relationship"
        ]);
        stencils["c4.person"].Layout.ShapeSvg.Should().Contain("tm-ext-marker-person");
        stencils["c4.database"].Layout.BackgroundShape.Should().Be("cylinder");
        stencils["c4.relationship"].Kind.Should().Be(DiagramStencilKind.Edge);
        stencils["c4.relationship"].EdgeDefaults!.EndArrow.Should().Be("block");
    }

    [Fact]
    public void Cloud_Palette_Uses_Generic_Tempo_Iconography_Without_Brand_Assets()
    {
        var stencils = GetAllStencils()
            .Where(pair => pair.Value.SetId == "cloud-architecture")
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        stencils.Keys.Should().Contain([
            "cloud.compute",
            "cloud.serverless",
            "cloud.load-balancer",
            "cloud.vpc",
            "cloud.database",
            "cloud.queue",
            "cloud.object-storage"
        ]);
        stencils.Values.Should().OnlyContain(stencil => stencil.ExternalAssetSourceId == null);
        stencils.Values.SelectMany(stencil => stencil.Tags).Should().NotContain(["aws", "azure", "gcp"]);
        stencils["cloud.compute"].Layout.ShapeSvg.Should().Contain("tm-ext-marker-cloud-compute");
        stencils["cloud.object-storage"].Layout.BackgroundShape.Should().Be("cylinder");
    }

    [Fact]
    public void Kubernetes_Palette_Uses_Neutral_Cluster_Shapes()
    {
        var stencils = GetAllStencils()
            .Where(pair => pair.Value.SetId == "kubernetes")
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        stencils.Keys.Should().Contain([
            "kubernetes.cluster",
            "kubernetes.namespace",
            "kubernetes.pod",
            "kubernetes.deployment",
            "kubernetes.service",
            "kubernetes.ingress",
            "kubernetes.config",
            "kubernetes.persistent-volume"
        ]);
        stencils.Values.SelectMany(stencil => stencil.Tags).Should().NotContain(["logo", "brand"]);
        stencils["kubernetes.pod"].Layout.ShapeSvg.Should().Contain("tm-ext-marker-k8s-pod");
        stencils["kubernetes.persistent-volume"].Layout.BackgroundShape.Should().Be("cylinder");
    }

    [Fact]
    public async Task C4_TemplateProvider_Returns_Tempo_Original_Template()
    {
        var provider = new ExtendedDiagramTemplateProvider();

        var templates = (await provider.GetTemplateCategoriesAsync())
            .SelectMany(category => category.Templates)
            .ToDictionary(template => template.Id, StringComparer.Ordinal);

        templates.Keys.Should().Contain("c4-container-baseline");
        templates["c4-container-baseline"].Category.Should().Be("C4");
        templates["c4-container-baseline"].DocumentJson.Should().Contain("c4.software-system");
        templates["c4-container-baseline"].DocumentJson.Should().Contain("c4.container");
        templates["c4-container-baseline"].DocumentJson.ToLowerInvariant().Should().NotContain("draw.io");
    }

    [Fact]
    public void AddTempoBlazorDiagramEditor_Registers_Extended_Provider_And_C4_TemplateProvider()
    {
        var services = new ServiceCollection();

        services.AddTempoBlazorDiagramEditor();

        var provider = services.BuildServiceProvider();
        provider.GetServices<IDiagramStencilProvider>()
            .Should().Contain(service => service is ExtendedDiagramStencilProvider);
        provider.GetRequiredService<DiagramStencilRegistry>()
            .GetStencil("c4.container")
            .Should().NotBeNull();
        provider.GetServices<IDiagramTemplateProvider>()
            .Should().Contain(service => service is ExtendedDiagramTemplateProvider);
    }

    private static Dictionary<string, DiagramStencil> GetStencils(string paletteId)
        => GetAllStencils()
            .Where(pair => pair.Value.PaletteId == paletteId)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static Dictionary<string, DiagramStencil> GetAllStencils()
        => new ExtendedDiagramStencilProvider()
            .GetStencilSets()
            .SelectMany(set => set.Stencils)
            .ToDictionary(stencil => stencil.Id, StringComparer.Ordinal);
}
