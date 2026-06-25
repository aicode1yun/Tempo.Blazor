using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Blazor.Reporting.Models;

/// <summary>Parameter submit event raised by <c>TmReportParameterPanel</c>.</summary>
public sealed record ReportParameterSubmitEventArgs(
    IReadOnlyDictionary<string, ReportParameterValue> Values);
