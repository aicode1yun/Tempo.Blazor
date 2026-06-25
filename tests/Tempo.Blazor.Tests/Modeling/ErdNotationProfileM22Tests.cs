using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Components.Modeling;
using Tempo.Blazor.Configuration;
using Tempo.Blazor.Modeling;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Modeling;

public sealed class ErdNotationProfileM22Tests : LocalizationTestBase
{
    [Fact]
    public void Erd_profile_exposes_m22_element_and_relationship_types()
    {
        var profile = new ErdNotationProfile();

        profile.NotationKey.Should().Be("erd");
        profile.DisplayName.Should().Be("ERD");
        profile.SupportedElementTypes.Should().Equal([
            "Entity",
            "WeakEntity",
            "Attribute",
            "MultiValuedAttribute",
            "DerivedAttribute",
            "KeyAttribute",
            "RelationshipSet",
            "WeakRelationshipSet"
        ]);
        profile.SupportedRelationshipTypes.Should().Equal([
            "OneToOne",
            "OneToMany",
            "ManyToMany",
            "Identifying",
            "NonIdentifying"
        ]);
    }

    [Fact]
    public void AddTempoBlazorModeling_does_not_register_erd_profile_by_default()
    {
        var services = new ServiceCollection();

        services.AddTempoBlazorModeling();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ModelingNotationProfileRegistry>();

        registry.GetProfile("erd").Should().BeNull();
    }

    [Fact]
    public void Consumer_can_register_erd_profile_after_AddTempoBlazorModeling()
    {
        var services = new ServiceCollection();

        services.AddTempoBlazorModeling();
        services.AddSingleton<IModelingNotationProfile, ErdNotationProfile>();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ModelingNotationProfileRegistry>();

        registry.GetProfile("erd").Should().BeOfType<ErdNotationProfile>();
    }

    [Fact]
    public void Erd_stencil_mapper_returns_null_until_consumer_registers_stencils()
    {
        var mapper = new ErdStencilMapper();

        mapper.GetStencilId("erd", "Entity").Should().BeNull();
        mapper.GetEdgeStencilId("erd", "OneToMany").Should().BeNull();
    }

    [Fact]
    public void Erd_model_without_stencils_reports_one_mapping_issue_per_element_and_returns_empty_document()
    {
        var profiles = new ModelingNotationProfileRegistry([new ErdNotationProfile()]);
        var generator = new ModelingDiagramGenerator(new BuiltInModelingStencilMapper(), CreateStencilRegistry(), notationProfiles: profiles);
        var model = CreateErdModel();

        var result = generator.Generate(model, new ModelingDiagramGenerationOptionsDto { ViewId = "erd-view" });

        result.Document.Should().NotBeNull();
        result.Document!.Nodes.Should().BeEmpty();
        result.Document.Edges.Should().BeEmpty();
        result.Issues.Should().HaveCount(model.Elements.Count);
        result.Issues.Should().OnlyContain(issue =>
            issue.Severity == ModelingIssueSeverity.Warning
            && issue.Category == "mapping"
            && issue.Message.Contains("No node stencil mapping", StringComparison.Ordinal)
            && issue.Message.Contains("was skipped", StringComparison.Ordinal)
            && issue.SuggestedFix.Contains("Register ERD diagram stencils", StringComparison.Ordinal));
    }

    [Fact]
    public void Erd_profile_appears_in_view_selector_when_consumer_registers_it()
    {
        Services.AddSingleton<IModelingNotationProfile, ErdNotationProfile>();

        using var cut = RenderComponent<TmModelingViewSelector>(parameters => parameters
            .Add(p => p.NotationKey, "erd"));

        var notationOptions = cut.Find("[data-testid='modeling-notation-select']").QuerySelectorAll("option");
        notationOptions.Should().Contain(option =>
            option.GetAttribute("value") == "erd"
            && option.TextContent.Contains("ERD", StringComparison.Ordinal));
        cut.Find("[data-testid='modeling-view-selector']").GetAttribute("data-notation").Should().Be("erd");
    }

    private static DiagramStencilRegistry CreateStencilRegistry()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
        return registry;
    }

    private static ModelingModelDto CreateErdModel()
    {
        var elements = new[]
        {
            Element("customer", "Entity", "Customer"),
            Element("order-line", "WeakEntity", "Order line"),
            Element("customer-id", "KeyAttribute", "Customer ID"),
            Element("places", "RelationshipSet", "Places")
        };

        return new ModelingModelDto
        {
            Id = "erd-no-stencils-test",
            Title = "ERD no stencils",
            Notation = ErdNotationProfile.Key,
            SupportedNotations = [ErdNotationProfile.Key],
            Elements = elements.ToList(),
            Relationships = [],
            Views =
            [
                new ModelingViewDto
                {
                    Id = "erd-view",
                    Name = "ERD",
                    Notation = ErdNotationProfile.Key,
                    Nodes = elements.Select((element, index) => new ModelingViewNodeDto
                    {
                        ElementId = element.Id,
                        X = 100 + index * 180,
                        Y = 120,
                        Width = 150,
                        Height = 70
                    }).ToList()
                }
            ]
        };
    }

    private static ModelingElementDto Element(string id, string semanticType, string name)
        => new()
        {
            Id = id,
            SourceId = id,
            SourceType = $"erd-{semanticType}",
            SourcePath = $"/ERD/{name}",
            Notation = ErdNotationProfile.Key,
            SemanticType = semanticType,
            Name = name,
            Properties = new Dictionary<string, System.Text.Json.JsonElement>()
        };
}
