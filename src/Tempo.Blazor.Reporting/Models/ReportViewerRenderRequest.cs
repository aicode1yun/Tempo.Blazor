using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Blazor.Reporting.Models;

/// <summary>Render request issued by <c>TmReportViewer</c>.</summary>
public sealed record ReportViewerRenderRequest
{
    /// <summary>Resolved or user-supplied parameter values.</summary>
    public IReadOnlyDictionary<string, ReportParameterValue> Parameters { get; init; } =
        new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal);

    /// <summary>Stateless interaction token carrying toggle and drill-down state.</summary>
    public string? InteractionToken { get; init; }

    /// <summary>Culture used for processing.</summary>
    public string CultureName { get; init; } = "en-US";

    /// <summary>Tenant identifier passed to report providers.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>User identifier passed to report providers.</summary>
    public string UserId { get; init; } = string.Empty;
}
