using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Blazor.Mcp.Reporting;

/// <summary>Fallback data provider for MCP preview smoke flows when the host has no provider.</summary>
public sealed class ReportingMcpFallbackDataProvider : IReportDataProvider
{
    /// <inheritdoc />
    public Task<ReportDataSetResult> GetDataAsync(
        string dataSetName,
        ReportDataQuery query,
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ReportDataSetResult(
            [],
            StreamRows(context.CancellationToken)));
    }

    private static async IAsyncEnumerable<ReportDataRow> StreamRows(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        yield return new ReportDataRow(new Dictionary<string, object?>(StringComparer.Ordinal));
    }
}
