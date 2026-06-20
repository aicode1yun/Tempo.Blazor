using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Components.Modeling;
using Tempo.Blazor.Modeling;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Modeling;

public sealed class ArchimateRelationshipMatrixM20Tests : LocalizationTestBase
{
    public static TheoryData<string, string, string> KnownValidRelationships => new()
    {
        { "BusinessProcess", "ApplicationComponent", "Association" },
        { "BusinessProcess", "BusinessProcess", "Specialization" },
        { "BusinessActor", "BusinessProcess", "Assignment" },
        { "ApplicationComponent", "ApplicationFunction", "Assignment" },
        { "Node", "TechnologyFunction", "Assignment" },
        { "WorkPackage", "Deliverable", "Assignment" },
        { "BusinessProcess", "BusinessService", "Realization" },
        { "ApplicationComponent", "ApplicationService", "Realization" },
        { "TechnologyFunction", "TechnologyService", "Realization" },
        { "Requirement", "Goal", "Realization" },
        { "Resource", "Capability", "Realization" },
        { "ApplicationService", "BusinessProcess", "Serving" },
        { "TechnologyService", "ApplicationComponent", "Serving" },
        { "BusinessService", "BusinessProcess", "Serving" },
        { "BusinessProcess", "BusinessObject", "Access" },
        { "ApplicationFunction", "DataObject", "Access" },
        { "TechnologyFunction", "Artifact", "Access" },
        { "Goal", "Requirement", "Influence" },
        { "Driver", "Goal", "Influence" },
        { "BusinessEvent", "BusinessProcess", "Triggering" },
        { "BusinessProcess", "ApplicationFunction", "Triggering" },
        { "ApplicationFunction", "BusinessProcess", "Flow" },
        { "Grouping", "BusinessProcess", "Composition" },
        { "BusinessCollaboration", "BusinessRole", "Aggregation" }
    };

    public static TheoryData<string, string, string> KnownInvalidRelationships => new()
    {
        { "BusinessProcess", "ApplicationComponent", "Serving" },
        { "ApplicationComponent", "BusinessProcess", "Serving" },
        { "BusinessObject", "BusinessProcess", "Assignment" },
        { "BusinessObject", "BusinessProcess", "Access" },
        { "Goal", "Requirement", "Realization" },
        { "BusinessProcess", "Goal", "Influence" },
        { "BusinessActor", "BusinessProcess", "Triggering" },
        { "BusinessObject", "BusinessObject", "Flow" },
        { "BusinessProcess", "ApplicationProcess", "Specialization" },
        { "BusinessProcess", "ApplicationComponent", "Composition" },
        { "ApplicationService", "TechnologyService", "Serving" }
    };

    [Theory]
    [MemberData(nameof(KnownValidRelationships))]
    public void Matrix_accepts_known_valid_archimate32_relationships(string sourceType, string targetType, string relationshipType)
    {
        var rules = CreateRules();

        rules.IsValidRelationship("archimate32", sourceType, targetType, relationshipType).Should().BeTrue();
        ArchimateRelationshipMatrix.IsValid(sourceType, targetType, relationshipType).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(KnownInvalidRelationships))]
    public void Matrix_rejects_known_invalid_archimate32_relationships(string sourceType, string targetType, string relationshipType)
    {
        var rules = CreateRules();

        rules.IsValidRelationship("archimate32", sourceType, targetType, relationshipType).Should().BeFalse();
        ArchimateRelationshipMatrix.IsValid(sourceType, targetType, relationshipType).Should().BeFalse();
    }

    [Fact]
    public void Matrix_handles_unknown_types_without_throwing()
    {
        var rules = CreateRules();

        rules.IsValidRelationship("archimate32", "UnknownElement", "BusinessProcess", "Serving").Should().BeFalse();
        rules.IsValidRelationship("archimate32", "BusinessProcess", "BusinessService", "UnknownRelationship").Should().BeFalse();
        ArchimateRelationshipMatrix.IsValid("BusinessProcess", "BusinessService", "UnknownRelationship").Should().BeFalse();
    }

    [Fact]
    public void Association_is_valid_between_any_known_archimate32_elements()
    {
        var rules = CreateRules();

        rules.IsValidRelationship("archimate32", "Stakeholder", "Artifact", "Association").Should().BeTrue();
        rules.IsValidRelationship("archimate32", "Material", "Goal", "Association").Should().BeTrue();
    }

    [Fact]
    public void Specialization_is_valid_only_between_same_element_type()
    {
        var rules = CreateRules();

        rules.IsValidRelationship("archimate32", "ApplicationComponent", "ApplicationComponent", "Specialization").Should().BeTrue();
        rules.IsValidRelationship("archimate32", "ApplicationComponent", "ApplicationCollaboration", "Specialization").Should().BeFalse();
    }

    [Fact]
    public void Motivation_elements_accept_influence_relationships()
    {
        var rules = CreateRules();

        rules.IsValidRelationship("archimate32", "Driver", "Assessment", "Influence").Should().BeTrue();
        rules.IsValidRelationship("archimate32", "Goal", "Requirement", "Influence").Should().BeTrue();
    }

    [Fact]
    public void Built_in_relationship_provider_delegates_to_archimate32_matrix()
    {
        var rules = new BuiltInModelingRelationshipRulesProvider(CreateProfiles());

        rules.IsValidRelationship("archimate32", "ApplicationService", "BusinessProcess", "Serving").Should().BeTrue();
        rules.IsValidRelationship("archimate32", "BusinessProcess", "ApplicationComponent", "Serving").Should().BeFalse();
    }

    [Fact]
    public void Generator_skips_invalid_archimate32_relationship_and_reports_warning()
    {
        var profiles = CreateProfiles();
        var generator = new ModelingDiagramGenerator(
            new BuiltInModelingStencilMapper(),
            CreateStencilRegistry(),
            notationProfiles: profiles,
            relationshipRules: new BuiltInModelingRelationshipRulesProvider(profiles));
        var model = CreateInvalidServingModel();

        var result = generator.Generate(model, new ModelingDiagramGenerationOptionsDto { ViewId = "archimate32-invalid-view" });

        result.Document!.Edges.Should().BeEmpty();
        result.Issues.Should().ContainSingle(issue =>
            issue.SourceRelationshipId == "invalid-serving"
            && issue.Category == "validation"
            && issue.Severity == ModelingIssueSeverity.Warning
            && issue.Message.Contains("BusinessProcess", StringComparison.Ordinal)
            && issue.Message.Contains("ApplicationComponent", StringComparison.Ordinal));
    }

    private static Archimate32RelationshipRulesProvider CreateRules()
        => new(CreateProfiles());

    private static ModelingNotationProfileRegistry CreateProfiles()
        => new([new Archimate32NotationProfile()]);

    private static DiagramStencilRegistry CreateStencilRegistry()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
        registry.RegisterProvider(new Archimate3DiagramStencilProvider());
        return registry;
    }

    private static ModelingModelDto CreateInvalidServingModel()
        => new()
        {
            Id = "archimate32-m20-invalid-test",
            Title = "ArchiMate 3.2 invalid serving",
            Notation = "archimate32",
            SupportedNotations = ["archimate32"],
            Elements =
            [
                Element("source", "BusinessProcess", "Source"),
                Element("target", "ApplicationComponent", "Target")
            ],
            Relationships =
            [
                new()
                {
                    Id = "invalid-serving",
                    SourceId = "invalid-serving",
                    SourceElementId = "source",
                    TargetElementId = "target",
                    RelationshipType = "Serving",
                    Name = "Invalid serving"
                }
            ],
            Views =
            [
                new ModelingViewDto
                {
                    Id = "archimate32-invalid-view",
                    Name = "Invalid",
                    Notation = "archimate32",
                    ViewpointKey = "Layered",
                    Nodes =
                    [
                        new() { ElementId = "source", X = 120, Y = 120, Width = 160, Height = 80 },
                        new() { ElementId = "target", X = 420, Y = 120, Width = 160, Height = 80 }
                    ],
                    Connections =
                    [
                        new() { RelationshipId = "invalid-serving", SourceNodeId = "source", TargetNodeId = "target" }
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
