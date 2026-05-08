using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Components.Diagram.Templates;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Localization;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Configuration;

/// <summary>
/// Extension methods for registering Tempo.Blazor services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all required Tempo.Blazor services.
    /// <list type="bullet">
    ///   <item><description><see cref="ITmLocalizer"/>: Singleton (stateless, thread-safe, backed by .resx)</description></item>
    ///   <item><description><see cref="ThemeService"/>: Scoped (per-circuit in Server mode, per-tab in WASM)</description></item>
    ///   <item><description><see cref="ToastService"/>: Scoped (per-circuit in Server mode, per-tab in WASM)</description></item>
    ///   <item><description><see cref="WireframeComponentRegistry"/>: Singleton, pre-loaded with <see cref="BuiltInWireframeComponentProvider"/></description></item>
    /// </list>
    ///
    /// Add this call to your Blazor Program.cs:
    /// <code>
    /// builder.Services.AddTempoBlazor();
    /// </code>
    ///
    /// To add custom wireframe components, call <see cref="AddWireframeComponentProvider{T}"/>
    /// after <c>AddTempoBlazor()</c>:
    /// <code>
    /// builder.Services.AddTempoBlazor();
    /// builder.Services.AddWireframeComponentProvider&lt;MyCustomProvider&gt;();
    /// </code>
    ///
    /// To override the built-in localization with your own strings, register
    /// a custom ITmLocalizer AFTER this call:
    /// <code>
    /// builder.Services.AddTempoBlazor();
    /// builder.Services.AddSingleton&lt;ITmLocalizer, MyCustomTmLocalizer&gt;();
    /// </code>
    ///
    /// To support Czech localization, configure culture in Program.cs:
    /// <code>
    /// var culture = new System.Globalization.CultureInfo("cs");
    /// System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
    /// System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
    /// </code>
    /// </summary>
    public static IServiceCollection AddTempoBlazor(this IServiceCollection services)
    {
        // Localization — Singleton (stateless, thread-safe, backed by .resx)
        services.AddLocalization();
        services.TryAddSingleton<ITmLocalizer, DefaultTmLocalizer>();

        // ThemeService — Scoped (each circuit/tab gets its own theme state)
        services.TryAddScoped<ThemeService>();

        // ToastService — Scoped (each circuit/tab gets its own toast queue)
        services.TryAddScoped<ToastService>();

        // DragDropService — Scoped (carries dragged IDs between sibling components)
        services.TryAddScoped<DragDropService>();

        // ── Notification system ───────────────────────────────────────────────
        services.TryAddSingleton<INotificationBadgeState, NotificationBadgeState>();
        services.TryAddSingleton<INotificationService, NoOpNotificationService>();
        services.TryAddScoped<CommentNotificationOrchestrator>();

        // ── Wireframe editor ──────────────────────────────────────────────────
        // WireframeCommandStack is NOT registered here – it is created by
        // TmWireframeEditor and cascaded to children so that multiple editor
        // instances on the same page each have an isolated undo/redo history.

        // Register built-in component provider so the registry can be populated.
        services.TryAddSingleton<IWireframeComponentProvider, BuiltInWireframeComponentProvider>();

        // Registry is a singleton that collects all registered providers.
        // It is populated lazily on first resolve by the factory below.
        services.TryAddSingleton<WireframeComponentRegistry>(sp =>
        {
            var registry  = new WireframeComponentRegistry();
            var providers = sp.GetServices<IWireframeComponentProvider>();
            foreach (var provider in providers.OrderBy(p => p.Priority))
                registry.RegisterProvider(provider);
            return registry;
        });

        // ── Wireframe schema registry (Abstractions-level, no Blazor dep) ────
        // BuiltInComponentSchemas is the single source of truth for prop metadata.
        services.TryAddSingleton<IWireframeSchemaSource, BuiltInComponentSchemas>();
        services.TryAddSingleton<WireframeSchemaRegistry>(sp =>
            new WireframeSchemaRegistry(sp.GetServices<IWireframeSchemaSource>()));

        // ── Diagram editor ────────────────────────────────────────────────────
        services.TryAddSingleton<IDiagramStencilProvider, BuiltInDiagramStencilProvider>();
        services.TryAddSingleton<DiagramStencilRegistry>(sp =>
        {
            var registry = new DiagramStencilRegistry();
            var providers = sp.GetServices<IDiagramStencilProvider>();
            foreach (var provider in providers.OrderBy(p => p.Priority))
                registry.RegisterProvider(provider);
            return registry;
        });

        services.TryAddSingleton<IDiagramTemplateProvider, BuiltInDiagramTemplateProvider>();
        services.TryAddSingleton<DiagramTemplateRegistry>(sp =>
        {
            var registry = new DiagramTemplateRegistry();
            var providers = sp.GetServices<IDiagramTemplateProvider>();
            foreach (var provider in providers)
                registry.RegisterProvider(provider);
            return registry;
        });

        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IWireframeComponentProvider"/> so its component
    /// definitions appear in <see cref="WireframeComponentRegistry"/> and are shown in
    /// the <c>TmWireframeToolbox</c>.
    ///
    /// Call this <em>after</em> <see cref="AddTempoBlazor"/>:
    /// <code>
    /// builder.Services.AddTempoBlazor();
    /// builder.Services.AddWireframeComponentProvider&lt;MarketingComponentProvider&gt;();
    /// </code>
    ///
    /// When the custom provider registers the same component type
    /// as the built-in provider, the one with the higher
    /// <see cref="IWireframeComponentProvider.Priority"/> wins (built-in uses priority 0;
    /// set a higher value to override).
    /// </summary>
    /// <typeparam name="T">Concrete provider type to register.</typeparam>
    public static IServiceCollection AddWireframeComponentProvider<T>(
        this IServiceCollection services)
        where T : class, IWireframeComponentProvider
    {
        // Register the concrete type so it can also be resolved directly if needed.
        services.TryAddSingleton<T>();
        // Register as IWireframeComponentProvider so GetServices<IWireframeComponentProvider>
        // picks it up inside the WireframeComponentRegistry factory.
        services.AddSingleton<IWireframeComponentProvider>(sp => sp.GetRequiredService<T>());
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IDiagramStencilProvider"/> so its stencil
    /// definitions appear in <see cref="DiagramStencilRegistry"/> and are shown in the
    /// <c>TmDiagramToolbox</c>.
    ///
    /// Call this <em>after</em> <see cref="AddTempoBlazor"/>:
    /// <code>
    /// builder.Services.AddTempoBlazor();
    /// builder.Services.AddDiagramStencilProvider&lt;CustomDiagramStencilProvider&gt;();
    /// </code>
    ///
    /// When the custom provider registers the same stencil id
    /// as the built-in provider, the one with the higher
    /// <see cref="IDiagramStencilProvider.Priority"/> wins (built-in uses priority 0;
    /// set a higher value to override).
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
    /// Replaces the default <see cref="NoOpNotificationService"/> with
    /// <see cref="InMemoryNotificationStore"/> so notifications are kept in memory.
    /// Also replaces <see cref="INotificationBadgeState"/> with the same store
    /// so the badge count is live.
    ///
    /// Use this in demo / test applications where you want to see notifications
    /// without a real backend.
    /// </summary>
    public static IServiceCollection AddInMemoryNotifications(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryNotificationStore>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<InMemoryNotificationStore>());
        services.AddSingleton<INotificationBadgeState>(sp => sp.GetRequiredService<InMemoryNotificationStore>());
        return services;
    }
}
