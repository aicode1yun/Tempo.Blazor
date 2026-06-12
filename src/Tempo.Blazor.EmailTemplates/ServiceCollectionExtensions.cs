using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tempo.Blazor.EmailTemplates.Abstractions;
using Tempo.Blazor.EmailTemplates.Abstractions.Templating;
using Tempo.Blazor.EmailTemplates.Localization;

namespace Tempo.Blazor.EmailTemplates;

/// <summary>DI registration for the Tempo.Blazor email template editor UI.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the email template editor: the engine (model, generation, rendering, validation from
    /// <c>Tempo.Blazor.EmailTemplates.Abstractions</c>), localization and the UI localizer. Hosts may
    /// override <see cref="ITmEmailLocalizer"/> after this call.
    /// </summary>
    public static IServiceCollection AddTempoEmailTemplates(
        this IServiceCollection services, Action<TemplateSecurityOptions>? configure = null)
    {
        services.AddLocalization();
        services.TryAddSingleton<ITmEmailLocalizer, DefaultTmEmailLocalizer>();
        services.AddTempoEmailTemplateEngine(configure);
        return services;
    }
}
