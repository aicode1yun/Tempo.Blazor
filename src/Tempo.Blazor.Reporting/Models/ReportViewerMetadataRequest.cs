using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Blazor.Reporting.Models;

/// <summary>Request used to resolve report metadata and cascading parameter options.</summary>
public sealed record ReportViewerMetadataRequest
{
    /// <summary>Current parameter values used for cascading option resolution.</summary>
    public IReadOnlyDictionary<string, ReportParameterValue> Parameters { get; init; } =
        new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal);

    /// <summary>Culture used for parameter parsing and option labels.</summary>
    public string CultureName { get; init; } = "en-US";

    /// <summary>Tenant identifier passed to report providers.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>User identifier passed to report providers.</summary>
    public string UserId { get; init; } = string.Empty;
}
