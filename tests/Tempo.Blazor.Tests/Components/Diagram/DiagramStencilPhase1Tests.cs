using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Stencils;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class DiagramStencilPhase1Tests
{
    [Fact]
    public void DiagramStencil_DefaultKind_Is_Node()
    {
        var stencil = new DiagramStencil();

        stencil.Kind.Should().Be(DiagramStencilKind.Node);
    }

    [Fact]
    public void DiagramStencil_Can_Be_Marked_As_Edge_With_Defaults()
    {
        var stencil = new DiagramStencil
        {
            Kind = DiagramStencilKind.Edge,
            EdgeDefaults = new()
            {
                Routing = "orthogonal",
                ConnectorType = "dependency",
                Shape = "connector",
                StartArrow = "none",
                EndArrow = "open",
                EndArrowFill = false
            }
        };

        stencil.Kind.Should().Be(DiagramStencilKind.Edge);
        stencil.EdgeDefaults.Should().NotBeNull();
        stencil.EdgeDefaults!.Routing.Should().Be("orthogonal");
        stencil.EdgeDefaults.ConnectorType.Should().Be("dependency");
        stencil.EdgeDefaults.EndArrow.Should().Be("open");
        stencil.EdgeDefaults.EndArrowFill.Should().BeFalse();
    }

    [Fact]
    public void DiagramStencilKind_Serializes_As_CamelCase_String()
    {
        var stencil = new DiagramStencil
        {
            Id = "edge.dependency",
            Name = "Dependency",
            Category = "UML",
            Origin = DiagramStencilOrigin.TempoOriginal,
            Kind = DiagramStencilKind.Edge,
            EdgeDefaults = new()
            {
                Routing = "straight",
                ConnectorType = "dependency",
                EndArrow = "open"
            }
        };

        var json = JsonSerializer.Serialize(stencil, DiagramJsonOptions.Default);
        var restored = JsonSerializer.Deserialize<DiagramStencil>(json, DiagramJsonOptions.Default);

        json.Should().Contain("\"kind\": \"edge\"");
        json.Should().Contain("\"edgeDefaults\"");
        restored.Should().NotBeNull();
        restored!.Kind.Should().Be(DiagramStencilKind.Edge);
        restored.EdgeDefaults!.ConnectorType.Should().Be("dependency");
    }

    [Fact]
    public void DiagramStencil_Metadata_Serializes_With_Normalized_Collections()
    {
        var stencil = new DiagramStencil
        {
            Id = "uml25.class",
            Name = "Class",
            Category = "UML",
            Origin = DiagramStencilOrigin.TempoOriginal,
            SetId = "uml25",
            PaletteId = "uml25.class",
            Order = 20,
            Tags = ["uml", "classifier"],
            Keywords = ["class", "object"]
        };

        var json = JsonSerializer.Serialize(stencil, DiagramJsonOptions.Default);
        var restored = JsonSerializer.Deserialize<DiagramStencil>(json, DiagramJsonOptions.Default);

        json.Should().Contain("\"setId\": \"uml25\"");
        json.Should().Contain("\"paletteId\": \"uml25.class\"");
        json.Should().Contain("\"order\": 20");
        restored.Should().NotBeNull();
        restored!.Tags.Should().BeEquivalentTo(["uml", "classifier"]);
        restored.Keywords.Should().BeEquivalentTo(["class", "object"]);
    }

    [Fact]
    public void Registry_GetAll_Orders_By_Set_Palette_Order_Then_Name()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterStencil(CreateStencil("bpmn2.task.user", "User Task", "bpmn2", "bpmn2.tasks", 20));
        registry.RegisterStencil(CreateStencil("uml25.usecase.actor", "Actor", "uml25", "uml25.usecase", 10));
        registry.RegisterStencil(CreateStencil("uml25.class.interface", "Interface", "uml25", "uml25.class", 30));
        registry.RegisterStencil(CreateStencil("uml25.class.class", "Class", "uml25", "uml25.class", 10));

        var ids = registry.GetAll().Select(stencil => stencil.Id).ToList();

        ids.Should().Equal(
            "bpmn2.task.user",
            "uml25.class.class",
            "uml25.class.interface",
            "uml25.usecase.actor");
    }

    [Fact]
    public void Registry_Search_Matches_Name_Tags_And_Keywords()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterStencil(CreateStencil(
            "uml25.class.class",
            "Class",
            "uml25",
            "uml25.class",
            10,
            tags: ["uml", "classifier"],
            keywords: ["object", "type"]));
        registry.RegisterStencil(CreateStencil(
            "bpmn2.task.user",
            "User Task",
            "bpmn2",
            "bpmn2.tasks",
            20,
            tags: ["process"],
            keywords: ["workflow"]));

        registry.Search("classifier").Should().ContainSingle().Which.Id.Should().Be("uml25.class.class");
        registry.Search("workflow").Should().ContainSingle().Which.Id.Should().Be("bpmn2.task.user");
        registry.Search("user").Should().ContainSingle().Which.Id.Should().Be("bpmn2.task.user");
        registry.Search("missing").Should().BeEmpty();
    }

    private static DiagramStencil CreateStencil(
        string id,
        string name,
        string setId,
        string paletteId,
        int order,
        string[]? tags = null,
        string[]? keywords = null)
        => new()
        {
            Id = id,
            Name = name,
            Category = setId,
            Origin = DiagramStencilOrigin.TempoOriginal,
            SetId = setId,
            PaletteId = paletteId,
            Order = order,
            Tags = tags?.ToList() ?? [],
            Keywords = keywords?.ToList() ?? []
        };
}
