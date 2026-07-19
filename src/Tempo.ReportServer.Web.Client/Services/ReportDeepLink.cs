using Microsoft.AspNetCore.WebUtilities;
using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.ReportServer.Web.Services;

/// <summary>Builds report viewer deep links (<c>/reports/{path}?param=value</c>) from resolved actions.</summary>
public static class ReportDeepLink
{
    /// <summary>
    /// Builds the deep-link URL for a resolved drill-through target, escaping each path segment and
    /// appending the mapped parameter values as query string entries. Returns <see langword="null"/> when
    /// the resolution has no usable target.
    /// </summary>
    public static string? Build(ReportDrillThroughResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (!resolution.HasTarget)
        {
            return null;
        }

        var target = resolution.TargetReportPath ?? resolution.TargetReportId!;
        var encodedPath = string.Join('/', target.Split('/').Select(Uri.EscapeDataString));
        var url = $"/reports/{encodedPath}";
        foreach (var parameter in resolution.Parameters)
        {
            if (parameter.Value is not null)
            {
                url = QueryHelpers.AddQueryString(url, parameter.Key, parameter.Value);
            }
        }

        return url;
    }
}
