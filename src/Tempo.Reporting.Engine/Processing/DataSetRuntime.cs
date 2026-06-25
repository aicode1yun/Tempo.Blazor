#pragma warning disable MA0048

using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Reporting.Engine.Processing;

/// <summary>Materialized data set consumed by report processing.</summary>
public sealed record ProcessedDataSet
{
    /// <summary>Creates a processed data set.</summary>
    public ProcessedDataSet(string name, IReadOnlyList<ReportDataColumn> schema, IReadOnlyList<ProcessedDataRow> rows)
    {
        Name = name;
        Schema = schema.ToArray();
        Rows = rows.ToArray();
    }

    /// <summary>Data set name.</summary>
    public string Name { get; }

    /// <summary>Typed schema supplied by the provider.</summary>
    public IReadOnlyList<ReportDataColumn> Schema { get; }

    /// <summary>Materialized rows.</summary>
    public IReadOnlyList<ProcessedDataRow> Rows { get; }

    /// <summary>Creates a forward-only cursor over materialized rows.</summary>
    public ReportDataCursor CreateCursor() => new(this);
}

/// <summary>Materialized provider row.</summary>
public sealed record ProcessedDataRow
{
    /// <summary>Creates a processed row.</summary>
    public ProcessedDataRow(IReadOnlyDictionary<string, object?> values)
    {
        Values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    /// <summary>Field values keyed by column name.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; }

    /// <summary>Gets a field value by name.</summary>
    public object? this[string name] => Values.TryGetValue(name, out var value) ? value : null;
}

/// <summary>Resettable row cursor over a processed data set.</summary>
public sealed class ReportDataCursor
{
    private readonly ProcessedDataSet _dataSet;
    private int _index = -1;

    /// <summary>Creates a cursor.</summary>
    public ReportDataCursor(ProcessedDataSet dataSet)
    {
        _dataSet = dataSet;
    }

    /// <summary>Current row.</summary>
    public ProcessedDataRow Current
        => _index >= 0 && _index < _dataSet.Rows.Count
            ? _dataSet.Rows[_index]
            : throw new InvalidOperationException("The cursor is not positioned on a row.");

    /// <summary>Moves to the next row.</summary>
    public bool MoveNext()
    {
        if (_index + 1 >= _dataSet.Rows.Count)
        {
            return false;
        }

        _index++;
        return true;
    }

    /// <summary>Resets the cursor to the position before the first row.</summary>
    public void Reset()
    {
        _index = -1;
    }
}

/// <summary>Loads provider streams into processing-friendly data sets.</summary>
public static class ReportDataSetRuntime
{
    /// <summary>Materializes a provider result.</summary>
    public static async Task<ProcessedDataSet> LoadAsync(
        string name,
        ReportDataSetResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var rows = new List<ProcessedDataRow>();
        await foreach (var row in result.Rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ProcessedDataRow(row.Values));
        }

        return new ProcessedDataSet(name, result.Schema, rows);
    }
}
