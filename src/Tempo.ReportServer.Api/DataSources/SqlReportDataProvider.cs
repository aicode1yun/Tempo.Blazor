using System.Data;
using System.Data.Common;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;

namespace Tempo.ReportServer.Api.DataSources;

/// <summary>SQL implementation of <see cref="IReportDataProvider"/> using provider parameters only.</summary>
public sealed class SqlReportDataProvider : IReportDataProvider
{
    private readonly IReportDataSourceRegistry _registry;

    /// <summary>Creates a SQL provider.</summary>
    public SqlReportDataProvider(IReportDataSourceRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public async Task<ReportDataSetResult> GetDataAsync(
        string dataSetName,
        ReportDataQuery query,
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        ReportExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (query.Text?.Contains('{', StringComparison.Ordinal) == true ||
            query.Text?.Contains('}', StringComparison.Ordinal) == true)
        {
            throw new ReportDataProviderException(
                "Sql.QueryInterpolationRejected",
                "SQL report queries must use provider parameters, not brace interpolation.");
        }

        var source = ResolveSource(query, context);
        await using var connection = source.SqlOptions!.ConnectionFactory();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(context.CancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = query.Text ?? string.Empty;
        if (query.Timeout is { TotalSeconds: > 0 })
        {
            command.CommandTimeout = Math.Max(1, (int)Math.Ceiling(query.Timeout.Value.TotalSeconds));
        }

        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = "@" + parameter.Key;
            dbParameter.Value = parameter.Value.ScalarValue ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }

        await using var reader = await command.ExecuteReaderAsync(context.CancellationToken);
        var schema = BuildSchema(reader);
        var rows = new List<ReportDataRow>();
        while (await reader.ReadAsync(context.CancellationToken))
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = await reader.IsDBNullAsync(i, context.CancellationToken)
                    ? null
                    : reader.GetValue(i);
                values[reader.GetName(i)] = value;
            }

            rows.Add(new ReportDataRow(values));
            if (query.MaxRows is not null && rows.Count >= query.MaxRows.Value)
            {
                break;
            }
        }

        return new ReportDataSetResult(schema, StreamRows(rows, context.CancellationToken));
    }

    private NamedReportDataSource ResolveSource(ReportDataQuery query, ReportExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(query.SourceName))
        {
            throw new ReportDataProviderException("Sql.SourceRequired", "SQL query requires a named data source.");
        }

        var source = _registry.Resolve(query.SourceName, context);
        if (source?.SqlOptions is null)
        {
            throw new ReportDataProviderException(
                "Sql.SourceNotFound",
                $"SQL data source '{query.SourceName}' was not found for tenant '{context.TenantId}'.");
        }

        return source;
    }

    private static IReadOnlyList<ReportDataColumn> BuildSchema(DbDataReader reader)
    {
        var columns = new ReportDataColumn[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns[i] = new ReportDataColumn(reader.GetName(i), InferType(reader.GetFieldType(i)));
        }

        return columns;
    }

    private static ReportDataFieldType InferType(Type type)
    {
        if (type == typeof(string))
        {
            return ReportDataFieldType.String;
        }

        if (type == typeof(bool))
        {
            return ReportDataFieldType.Boolean;
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return ReportDataFieldType.Date;
        }

        if (type == typeof(byte) ||
            type == typeof(short) ||
            type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(float) ||
            type == typeof(double) ||
            type == typeof(decimal))
        {
            return ReportDataFieldType.Number;
        }

        return ReportDataFieldType.Object;
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
}
