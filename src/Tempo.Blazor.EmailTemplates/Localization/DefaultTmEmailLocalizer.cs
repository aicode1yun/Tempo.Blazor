using Microsoft.Extensions.Localization;
using Tempo.Blazor.EmailTemplates.Resources;

namespace Tempo.Blazor.EmailTemplates.Localization;

/// <summary>Default <see cref="ITmEmailLocalizer"/> backed by the embedded <c>TmEmailResources</c> resx files.</summary>
internal sealed class DefaultTmEmailLocalizer : ITmEmailLocalizer
{
    private readonly IStringLocalizer<TmEmailResources> _localizer;

    public DefaultTmEmailLocalizer(IStringLocalizer<TmEmailResources> localizer) => _localizer = localizer;

    public string this[string key] => _localizer[key].Value;

    public string this[string key, params object[] arguments] => _localizer[key, arguments].Value;
}
