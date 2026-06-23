namespace Tempo.Reporting.Abstractions.Data;

/// <summary>Embedded/static report data provider for tests and host-provided inline data.</summary>
public sealed class StaticDataProvider : IReportDataProvider
{
    private readonly Dictionary<string, List<ReportDataRow>> _dataSets;

    private StaticDataProvider(Dictionary<string, List<ReportDataRow>> dataSets)
    {
        _dataSets = dataSets;
    }

    /// <summary>Creates a provider with one data set from dictionaries.</summary>
    public static StaticDataProvider FromRows(
        string dataSetName,
        IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        var dataSets = new Dictionary<string, List<ReportDataRow>>(StringComparer.Ordinal)
        {
            [dataSetName] = rows.Select(row => new ReportDataRow(row)).ToList(),
        };
        return new StaticDataProvider(dataSets);
    }

    /// <inheritdoc />
    public Task<ReportDataSetResult> GetDataAsync(
        string dataSetName,
        ReportDataQuery query,
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (!_dataSets.TryGetValue(dataSetName, out var rows))
        {
            throw new ReportDataProviderException(
                "StaticData.DataSetNotFound",
                $"Static data set '{dataSetName}' was not found.");
        }

        var limitedRows = query.MaxRows is > -1
            ? rows.Take(query.MaxRows.Value).ToList()
            : rows.ToList();
        var schema = InferSchema(limitedRows.Count == 0 ? rows : limitedRows);
        return Task.FromResult(new ReportDataSetResult(schema, StreamRows(limitedRows, context.CancellationToken)));
    }

    private static IReadOnlyList<ReportDataColumn> InferSchema(IReadOnlyList<ReportDataRow> rows)
    {
        var first = rows.FirstOrDefault();
        if (first is null)
        {
            return [];
        }

        return first.Values.Select(pair => new ReportDataColumn(pair.Key, InferType(pair.Value))).ToArray();
    }

    private static async IAsyncEnumerable<ReportDataRow> StreamRows(
        IEnumerable<ReportDataRow> rows,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return row;
        }
    }

    private static ReportDataFieldType InferType(object? value)
    {
        if (value is null)
        {
            return ReportDataFieldType.Object;
        }

        return value switch
        {
            string => ReportDataFieldType.String,
            bool => ReportDataFieldType.Boolean,
            DateTime or DateTimeOffset => ReportDataFieldType.Date,
            byte or short or int or long or float or double or decimal => ReportDataFieldType.Number,
            _ => ReportDataFieldType.Object,
        };
    }
}
