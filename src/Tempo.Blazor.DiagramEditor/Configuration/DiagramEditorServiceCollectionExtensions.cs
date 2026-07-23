using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Components.Diagram.Templates;

namespace Tempo.Blazor.Configuration;

/// <summary>
/// Extension methods for registering Tempo.Blazor diagram editor services.
/// </summary>
public static class DiagramEditorServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required by the diagram editor component group.
    /// </summary>
    public static IServiceCollection AddTempoBlazorDiagramEditor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoBlazor();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiagramStencilProvider, BuiltInDiagramStencilProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiagramStencilProvider, Uml25DiagramStencilProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiagramStencilProvider, Bpmn2DiagramStencilProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiagramStencilProvider, Archimate3DiagramStencilProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiagramStencilProvider, ExtendedDiagramStencilProvider>());
        services.TryAddSingleton<DiagramStencilRegistry>(sp =>
        {
            var registry = new DiagramStencilRegistry();
            var providers = sp.GetServices<IDiagramStencilProvider>();
            foreach (var provider in providers.OrderBy(p => p.Priority))
            {
                registry.RegisterProvider(provider);
            }

            return registry;
        });

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiagramTemplateProvider, BuiltInDiagramTemplateProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiagramTemplateProvider, ExtendedDiagramTemplateProvider>());
        services.TryAddSingleton<DiagramTemplateRegistry>(sp =>
        {
            var registry = new DiagramTemplateRegistry();
            var providers = sp.GetServices<IDiagramTemplateProvider>();
            foreach (var provider in providers)
            {
                registry.RegisterProvider(provider);
            }

            return registry;
        });

        services.TryAddSingleton<IDiagramSvgRenderer, DiagramSvgRenderer>();

        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IDiagramStencilProvider"/> so its stencil
    /// definitions appear in <see cref="DiagramStencilRegistry"/> and are shown in the
    /// <c>TmDiagramToolbox</c>.
    /// </summary>
    /// <typeparam name="T">Concrete provider type to register.</typeparam>
    public static IServiceCollection AddDiagramStencilProvider<T>(
        this IServiceCollection services)
        where T : class, IDiagramStencilProvider
    {
        services.TryAddSingleton<T>();
        services.AddSingleton<IDiagramStencilProvider>(sp => sp.GetRequiredService<T>());
        return services;
    }

    /// <summary>
    /// Registers a JSON-backed diagram stencil provider with optional lazy-loaded libraries.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="sources">JSON stencil library sources.</param>
    /// <param name="priority">Provider priority. Higher values override lower-priority stencil ids.</param>
    public static IServiceCollection AddJsonDiagramStencilProvider(
        this IServiceCollection services,
        IEnumerable<JsonDiagramStencilLibrarySource> sources,
        int priority = 50)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(sources);

        services.AddSingleton<IDiagramStencilProvider>(new JsonDiagramStencilProvider(sources, priority));
        return services;
    }
}
