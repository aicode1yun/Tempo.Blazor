using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tempo.Reporting.Abstractions.Resources;

namespace Tempo.Reporting.Abstractions.Tests.Validation;

internal static class ReportingValidationTestLocalizer
{
    public static IStringLocalizer<ReportingValidationResources> Create()
    {
        var factory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()),
            NullLoggerFactory.Instance);

        return new StringLocalizer<ReportingValidationResources>(factory);
    }

    public static TResult InCulture<TResult>(string cultureName, Func<TResult> action)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
