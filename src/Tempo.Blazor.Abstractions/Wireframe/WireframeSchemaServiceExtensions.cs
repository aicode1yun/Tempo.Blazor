using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// DI extension methods for the wireframe schema infrastructure.
/// Use <c>AddWireframeSchemas()</c> in projects that reference only
/// <c>Tempo.Blazor.Abstractions</c> (e.g. an API/MCP server project).
/// The full <c>AddTempoBlazor()</c> call in the main Blazor package
/// already calls this automatically.
/// </summary>
public static class WireframeSchemaServiceExtensions
{
    /// <summary>
    /// Registers <see cref="BuiltInComponentSchemas"/> and <see cref="WireframeSchemaRegistry"/>
    /// plus the built-in UI role vocabulary so both registries can be injected anywhere.
    /// </summary>
    public static IServiceCollection AddWireframeSchemas(this IServiceCollection services)
    {
        services.TryAddSingleton<IWireframeSchemaSource, BuiltInComponentSchemas>();
        services.TryAddSingleton<WireframeSchemaRegistry>(sp =>
            new WireframeSchemaRegistry(sp.GetServices<IWireframeSchemaSource>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiRoleVocabularySource, BuiltInUiRoleVocabularySource>());
        services.TryAddSingleton<UiRoleVocabulary>(sp =>
            new UiRoleVocabulary(sp.GetServices<IUiRoleVocabularySource>()));
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IWireframeSchemaSource"/> so its schemas
    /// are merged into <see cref="WireframeSchemaRegistry"/>.
    /// Higher <see cref="IWireframeSchemaSource.Priority"/> wins on conflict.
    /// </summary>
    public static IServiceCollection AddWireframeSchemaSource<T>(this IServiceCollection services)
        where T : class, IWireframeSchemaSource
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWireframeSchemaSource, T>());
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IUiRoleVocabularySource"/> so its roles
    /// and synonyms are merged into <see cref="UiRoleVocabulary"/>.
    /// Higher <see cref="IUiRoleVocabularySource.Priority"/> wins for duplicate
    /// role display metadata while synonyms are unioned.
    /// </summary>
    public static IServiceCollection AddUiRoleVocabularySource<T>(this IServiceCollection services)
        where T : class, IUiRoleVocabularySource
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiRoleVocabularySource, T>());
        return services;
    }
}
