using System.Globalization;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Engine.Expressions;

namespace Tempo.Reporting.Engine.Processing;

/// <summary>Runtime processing context shared by data, grouping, aggregate and band phases.</summary>
public sealed record ReportProcessingContext
{
    /// <summary>Creates a processing context from execution metadata and resolved parameters.</summary>
    public ReportProcessingContext(
        ReportExecutionContext executionContext,
        IReadOnlyDictionary<string, ReportParameterValue>? parameters = null,
        IReadOnlyDictionary<string, ProcessedDataSet>? dataSets = null,
        DateTimeOffset? executionTime = null)
    {
        ExecutionContext = executionContext;
        Culture = CreateCulture(executionContext.CultureName);
        Parameters = parameters is null
            ? new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
            : new Dictionary<string, ReportParameterValue>(parameters, StringComparer.Ordinal);
        DataSets = dataSets is null
            ? new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal)
            : new Dictionary<string, ProcessedDataSet>(dataSets, StringComparer.Ordinal);
        Globals = new ExpressionGlobals
        {
            ExecutionTime = executionTime ?? DateTimeOffset.UtcNow,
            UserName = executionContext.UserId,
            TenantName = executionContext.TenantId,
        };
    }

    /// <summary>Tenant-scoped execution context.</summary>
    public ReportExecutionContext ExecutionContext { get; }

    /// <summary>Culture used for collation, formatting and type conversion.</summary>
    public CultureInfo Culture { get; }

    /// <summary>Resolved report parameters.</summary>
    public IReadOnlyDictionary<string, ReportParameterValue> Parameters { get; }

    /// <summary>Materialized data sets available to processing expressions.</summary>
    public IReadOnlyDictionary<string, ProcessedDataSet> DataSets { get; }

    /// <summary>Global expression values.</summary>
    public ExpressionGlobals Globals { get; }

    /// <summary>Creates an expression context for a row.</summary>
    public ExpressionContext CreateExpressionContext(ProcessedDataRow? row = null)
        => new(row?.Values ?? new Dictionary<string, object?>(StringComparer.Ordinal), ParameterScalars(), Globals);

    /// <summary>Creates a copy with a different parameter dictionary.</summary>
    public ReportProcessingContext WithParameters(IReadOnlyDictionary<string, ReportParameterValue> parameters)
        => new(ExecutionContext, parameters, DataSets, Globals.ExecutionTime);

    /// <summary>Creates a copy with a different data set dictionary.</summary>
    public ReportProcessingContext WithDataSets(IReadOnlyDictionary<string, ProcessedDataSet> dataSets)
        => new(ExecutionContext, Parameters, dataSets, Globals.ExecutionTime);

    private IReadOnlyDictionary<string, object?> ParameterScalars()
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in Parameters)
        {
            values[pair.Key] = pair.Value.Values.Count > 1 ? pair.Value.Values.ToArray() : pair.Value.ScalarValue;
        }

        return values;
    }

    private static CultureInfo CreateCulture(string cultureName)
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(cultureName) ? "en-US" : cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
    }
}
