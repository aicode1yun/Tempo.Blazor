using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Mcp.Wireframe;

namespace Tempo.Blazor.Mcp;

/// <summary>
/// Registration helpers for the wireframe MCP tools.
/// </summary>
/// <remarks>
/// The host application maps the tools onto its own MCP server, e.g.:
/// <code>
/// builder.Services.AddTempoWireframeMcpTools();
/// builder.Services.AddMcpServer()
///     .WithHttpTransport()
///     .WithToolsFromAssembly(typeof(TempoWireframeMcp).Assembly);
/// // plus the host's ITempoDocumentLibraryProvider and IWireframeDocumentProvider.
/// </code>
/// Prefer <c>WithToolsFromAssembly</c> over <c>WithTools(ToolTypes)</c>: the assembly scan
/// advertises the <c>tools</c> capability in the MCP handshake, which the type list does not.
/// </remarks>
public static class TempoWireframeMcp
{
    /// <summary>
    /// The wireframe tool types, exposed for hosts that register tools by type. Prefer
    /// <c>WithToolsFromAssembly(typeof(TempoWireframeMcp).Assembly)</c>, which also advertises
    /// the <c>tools</c> capability in the handshake.
    /// </summary>
    public static IReadOnlyList<Type> ToolTypes { get; } =
    [
        typeof(WireframeComponentCatalogTools),
        typeof(WireframeDocumentTools),
        typeof(WireframeValidationTools),
        typeof(WireframeOperationTools),
        typeof(WireframeBriefTools)
    ];

    /// <summary>
    /// Registers the dependencies the wireframe MCP tools resolve from DI (the component schema
    /// registry). The host must additionally supply an <c>ITempoDocumentLibraryProvider</c> and an
    /// <c>IWireframeDocumentProvider</c>, and register the tools with its MCP server via
    /// <see cref="ToolTypes"/>.
    /// </summary>
    public static IServiceCollection AddTempoWireframeMcpTools(this IServiceCollection services)
    {
        services.AddWireframeSchemas();
        return services;
    }
}
