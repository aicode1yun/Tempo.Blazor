using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Components.Modeling;
using Tempo.Blazor.Modeling;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Modeling;

public sealed class Archimate32NotationProfileM19Tests : LocalizationTestBase
{
    [Fact]
    public void Archimate32_profile_exposes_m19_element_catalog()
    {
        var profile = new Archimate32NotationProfile();

        profile.NotationKey.Should().Be("archimate32");
        profile.DisplayName.Should().Be("ArchiMate 3.2");
        profile.SupportedElementTypes.Should().HaveCountGreaterThanOrEqualTo(56);
        profile.SupportedElementTypes.Should().Contain([
            "Capability",
            "CourseOfAction",
            "Resource",
            "ValueStream",
            "BusinessActor",
            "BusinessRole",
            "BusinessCollaboration",
            "BusinessInterface",
            "BusinessProcess",
            "BusinessFunction",
            "BusinessInteraction",
            "BusinessEvent",
            "BusinessService",
            "BusinessObject",
            "Contract",
            "Representation",
            "Product",
            "ApplicationComponent",
            "ApplicationCollaboration",
            "ApplicationInterface",
            "ApplicationFunction",
            "ApplicationInteraction",
            "ApplicationProcess",
            "ApplicationEvent",
            "ApplicationService",
            "DataObject",
            "Node",
            "Device",
            "SystemSoftware",
            "TechnologyCollaboration",
            "TechnologyInterface",
            "Path",
            "CommunicationNetwork",
            "TechnologyFunction",
            "TechnologyProcess",
            "TechnologyInteraction",
            "TechnologyEvent",
            "TechnologyService",
            "Artifact",
            "Equipment",
            "Facility",
            "DistributionNetwork",
            "Material",
            "Stakeholder",
            "Driver",
            "Assessment",
            "Goal",
            "Outcome",
            "Principle",
            "Requirement",
            "Constraint",
            "Meaning",
            "Value",
            "WorkPackage",
            "Deliverable",
            "ImplementationEvent",
            "Plateau",
            "Gap",
            "Junction",
            "Grouping",
            "Location"
        ]);
        profile.SupportedViewpointKeys.Should().Contain(["Layered", "Motivation", "Implementation", "Physical"]);
    }

    [Fact]
    public void Every_archimate32_element_type_has_existing_node_stencil_mapping()
    {
        var mapper = new Archimate32StencilMapper();
        var registry = CreateStencilRegistry();
        var profile = new Archimate32NotationProfile();

        foreach (var semanticType in profile.SupportedElementTypes)
        {
            var stencilId = mapper.GetStencilId(profile.NotationKey, semanticType);

            stencilId.Should().NotBeNull("ArchiMate 3.2 element type {0} must map to a diagram stencil", semanticType);
            registry.GetStencil(stencilId!).Should().NotBeNull("mapped ArchiMate 3.2 stencil {0} must exist", stencilId);
        }
    }

    [Fact]
    public void Archimate32_mapper_exposes_cross_cutting_elements_and_ignores_unknown_types()
    {
        var mapper = new Archimate32StencilMapper();

        mapper.GetStencilId("archimate32", "Junction").Should().Be("archimate3.cross.junction");
        mapper.GetStencilId("archimate32", "Grouping").Should().Be("archimate3.cross.grouping");
        mapper.GetStencilId("archimate32", "Location").Should().Be("archimate3.cross.location");
        mapper.GetStencilId("archimate32", "UnknownElement").Should().BeNull();
        mapper.GetStencilId("archimate", "BusinessProcess").Should().BeNull();
    }

    [Fact]
    public void Generator_skips_archimate32_elements_without_stencil_mapping()
    {
        var generator = CreateGenerator();
        var model = CreateModel(
            [
                Element("valid", "BusinessProcess", "Valid process"),
                Element("missing", "UnmappedElement", "Missing mapping")
            ]);

        var result = generator.Generate(model, new ModelingDiagramGenerationOptionsDto { ViewId = "archimate32-view" });

        result.Document!.Nodes.Should().ContainSingle().Which.StencilId.Should().Be("archimate3.business.process");
        result.Document.Nodes
            .Any(node => node.Data.TryGetValue("modelElementId", out var value) && Equals(value, "missing"))
            .Should().BeFalse();
        result.Issues.Should().ContainSingle(issue =>
            issue.SourceElementId == "missing"
            && issue.Category == "mapping"
            && issue.Message.Contains("skipped", StringComparison.OrdinalIgnoreCase)
            && issue.Message.Contains("UnmappedElement", StringComparison.Ordinal));
    }

    private static ModelingDiagramGenerator CreateGenerator()
    {
        var profiles = new ModelingNotationProfileRegistry([new Archimate32NotationProfile()]);
        return new ModelingDiagramGenerator(
            new BuiltInModelingStencilMapper(),
            CreateStencilRegistry(),
            notationProfiles: profiles,
            relationshipRules: new BuiltInModelingRelationshipRulesProvider(profiles));
    }

    private static BuiltInModelingRelationshipRulesProvider CreateRules()
        => new(new ModelingNotationProfileRegistry([new Archimate32NotationProfile()]));

    private static DiagramStencilRegistry CreateStencilRegistry()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
        registry.RegisterProvider(new Archimate3DiagramStencilProvider());
        return registry;
    }

    private static ModelingModelDto CreateModel(IReadOnlyCollection<ModelingElementDto> elements)
        => new()
        {
            Id = "archimate32-m19-test",
            Title = "ArchiMate 3.2 M19 test",
            Notation = "archimate32",
            SupportedNotations = ["archimate32"],
            Elements = elements.ToList(),
            Relationships = [],
            Views =
            [
                new ModelingViewDto
                {
                    Id = "archimate32-view",
                    Name = "ArchiMate 3.2 view",
                    Notation = "archimate32",
                    ViewpointKey = "Layered",
                    Nodes = elements.Select((element, index) => new ModelingViewNodeDto
                    {
                        ElementId = element.Id,
                        X = 100 + index * 220,
                        Y = 120,
                        Width = 160,
                        Height = 90
                    }).ToList()
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
