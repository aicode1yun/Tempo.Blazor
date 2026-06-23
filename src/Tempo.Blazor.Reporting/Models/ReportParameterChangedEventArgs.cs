using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Blazor.Reporting.Models;

/// <summary>Parameter change raised by <c>TmReportParameterPanel</c>.</summary>
public sealed record ReportParameterChangedEventArgs(
    ReportParameterDefinition Parameter,
    IReadOnlyDictionary<string, ReportParameterValue> Values);
