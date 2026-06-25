#pragma warning disable MA0016, MA0048

namespace Tempo.Reporting.Abstractions.Data;

/// <summary>Report data field type.</summary>
public enum ReportDataFieldType
{
    /// <summary>String field.</summary>
    String,

    /// <summary>Numeric field.</summary>
    Number,

    /// <summary>Date or date-time field.</summary>
    Date,

    /// <summary>Boolean field.</summary>
    Boolean,

    /// <summary>Opaque object field.</summary>
    Object,
}

/// <summary>Column in a provider result schema.</summary>
public sealed record ReportDataColumn(string Name, ReportDataFieldType DataType);

/// <summary>Single streamed data row.</summary>
public sealed record ReportDataRow
{
    /// <summary>Creates a data row from values.</summary>
    public ReportDataRow(IReadOnlyDictionary<string, object?> values)
    {
        Values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    /// <summary>Row values keyed by column name.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; }
}

/// <summary>Data provider result with schema and streaming-friendly rows.</summary>
public sealed record ReportDataSetResult(
    IReadOnlyList<ReportDataColumn> Schema,
    IAsyncEnumerable<ReportDataRow> Rows);

/// <summary>Provider query descriptor.</summary>
public sealed record ReportDataQuery
{
    /// <summary>Named data source reference.</summary>
    public string? SourceName { get; init; }

    /// <summary>Provider-specific query text or URL template.</summary>
    public string? Text { get; init; }

    /// <summary>Optional result selector, such as JSONPath or JSON Pointer.</summary>
    public string? Selector { get; init; }

    /// <summary>Maximum number of rows to return.</summary>
    public int? MaxRows { get; init; }

    /// <summary>Provider timeout.</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>Report parameter value passed to data providers.</summary>
public sealed record ReportParameterValue
{
    private ReportParameterValue(IReadOnlyList<object?> values)
    {
        Values = values.ToArray();
    }

    /// <summary>Parameter values. Single-value parameters contain one item.</summary>
    public IReadOnlyList<object?> Values { get; }

    /// <summary>First value or null.</summary>
    public object? ScalarValue => Values.Count == 0 ? null : Values[0];

    /// <summary>Creates a scalar parameter value.</summary>
    public static ReportParameterValue Scalar(object? value) => new([value]);

    /// <summary>Creates a multi-value parameter value.</summary>
    public static ReportParameterValue Multiple(IEnumerable<object?> values) => new(values.ToArray());
}

/// <summary>Report data provider contract.</summary>
public interface IReportDataProvider
{
    /// <summary>Gets data for a data set as schema plus streamed rows.</summary>
    Task<ReportDataSetResult> GetDataAsync(
        string dataSetName,
        ReportDataQuery query,
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        ReportExecutionContext context);
}

/// <summary>Exception thrown by report data providers.</summary>
public sealed class ReportDataProviderException : Exception
{
    /// <summary>Creates a provider exception.</summary>
    public ReportDataProviderException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Stable provider error code.</summary>
    public string Code { get; }
}

#pragma warning restore MA0016, MA0048
