using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Configuration;

/// <summary>
/// Extension methods for registering Tempo.Blazor wireframe editor services.
/// </summary>
public static class WireframeServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required by the wireframe editor component group.
    /// </summary>
    public static IServiceCollection AddTempoBlazorWireframe(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoBlazor();

        // WireframeCommandStack is created by TmWireframeEditor and cascaded to
        // children so multiple editor instances keep isolated undo/redo history.

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWireframeComponentProvider, BuiltInStencilPackProvider>());
        services.TryAddSingleton<WireframeComponentRegistry>(sp =>
        {
            var registry = new WireframeComponentRegistry();
            var providers = sp.GetServices<IWireframeComponentProvider>();
            foreach (var provider in providers.OrderBy(p => p.Priority))
            {
                registry.RegisterProvider(provider);
            }

            return registry;
        });

        // Headless server-side SVG renderer (no browser/JS) built on the registry above.
        services.TryAddSingleton<IWireframeSvgRenderer, WireframeSvgRenderer>();
        services.TryAddSingleton<NativeRendererRegistry>();
        services.TryAddSingleton<StencilPackCompiler>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWireframeSchemaSource, BuiltInComponentSchemas>());
        services.TryAddSingleton<WireframeSchemaRegistry>(sp =>
            new WireframeSchemaRegistry(sp.GetServices<IWireframeSchemaSource>()));

        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IWireframeComponentProvider"/> so its component
    /// definitions appear in <see cref="WireframeComponentRegistry"/> and are shown in
    /// the <c>TmWireframeToolbox</c>.
    /// </summary>
    /// <typeparam name="T">Concrete provider type to register.</typeparam>
    public static IServiceCollection AddWireframeComponentProvider<T>(
        this IServiceCollection services)
        where T : class, IWireframeComponentProvider
    {
        services.TryAddSingleton<T>();
        services.AddSingleton<IWireframeComponentProvider>(sp => sp.GetRequiredService<T>());
        return services;
    }
}
