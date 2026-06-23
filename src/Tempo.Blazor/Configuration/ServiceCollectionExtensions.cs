using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Localization;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Configuration;

/// <summary>
/// Extension methods for registering Tempo.Blazor services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core Tempo.Blazor services shared by all component groups.
    /// <list type="bullet">
    ///   <item><description><see cref="ITmLocalizer"/>: Singleton (stateless, thread-safe, backed by .resx)</description></item>
    ///   <item><description><see cref="ThemeService"/>: Scoped (per-circuit in Server mode, per-tab in WASM)</description></item>
    ///   <item><description><see cref="ToastService"/>: Scoped (per-circuit in Server mode, per-tab in WASM)</description></item>
    ///   <item><description><see cref="DragDropService"/>: Scoped drag/drop state shared by core components</description></item>
    /// </list>
    ///
    /// Add this call to your Blazor Program.cs:
    /// <code>
    /// builder.Services.AddTempoBlazor();
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
        ArgumentNullException.ThrowIfNull(services);

        // Localization: singleton, stateless, backed by .resx.
        services.AddLocalization();
        services.TryAddSingleton<ITmLocalizer, DefaultTmLocalizer>();

        // ThemeService: scoped so each circuit/tab gets its own theme state.
        services.TryAddScoped<ThemeService>();

        // ToastService: scoped so each circuit/tab gets its own toast queue.
        services.TryAddScoped<ToastService>();

        // DragDropService carries dragged IDs between sibling components.
        services.TryAddScoped<DragDropService>();

        // Notification system.
        services.TryAddSingleton<ITmNotificationService, NoOpNotificationService>();

        return services;
    }

    /// <summary>
    /// Registers services required by the signing component group.
    /// </summary>
    public static IServiceCollection AddTempoBlazorSigning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoBlazor();
        return services;
    }

    /// <summary>
    /// Replaces the default <see cref="NoOpNotificationService"/> with
    /// <see cref="InMemoryNotificationStore"/> so notifications are kept in memory.
    ///
    /// Use this in demo / test applications where you want to see notifications
    /// without a real backend.
    /// </summary>
    public static IServiceCollection AddInMemoryNotifications(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InMemoryNotificationStore>();
        services.AddSingleton<ITmNotificationService>(sp => sp.GetRequiredService<InMemoryNotificationStore>());
        return services;
    }
}
