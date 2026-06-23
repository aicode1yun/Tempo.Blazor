using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tempo.Blazor.NotionEditor.Helpers;

namespace Tempo.Blazor.Configuration;

/// <summary>
/// Extension methods for registering Tempo.Blazor Notion editor services.
/// </summary>
public static class NotionEditorServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required by the Notion-style editor component group.
    /// </summary>
    public static IServiceCollection AddTempoBlazorNotionEditor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoBlazor();
        services.TryAddScoped<CommentNotificationOrchestrator>();
        return services;
    }
}
