using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Components.Modeling;
using Tempo.Blazor.Modeling;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Modeling;

public sealed class UmlNotationProfileM18Tests : LocalizationTestBase
{
    public UmlNotationProfileM18Tests()
    {
        Services.AddSingleton(CreateStencilRegistry());
    }

    [Fact]
    public void Uml_profile_exposes_m18_notation_types_relationships_and_viewpoints()
    {
        var profile = new UmlNotationProfile();

        profile.NotationKey.Should().Be("uml25");
        profile.SupportedElementTypes.Should().Equal([
            "Class",
            "Interface",
            "AbstractClass",
            "Enumeration",
            "Package",
            "Component",
            "Node",
            "Artifact",
            "UseCase",
            "Actor",
            "State",
            "PseudoState",
            "Activity",
            "Action",
            "ObjectNode",
            "Lifeline",
            "CombinedFragment",
            "Collaboration"
        ]);
        profile.SupportedRelationshipTypes.Should().Equal([
            "Association",
            "DirectedAssociation",
            "Aggregation",
            "Composition",
            "Dependency",
            "Generalization",
            "Realization",
            "Include",
            "Extend",
            "Message"
        ]);
        profile.SupportedViewpointKeys.Should().Equal([
            "ClassDiagram",
            "PackageDiagram",
            "UseCaseDiagram",
            "ActivityDiagram",
            "StateMachineDiagram",
            "SequenceDiagram",
            "CommunicationDiagram",
            "ComponentDiagram",
            "DeploymentDiagram",
            "ObjectDiagram"
        ]);
    }

    [Fact]
    public void Uml_message_is_a_relationship_not_a_node_element()
    {
        var profile = new UmlNotationProfile();
        var mapper = new UmlStencilMapper();
        var registry = CreateStencilRegistry();

        profile.SupportedElementTypes.Should().NotContain("Message");
        profile.SupportedRelationshipTypes.Should().Contain("Message");
        mapper.GetStencilId(profile.NotationKey, "Message").Should().BeNull();
        mapper.GetEdgeStencilId(profile.NotationKey, "Message").Should().Be("uml25.sequence-message");
        registry.GetStencil("uml25.sequence-message")!.Kind.Should().Be(DiagramStencilKind.Edge);
    }

    [Fact]
    public void All_uml_element_types_have_existing_node_stencil_mapping()
    {
        var mapper = new UmlStencilMapper();
        var registry = CreateStencilRegistry();
        var profile = new UmlNotationProfile();

        foreach (var semanticType in profile.SupportedElementTypes)
        {
            var stencilId = mapper.GetStencilId(profile.NotationKey, semanticType);

            stencilId.Should().NotBeNull("UML element type {0} must map to a diagram stencil", semanticType);
            registry.GetStencil(stencilId!).Should().NotBeNull("mapped UML stencil {0} must exist", stencilId);
        }
    }

    [Fact]
    public void Uml_relationship_rules_restrict_include_and_extend_to_use_case_diagram()
    {
        var rules = CreateRules();
        var useCaseContext = CreateRelationshipContext(
            "UseCaseDiagram",
            Relationship("include-ok", "checkout", "payment", "Include"),
            Element("checkout", "UseCase", "Checkout"),
            Element("payment", "UseCase", "Authorize payment"));
        var classContext = CreateRelationshipContext(
            "ClassDiagram",
            Relationship("include-bad", "checkout", "payment", "Include"),
            Element("checkout", "UseCase", "Checkout"),
            Element("payment", "UseCase", "Authorize payment"));

        rules.ValidateRelationship(useCaseContext).IsValid.Should().BeTrue();
        rules.ValidateRelationship(classContext).IsValid.Should().BeFalse();
        rules.ValidateRelationship(classContext).Message.Should().Contain("UseCaseDiagram");
    }

    [Fact]
    public void Uml_generalization_has_explicit_class_and_deployment_diagram_behavior()
    {
        var rules = CreateRules();
        var classContext = CreateRelationshipContext(
            "ClassDiagram",
            Relationship("generalization-class", "order", "aggregate", "Generalization"),
            Element("order", "Class", "Order"),
            Element("aggregate", "AbstractClass", "AggregateRoot"));
        var deploymentContext = CreateRelationshipContext(
            "DeploymentDiagram",
            Relationship("generalization-deployment", "node-a", "node-b", "Generalization"),
            Element("node-a", "Node", "App node"),
            Element("node-b", "Node", "Base node"));

        rules.ValidateRelationship(classContext).IsValid.Should().BeTrue();
        rules.ValidateRelationship(deploymentContext).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Generator_warns_when_actor_is_used_outside_use_case_diagram()
    {
        var generator = CreateGenerator();
        var model = CreateUmlModel(
            [Element("actor", "Actor", "Customer")],
            [],
            "ClassDiagram");

        var result = generator.Generate(model, new ModelingDiagramGenerationOptionsDto { ViewId = "uml-view", ViewpointKey = "ClassDiagram" });

        result.Document!.Nodes.Should().ContainSingle().Which.StencilId.Should().Be("uml25.actor");
        result.Issues.Should().ContainSingle(issue =>
            issue.Severity == ModelingIssueSeverity.Warning
            && issue.Category == "viewpoint"
            && issue.SourceElementId == "actor"
            && issue.Message.Contains("Actor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generator_skips_include_relationship_in_class_diagram()
    {
        var generator = CreateGenerator();
        var model = CreateUmlModel(
            [Element("source", "UseCase", "Source"), Element("target", "UseCase", "Target")],
            [Relationship("include-invalid", "source", "target", "Include")],
            "ClassDiagram");

        var result = generator.Generate(model, new ModelingDiagramGenerationOptionsDto { ViewId = "uml-view", ViewpointKey = "ClassDiagram" });

        result.Document!.Edges.Should().BeEmpty();
        result.Issues.Should().ContainSingle(issue =>
            issue.SourceRelationshipId == "include-invalid"
            && issue.Message.Contains("UseCaseDiagram", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Uml_class_without_attributes_renders_header_without_empty_compartments()
    {
        var registry = CreateStencilRegistry();
        var stencil = registry.GetStencil("uml25.class")!;
        var node = new DiagramNode
        {
            Id = "empty-class",
            StencilId = stencil.Id,
            W = stencil.DefaultWidth,
            H = 72,
            Data =
            {
                ["name"] = "Marker",
                ["attributes"] = Array.Empty<string>(),
                ["operations"] = Array.Empty<string>()
            }
        };

        var cut = RenderComponent<TmDiagramStencilShape>(parameters => parameters.Add(p => p.Node, node));

        cut.Markup.Should().Contain("Marker");
        cut.FindAll(".tm-diagram-node__list-item").Should().BeEmpty();
        cut.FindAll(".tm-diagram-node__divider").Should().BeEmpty();
    }

    private static UmlRelationshipRulesProvider CreateRules()
        => new(new ModelingNotationProfileRegistry([new UmlNotationProfile()]));

    private static ModelingDiagramGenerator CreateGenerator()
    {
        var profiles = new ModelingNotationProfileRegistry([new UmlNotationProfile()]);
        return new ModelingDiagramGenerator(
            new BuiltInModelingStencilMapper(),
            CreateStencilRegistry(),
            notationProfiles: profiles,
            relationshipRules: new BuiltInModelingRelationshipRulesProvider(profiles),
            viewpointRules: new BuiltInModelingViewpointRulesProvider(profiles));
    }

    private static DiagramStencilRegistry CreateStencilRegistry()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
        registry.RegisterProvider(new Uml25DiagramStencilProvider());
        return registry;
    }

    private static ModelingModelDto CreateUmlModel(
        IReadOnlyCollection<ModelingElementDto> elements,
        IReadOnlyCollection<ModelingRelationshipDto> relationships,
        string viewpoint)
        => new()
        {
            Id = "uml-m18-test",
            Title = "UML M18 test",
            Notation = "uml25",
            SupportedNotations = ["uml25"],
            Elements = elements.ToList(),
            Relationships = relationships.ToList(),
            Views =
            [
                new ModelingViewDto
                {
                    Id = "uml-view",
                    Name = viewpoint,
                    Notation = "uml25",
                    ViewpointKey = viewpoint,
                    Nodes = elements.Select((element, index) => new ModelingViewNodeDto
                    {
                        ElementId = element.Id,
                        X = 100 + index * 220,
                        Y = 120,
                        Width = 160,
                        Height = 90
                    }).ToList(),
                    Connections = relationships.Select(relationship => new ModelingViewConnectionDto
                    {
                        RelationshipId = relationship.Id,
                        SourceNodeId = relationship.SourceElementId,
                        TargetNodeId = relationship.TargetElementId
                    }).ToList()
                }
            ]
        };

    private static ModelingRelationshipRuleContext CreateRelationshipContext(
        string viewpoint,
        ModelingRelationshipDto relationship,
        params ModelingElementDto[] elements)
    {
        var byId = elements.ToDictionary(element => element.Id, StringComparer.Ordinal);
        return new ModelingRelationshipRuleContext
        {
            NotationKey = "uml25",
            ViewpointKey = viewpoint,
            Relationship = relationship,
            SourceElement = byId[relationship.SourceElementId],
            TargetElement = byId[relationship.TargetElementId],
            ElementsById = byId
        };
    }

    private static ModelingElementDto Element(string id, string semanticType, string name)
        => new()
        {
            Id = id,
            SourceId = $"m18/{id}",
            SourceType = semanticType,
            SourcePath = $"/M18/{id}",
            Notation = "uml25",
            SemanticType = semanticType,
            Name = name
        };

    private static ModelingRelationshipDto Relationship(string id, string sourceId, string targetId, string relationshipType)
        => new()
        {
            Id = id,
            SourceId = $"m18/{id}",
            SourceType = relationshipType,
            SourceElementId = sourceId,
            TargetElementId = targetId,
            RelationshipType = relationshipType
        };
}
