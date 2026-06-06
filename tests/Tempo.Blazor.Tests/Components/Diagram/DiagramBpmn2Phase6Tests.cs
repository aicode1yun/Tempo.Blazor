using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Configuration;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class DiagramBpmn2Phase6Tests : LocalizationTestBase
{
    public DiagramBpmn2Phase6Tests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["DiagramStencilSet_Bpmn2"] = "BPMN 2.0",
            ["DiagramStencilPalette_Bpmn2General"] = "General",
            ["DiagramStencilPalette_Bpmn2Tasks"] = "Tasks",
            ["DiagramStencilPalette_Bpmn2Events"] = "Events",
            ["DiagramStencilPalette_Bpmn2Gateways"] = "Gateways",
            ["DiagramStencilPalette_Bpmn2Swimlanes"] = "Pools and Lanes",
            ["DiagramStencilPalette_Bpmn2Relationships"] = "Flows",
            ["DiagramStencil_Bpmn2UserTask"] = "User Task"
        });
    }

    [Fact]
    public void Provider_Exposes_Bpmn2_Set_And_Core_Palettes()
    {
        var set = new Bpmn2DiagramStencilProvider().GetStencilSets().Should().ContainSingle().Subject;
        var stencils = set.Stencils.ToList();

        set.Id.Should().Be("bpmn2");
        set.NameResourceKey.Should().Be("DiagramStencilSet_Bpmn2");
        stencils.Select(stencil => stencil.PaletteId)
            .Should().Contain([
                "bpmn2.general",
                "bpmn2.tasks",
                "bpmn2.events",
                "bpmn2.gateways",
                "bpmn2.swimlanes",
                "bpmn2.relationships"
            ]);
        stencils.All(stencil => stencil.SetId == "bpmn2").Should().BeTrue();
        stencils.All(stencil => stencil.SetNameResourceKey == "DiagramStencilSet_Bpmn2").Should().BeTrue();
        stencils.All(stencil => !string.IsNullOrWhiteSpace(stencil.PaletteNameResourceKey)).Should().BeTrue();
        stencils.All(stencil => !string.IsNullOrWhiteSpace(stencil.NameResourceKey)).Should().BeTrue();
        stencils.All(stencil => stencil.Origin == DiagramStencilOrigin.TempoOriginal).Should().BeTrue();
    }

    [Fact]
    public void Registry_Search_Matches_Bpmn2_UserTask_With_Library_Terms()
    {
        var registry = CreateRegistryWithBpmn2();

        registry.Search("BPMN 2.0 User Task")
            .Should().Contain(stencil => stencil.Id == "bpmn2.task.user");
    }

    [Fact]
    public void Bpmn2_Tasks_Palette_Contains_Core_And_Specialized_Task_Markers()
    {
        var tasks = GetBpmn2Stencils()
            .Where(stencil => stencil.PaletteId == "bpmn2.tasks")
            .ToDictionary(stencil => stencil.Id, StringComparer.Ordinal);

        tasks.Keys.Should().Contain([
            "bpmn2.task",
            "bpmn2.task.user",
            "bpmn2.task.service",
            "bpmn2.task.manual",
            "bpmn2.task.script",
            "bpmn2.task.business-rule",
            "bpmn2.task.send",
            "bpmn2.task.receive"
        ]);
        tasks["bpmn2.task.user"].Layout.ShapeSvg.Should().Contain("tm-bpmn-task-marker-user");
        tasks["bpmn2.task.service"].Layout.ShapeSvg.Should().Contain("tm-bpmn-task-marker-service");
        tasks.Values.All(stencil => stencil.DefaultWidth == 150 && stencil.DefaultHeight == 92).Should().BeTrue();
    }

    [Fact]
    public void Bpmn2_Subprocess_Stencils_Are_Collapsible()
    {
        var stencils = GetBpmn2Stencils().ToDictionary(stencil => stencil.Id, StringComparer.Ordinal);

        stencils.Keys.Should().Contain(["bpmn2.subprocess", "bpmn2.subprocess.collapsed"]);
        stencils["bpmn2.subprocess"].IsCollapsible.Should().BeTrue();
        stencils["bpmn2.subprocess.collapsed"].IsCollapsible.Should().BeTrue();
        stencils["bpmn2.subprocess.collapsed"].Layout.ShapeSvg.Should().Contain("tm-bpmn-subprocess-marker");
    }

    [Fact]
    public void Bpmn2_Events_Palette_Contains_Rings_Markers_And_NonInterrupting_Variant()
    {
        var events = GetBpmn2Stencils()
            .Where(stencil => stencil.PaletteId == "bpmn2.events")
            .ToDictionary(stencil => stencil.Id, StringComparer.Ordinal);

        events.Keys.Should().Contain([
            "bpmn2.event.start",
            "bpmn2.event.intermediate",
            "bpmn2.event.end",
            "bpmn2.event.message",
            "bpmn2.event.timer",
            "bpmn2.event.error",
            "bpmn2.event.signal",
            "bpmn2.event.terminate",
            "bpmn2.event.non-interrupting"
        ]);
        events["bpmn2.event.message"].Layout.ShapeSvg.Should().Contain("tm-bpmn-event-marker-message");
        events["bpmn2.event.timer"].Layout.ShapeSvg.Should().Contain("tm-bpmn-event-marker-timer");
        events["bpmn2.event.error"].Layout.ShapeSvg.Should().Contain("tm-bpmn-event-marker-error");
        events["bpmn2.event.non-interrupting"].Layout.ShapeSvg.Should().Contain("stroke-dasharray");
    }

    [Fact]
    public void Bpmn2_Gateways_Palette_Contains_Expected_Symbols()
    {
        var gateways = GetBpmn2Stencils()
            .Where(stencil => stencil.PaletteId == "bpmn2.gateways")
            .ToDictionary(stencil => stencil.Id, StringComparer.Ordinal);

        gateways.Keys.Should().Contain([
            "bpmn2.gateway.exclusive",
            "bpmn2.gateway.parallel",
            "bpmn2.gateway.inclusive",
            "bpmn2.gateway.event-based",
            "bpmn2.gateway.complex"
        ]);
        gateways["bpmn2.gateway.exclusive"].Layout.ShapeSvg.Should().Contain("tm-bpmn-gateway-marker-exclusive");
        gateways["bpmn2.gateway.parallel"].Layout.ShapeSvg.Should().Contain("tm-bpmn-gateway-marker-parallel");
        gateways.Values.All(stencil => stencil.DefaultWidth == 84 && stencil.DefaultHeight == 84).Should().BeTrue();
    }

    [Fact]
    public void Bpmn2_Pool_And_Lane_Use_Swimlane_Data()
    {
        var stencils = GetBpmn2Stencils().ToDictionary(stencil => stencil.Id, StringComparer.Ordinal);

        stencils.Keys.Should().Contain(["bpmn2.pool", "bpmn2.lane"]);
        stencils["bpmn2.pool"].IsSwimlane.Should().BeTrue();
        stencils["bpmn2.pool"].IsCollapsible.Should().BeTrue();
        stencils["bpmn2.pool"].Layout.BackgroundShape.Should().Be("swimlane-horizontal");
        stencils["bpmn2.pool"].Layout.Sections.Should().Contain(section => section.Type == "swimlane");
        stencils["bpmn2.lane"].IsSwimlane.Should().BeTrue();
        stencils["bpmn2.lane"].Layout.BackgroundShape.Should().Be("swimlane-horizontal");
        stencils["bpmn2.lane"].Layout.Sections.Should().Contain(section => section.Type == "swimlane");
    }

    [Fact]
    public void Bpmn2_Relationship_Edge_Presets_Create_Valid_Edges()
    {
        var edges = GetBpmn2Stencils()
            .Where(stencil => stencil.Kind == DiagramStencilKind.Edge)
            .ToDictionary(stencil => stencil.Id, StringComparer.Ordinal);

        edges.Keys.Should().Contain([
            "bpmn2.flow.sequence",
            "bpmn2.flow.conditional",
            "bpmn2.flow.default",
            "bpmn2.flow.message",
            "bpmn2.association",
            "bpmn2.data-association"
        ]);

        var sequence = DiagramEdgeStencilFactory.CreateEdge(edges["bpmn2.flow.sequence"], "source", null, "target", null);
        sequence.IsValid().Should().BeTrue();
        sequence.ConnectorType.Should().Be("bpmn-sequence-flow");
        sequence.EndArrow.Should().Be("block");
        sequence.EndArrowFill.Should().BeTrue();

        var message = DiagramEdgeStencilFactory.CreateEdge(edges["bpmn2.flow.message"], "source", null, "target", null);
        message.ConnectorType.Should().Be("bpmn-message-flow");
        message.Style.StrokeDashPattern.Should().Be("dashed");
        message.StartArrow.Should().Be("oval");
        message.EndArrow.Should().Be("open");

        var association = DiagramEdgeStencilFactory.CreateEdge(edges["bpmn2.association"], "source", null, "target", null);
        association.EndArrow.Should().Be("none");
        association.Style.StrokeDashPattern.Should().Be("dotted");
    }

    [Fact]
    public void AddTempoBlazor_Registers_Bpmn2_Provider()
    {
        var services = new ServiceCollection();

        services.AddTempoBlazor();

        var provider = services.BuildServiceProvider();
        provider.GetServices<IDiagramStencilProvider>()
            .Should().Contain(provider => provider is Bpmn2DiagramStencilProvider);
        provider.GetRequiredService<DiagramStencilRegistry>()
            .GetStencil("bpmn2.task.user")
            .Should().NotBeNull();
    }

    private static List<DiagramStencil> GetBpmn2Stencils()
        => new Bpmn2DiagramStencilProvider()
            .GetStencilSets()
            .SelectMany(set => set.Stencils)
            .ToList();

    private static DiagramStencilRegistry CreateRegistryWithBpmn2()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new Bpmn2DiagramStencilProvider());
        return registry;
    }
}
