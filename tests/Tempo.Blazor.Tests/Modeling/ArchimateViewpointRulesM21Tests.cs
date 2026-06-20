using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Components.Modeling;
using Tempo.Blazor.Modeling;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Modeling;

public sealed class ArchimateViewpointRulesM21Tests : LocalizationTestBase
{
    private static readonly string[] M21ViewpointKeys =
    [
        "Organization",
        "BusinessProcess",
        "ApplicationUsage",
        "ApplicationCooperation",
        "ApplicationStructure",
        "TechnologyUsage",
        "TechnologyCooperation",
        "TechnologyInfrastructure",
        "PhysicalEnvironment",
        "Implementation",
        "MigrationMap",
        "Motivation",
        "StrategyEnvironment",
        "Capability",
        "ValueStream",
        "ProjectStatus",
        "LandscapeMap"
    ];

    public static TheoryData<string> M21Viewpoints => new()
    {
        M21ViewpointKeys[0],
        M21ViewpointKeys[1],
        M21ViewpointKeys[2],
        M21ViewpointKeys[3],
        M21ViewpointKeys[4],
        M21ViewpointKeys[5],
        M21ViewpointKeys[6],
        M21ViewpointKeys[7],
        M21ViewpointKeys[8],
        M21ViewpointKeys[9],
        M21ViewpointKeys[10],
        M21ViewpointKeys[11],
        M21ViewpointKeys[12],
        M21ViewpointKeys[13],
        M21ViewpointKeys[14],
        M21ViewpointKeys[15],
        M21ViewpointKeys[16]
    };

    [Fact]
    public void Archimate32_profile_exposes_m21_viewpoints()
    {
        var profile = new Archimate32NotationProfile();

        profile.SupportedViewpointKeys.Should().Contain(M21ViewpointKeys);
    }

    [Fact]
    public void Organization_viewpoint_allows_business_actor()
    {
        var rules = new Archimate32ViewpointRulesProvider();

        rules.IsElementAllowedInViewpoint("archimate32", "Organization", "BusinessActor").Should().BeTrue();
    }

    [Fact]
    public void Business_process_viewpoint_rejects_application_component()
    {
        var rules = new Archimate32ViewpointRulesProvider();

        // The ArchiMate Business Process viewpoint focuses on business behavior and passive/serving business context;
        // application structure belongs to Application Usage, Application Cooperation, or Application Structure viewpoints.
        rules.IsElementAllowedInViewpoint("archimate32", "BusinessProcess", "ApplicationComponent").Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(M21Viewpoints))]
    public void Junction_and_grouping_are_allowed_in_every_m21_viewpoint(string viewpoint)
    {
        var rules = new Archimate32ViewpointRulesProvider();

        rules.IsElementAllowedInViewpoint("archimate32", viewpoint, "Junction").Should().BeTrue();
        rules.IsElementAllowedInViewpoint("archimate32", viewpoint, "Grouping").Should().BeTrue();
    }

    [Fact]
    public void Unknown_viewpoint_key_rejects_elements_without_throwing()
    {
        var rules = new Archimate32ViewpointRulesProvider();

        rules.IsElementAllowedInViewpoint("archimate32", "UnknownViewpoint", "BusinessActor").Should().BeFalse();
    }

    [Fact]
    public void Empty_viewpoint_key_disables_filtering()
    {
        var rules = new Archimate32ViewpointRulesProvider();

        rules.IsElementAllowedInViewpoint("archimate32", string.Empty, "BusinessActor").Should().BeTrue();
        rules.IsElementAllowedInViewpoint("archimate32", string.Empty, "ApplicationComponent").Should().BeTrue();
        rules.IsElementAllowedInViewpoint("archimate32", string.Empty, "Goal").Should().BeTrue();
    }

    [Fact]
    public void Built_in_viewpoint_provider_delegates_to_archimate32_rules()
    {
        var rules = new BuiltInModelingViewpointRulesProvider(CreateProfiles());

        rules.IsElementAllowedInViewpoint("archimate32", "ApplicationUsage", "ApplicationComponent").Should().BeTrue();
        rules.IsElementAllowedInViewpoint("archimate32", "ApplicationUsage", "TechnologyService").Should().BeTrue();
        rules.IsElementAllowedInViewpoint("archimate32", "ApplicationUsage", "BusinessActor").Should().BeFalse();
    }

    [Fact]
    public void Generator_skips_elements_outside_selected_archimate32_viewpoint_and_reports_warning()
    {
        var profiles = CreateProfiles();
        var generator = new ModelingDiagramGenerator(
            new BuiltInModelingStencilMapper(),
            CreateStencilRegistry(),
            notationProfiles: profiles,
            relationshipRules: new BuiltInModelingRelationshipRulesProvider(profiles),
            viewpointRules: new BuiltInModelingViewpointRulesProvider(profiles));
        var model = CreateMixedViewpointModel();

        var result = generator.Generate(model, new ModelingDiagramGenerationOptionsDto { ViewId = "mixed-application-usage" });

        result.Document!.Nodes.Should().ContainSingle(node => (string)node.Data["modelElementId"] == "app");
        result.Document.Nodes.Should().NotContain(node => (string)node.Data["modelElementId"] == "business");
        result.Issues.Should().ContainSingle(issue =>
            issue.SourceElementId == "business"
            && issue.Category == "viewpoint"
            && issue.Severity == ModelingIssueSeverity.Warning
            && issue.Message.Contains("BusinessActor", StringComparison.Ordinal)
            && issue.Message.Contains("ApplicationUsage", StringComparison.Ordinal));
    }

    private static ModelingNotationProfileRegistry CreateProfiles()
        => new([new Archimate32NotationProfile()]);

    private static DiagramStencilRegistry CreateStencilRegistry()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
        registry.RegisterProvider(new Archimate3DiagramStencilProvider());
        return registry;
    }

    private static ModelingModelDto CreateMixedViewpointModel()
        => new()
        {
            Id = "archimate32-m21-mixed-test",
            Title = "ArchiMate 3.2 viewpoint test",
            Notation = "archimate32",
            SupportedNotations = ["archimate32"],
            Elements =
            [
                Element("business", "BusinessActor", "Customer"),
                Element("app", "ApplicationComponent", "Order portal")
            ],
            Views =
            [
                new ModelingViewDto
                {
                    Id = "mixed-application-usage",
                    Name = "Application usage",
                    Notation = "archimate32",
                    ViewpointKey = "ApplicationUsage",
                    Nodes =
                    [
                        new() { ElementId = "business", X = 120, Y = 120, Width = 160, Height = 80 },
                        new() { ElementId = "app", X = 420, Y = 120, Width = 160, Height = 80 }
                    ]
                }
            ]
        };

    private static ModelingElementDto Element(string id, string semanticType, string name)
        => new()
        {
            Id = id,
            SourceId = id,
            SourceType = $"archimate32-{semanticType}",
            SourcePath = $"/ArchiMate 3.2/{name}",
            Notation = "archimate32",
            SemanticType = semanticType,
            Name = name,
            Properties = new Dictionary<string, System.Text.Json.JsonElement>()
        };
}
