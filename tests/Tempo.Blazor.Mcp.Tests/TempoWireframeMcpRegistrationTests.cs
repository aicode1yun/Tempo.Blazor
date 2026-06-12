using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Mcp;

namespace Tempo.Blazor.Mcp.Tests;

/// <summary>
/// Guards the MCP tool contract: registration wires the schema registry, and every tool exposes a
/// stable snake_case name and a non-empty description (snapshot of the LLM-facing surface).
/// </summary>
public class TempoWireframeMcpRegistrationTests
{
    private static IEnumerable<MethodInfo> ToolMethods()
        => TempoWireframeMcp.ToolTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

    [Fact]
    public void AddTempoWireframeMcpTools_RegistersSchemaRegistry()
    {
        var services = new ServiceCollection();
        services.AddTempoWireframeMcpTools();

        using var provider = services.BuildServiceProvider();
        provider.GetService<WireframeSchemaRegistry>().Should().NotBeNull();
    }

    [Fact]
    public void EveryTool_HasNameAndDescription()
    {
        foreach (var method in ToolMethods())
        {
            var tool = method.GetCustomAttribute<McpServerToolAttribute>()!;
            var description = method.GetCustomAttribute<DescriptionAttribute>();

            tool.Name.Should().NotBeNullOrWhiteSpace($"{method.Name} must have a stable tool name");
            description.Should().NotBeNull($"{method.Name} must have a description for the LLM");
            description!.Description.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ToolNames_MatchExpectedContract()
    {
        var names = ToolMethods()
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()!.Name)
            .OrderBy(n => n)
            .ToList();

        names.Should().BeEquivalentTo(new[]
        {
            "wireframe_apply_operations",
            "wireframe_create_document",
            "wireframe_get_component_schema",
            "wireframe_get_document",
            "wireframe_get_implementation_brief",
            "wireframe_list_components",
            "wireframe_list_documents",
            "wireframe_replace_document",
            "wireframe_validate_document"
        });
    }
}
