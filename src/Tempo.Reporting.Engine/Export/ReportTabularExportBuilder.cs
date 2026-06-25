using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Expressions;
using Tempo.Reporting.Engine.Processing;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;

namespace Tempo.Reporting.Engine.Export;

/// <summary>Builds culture-neutral tabular export data from processed report output.</summary>
public static class ReportTabularExportBuilder
{
    /// <summary>Creates export sheets for report tables, falling back to the first processed data set.</summary>
    public static ReportTabularExportDocument Build(
        ReportDefinition definition,
        ReportProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        var sheets = BuildTableSheets(definition, context);
        if (sheets.Count == 0)
        {
            sheets.AddRange(context.DataSets.Values.Select(BuildDataSetSheet));
        }

        return new ReportTabularExportDocument(sheets);
    }

    private static List<ReportTabularExportSheet> BuildTableSheets(
        ReportDefinition definition,
        ReportProcessingContext context)
    {
        var sheets = new List<ReportTabularExportSheet>();
        foreach (var table in EnumerateBands(definition.Bands)
            .SelectMany(band => band.Elements)
            .OfType<ReportTableElement>())
        {
            var dataSet = ResolveDataSet(table.DataSetName, context);
            if (dataSet is null)
            {
                continue;
            }

            var rows = new List<ReportTabularExportRow>();
            if (table.Header is not null)
            {
                rows.Add(BuildTemplateRow(table, table.Header, null, dataSet.Rows, context, isHeader: true));
            }
            else if (table.Columns.Count > 0)
            {
                rows.Add(new ReportTabularExportRow(
                    table.Columns.Select(column => new ReportTabularExportCell(column.Header, ReportTabularExportCellKind.String, bold: true)).ToArray(),
                    isHeader: true));
            }
            else if (dataSet.Schema.Count > 0)
            {
                rows.Add(new ReportTabularExportRow(
                    dataSet.Schema.Select(column => new ReportTabularExportCell(column.Name, ReportTabularExportCellKind.String, bold: true)).ToArray(),
                    isHeader: true));
            }

            foreach (var dataRow in dataSet.Rows)
            {
                if (!IsVisible(table.Detail.VisibleExpression, dataRow, dataSet.Rows, context))
                {
                    continue;
                }

                rows.Add(BuildTemplateRow(table, table.Detail, dataRow, dataSet.Rows, context, isHeader: false));
            }

            if (table.Footer is not null)
            {
                rows.Add(BuildTemplateRow(table, table.Footer, dataSet.Rows.LastOrDefault(), dataSet.Rows, context, isHeader: false));
            }

            sheets.Add(new ReportTabularExportSheet(SheetName(table.Id, dataSet.Name), rows));
        }

        return sheets;
    }

    private static ReportTabularExportRow BuildTemplateRow(
        ReportTableElement table,
        ReportTableRow row,
        ProcessedDataRow? currentRow,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context,
        bool isHeader)
    {
        var cellCount = Math.Max(table.Columns.Count, row.Cells.Count);
        var cells = new List<ReportTabularExportCell>(cellCount);
        for (var index = 0; index < cellCount; index++)
        {
            var cell = index < row.Cells.Count ? row.Cells[index] : new ReportTableCell();
            var value = ResolveCellValue(cell, currentRow, scopeRows, context);
            cells.Add(value with
            {
                Bold = value.Bold || isHeader || cell.TextStyle.Bold,
                BackgroundColor = cell.BackgroundColor ?? row.BackgroundColor ?? value.BackgroundColor,
                NumberFormat = cell.NumberFormat ?? value.NumberFormat,
            });
        }

        return new ReportTabularExportRow(cells, isHeader, row.BackgroundColor);
    }

    private static ReportTabularExportCell ResolveCellValue(
        ReportTableCell cell,
        ProcessedDataRow? currentRow,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context)
    {
        var nestedTextBox = cell.Elements.OfType<ReportTextBoxElement>().FirstOrDefault();
        var expression = nestedTextBox?.Expression ?? cell.Expression;
        if (string.IsNullOrWhiteSpace(expression) &&
            !string.IsNullOrWhiteSpace(cell.Text) &&
            cell.Text.TrimStart().StartsWith("=", StringComparison.Ordinal))
        {
            expression = cell.Text;
        }

        if (!string.IsNullOrWhiteSpace(expression))
        {
            var value = ReportAggregateEngine.EvaluateForRow(expression, currentRow, scopeRows, context, scopeRows);
            return FromExpressionValue(value, cell.NumberFormat);
        }

        return new ReportTabularExportCell(
            nestedTextBox?.Text ?? cell.Text ?? string.Empty,
            ReportTabularExportCellKind.String,
            cell.NumberFormat);
    }

    private static ReportTabularExportCell FromExpressionValue(ExpressionValue value, string? numberFormat)
        => value.Kind switch
        {
            ExpressionValueKind.Null => new ReportTabularExportCell(null, ReportTabularExportCellKind.Empty, numberFormat),
            ExpressionValueKind.Number => new ReportTabularExportCell(value.RawValue, ReportTabularExportCellKind.Number, numberFormat),
            ExpressionValueKind.Boolean => new ReportTabularExportCell(value.RawValue, ReportTabularExportCellKind.Boolean, numberFormat),
            ExpressionValueKind.Date => new ReportTabularExportCell(value.RawValue, ReportTabularExportCellKind.Date, numberFormat),
            ExpressionValueKind.String => new ReportTabularExportCell(value.RawValue, ReportTabularExportCellKind.String, numberFormat),
            _ => new ReportTabularExportCell(value.AsString(), ReportTabularExportCellKind.String, numberFormat),
        };

    private static ReportTabularExportSheet BuildDataSetSheet(ProcessedDataSet dataSet)
    {
        var columns = dataSet.Schema.Count > 0
            ? dataSet.Schema.Select(column => column.Name).ToArray()
            : dataSet.Rows.SelectMany(row => row.Values.Keys).Distinct(StringComparer.Ordinal).ToArray();
        var rows = new List<ReportTabularExportRow>
        {
            new(columns.Select(column => new ReportTabularExportCell(column, ReportTabularExportCellKind.String, bold: true)).ToArray(), isHeader: true),
        };
        foreach (var row in dataSet.Rows)
        {
            rows.Add(new ReportTabularExportRow(columns
                .Select(column => FromRawValue(row[column], ResolveFieldType(dataSet, column)))
                .ToArray()));
        }

        return new ReportTabularExportSheet(SheetName(dataSet.Name, "Data"), rows);
    }

    private static ReportTabularExportCell FromRawValue(object? value, DataFieldType? fieldType)
    {
        if (value is null)
        {
            return new ReportTabularExportCell(null, ReportTabularExportCellKind.Empty);
        }

        var kind = fieldType switch
        {
            DataFieldType.Number => ReportTabularExportCellKind.Number,
            DataFieldType.Date => ReportTabularExportCellKind.Date,
            DataFieldType.Boolean => ReportTabularExportCellKind.Boolean,
            DataFieldType.String => ReportTabularExportCellKind.String,
            _ => InferKind(value),
        };
        return new ReportTabularExportCell(value, kind);
    }

    private static ReportTabularExportCellKind InferKind(object value)
        => value switch
        {
            decimal or double or float or int or long or short or byte => ReportTabularExportCellKind.Number,
            DateTime or DateTimeOffset or DateOnly => ReportTabularExportCellKind.Date,
            bool => ReportTabularExportCellKind.Boolean,
            string => ReportTabularExportCellKind.String,
            _ => ReportTabularExportCellKind.Object,
        };

    private static bool IsVisible(
        string? expression,
        ProcessedDataRow row,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context)
        => string.IsNullOrWhiteSpace(expression) ||
            ReportAggregateEngine.EvaluateForRow(expression, row, scopeRows, context, scopeRows).AsBoolean();

    private static IEnumerable<ReportBand> EnumerateBands(ReportBandCollection bands)
    {
        if (bands.ReportHeader is not null)
        {
            yield return bands.ReportHeader;
        }

        if (bands.PageHeader is not null)
        {
            yield return bands.PageHeader;
        }

        foreach (var group in bands.Groups)
        {
            if (group.GroupHeader is not null)
            {
                yield return group.GroupHeader;
            }

            if (group.GroupFooter is not null)
            {
                yield return group.GroupFooter;
            }
        }

        if (bands.Detail is not null)
        {
            yield return bands.Detail;
        }

        if (bands.ReportFooter is not null)
        {
            yield return bands.ReportFooter;
        }

        if (bands.PageFooter is not null)
        {
            yield return bands.PageFooter;
        }
    }

    private static ProcessedDataSet? ResolveDataSet(string? dataSetName, ReportProcessingContext context)
    {
        if (!string.IsNullOrWhiteSpace(dataSetName) &&
            context.DataSets.TryGetValue(dataSetName, out var named))
        {
            return named;
        }

        return context.DataSets.Values.FirstOrDefault();
    }

    private static DataFieldType? ResolveFieldType(ProcessedDataSet dataSet, string column)
        => dataSet.Schema.FirstOrDefault(candidate => string.Equals(candidate.Name, column, StringComparison.Ordinal))?.DataType;

    private static string SheetName(string preferred, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
        const string invalid = ":\\/?*[]";
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Data" : sanitized[..Math.Min(31, sanitized.Length)];
    }
}
