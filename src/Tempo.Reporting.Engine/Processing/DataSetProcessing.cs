#pragma warning disable MA0048

using Tempo.Reporting.Engine.Expressions;

namespace Tempo.Reporting.Engine.Processing;

/// <summary>Sort direction for report processing.</summary>
public enum ReportSortDirection
{
    /// <summary>Ascending order.</summary>
    Ascending,

    /// <summary>Descending order.</summary>
    Descending,
}

/// <summary>Null ordering for report sorting.</summary>
public enum ReportNullSortOrder
{
    /// <summary>Null values sort before non-null values.</summary>
    First,

    /// <summary>Null values sort after non-null values.</summary>
    Last,
}

/// <summary>Expression-backed sort definition.</summary>
public sealed record ReportSortDefinition(
    string Expression,
    ReportSortDirection Direction = ReportSortDirection.Ascending,
    ReportNullSortOrder Nulls = ReportNullSortOrder.Last);

/// <summary>Filtering and sorting helpers for processed data sets.</summary>
public static class ReportDataSetProcessor
{
    /// <summary>Applies an optional filter and expression-backed sort list.</summary>
    public static ProcessedDataSet FilterAndSort(
        ProcessedDataSet dataSet,
        string? filterExpression,
        IReadOnlyList<ReportSortDefinition> sorts,
        ReportProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentNullException.ThrowIfNull(context);

        var filterNode = string.IsNullOrWhiteSpace(filterExpression)
            ? null
            : ExpressionParser.Parse(filterExpression);
        var sortNodes = sorts
            .Select(sort => ExpressionParser.Parse(sort.Expression))
            .ToArray();

        var filtered = filterNode is null
            ? dataSet.Rows
            : dataSet.Rows
                .Where(row => ExpressionEvaluator.Evaluate(filterNode, context.CreateExpressionContext(row)).AsBoolean())
                .ToArray();

        var rows = sorts.Count == 0
            ? filtered.ToArray()
            : filtered
                .Select((row, ordinal) => new SortRow(row, ordinal, EvaluateSortKeys(row, sortNodes, context)))
                .OrderBy(row => row, new SortRowComparer(sorts, context))
                .Select(row => row.Row)
                .ToArray();

        return new ProcessedDataSet(dataSet.Name, dataSet.Schema, rows);
    }

    private static ExpressionValue[] EvaluateSortKeys(
        ProcessedDataRow row,
        IReadOnlyList<ExpressionNode> sortNodes,
        ReportProcessingContext context)
        => sortNodes
            .Select(sort => ReportAggregateEngine.Evaluate(sort, [row], context))
            .ToArray();

    private sealed record SortRow(ProcessedDataRow Row, int Ordinal, IReadOnlyList<ExpressionValue> Keys);

    private sealed class SortRowComparer : IComparer<SortRow>
    {
        private readonly IReadOnlyList<ReportSortDefinition> _sorts;
        private readonly ReportProcessingContext _context;

        public SortRowComparer(IReadOnlyList<ReportSortDefinition> sorts, ReportProcessingContext context)
        {
            _sorts = sorts;
            _context = context;
        }

        public int Compare(SortRow? x, SortRow? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            for (var i = 0; i < _sorts.Count; i++)
            {
                var comparison = ReportValueComparer.Compare(x.Keys[i], y.Keys[i], _context, _sorts[i].Nulls);
                if (_sorts[i].Direction == ReportSortDirection.Descending &&
                    x.Keys[i].Kind != ExpressionValueKind.Null &&
                    y.Keys[i].Kind != ExpressionValueKind.Null)
                {
                    comparison *= -1;
                }

                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return x.Ordinal.CompareTo(y.Ordinal);
        }
    }
}

internal static class ReportValueComparer
{
    public static int Compare(
        ExpressionValue left,
        ExpressionValue right,
        ReportProcessingContext context,
        ReportNullSortOrder nullOrder = ReportNullSortOrder.Last)
    {
        if (left.Kind == ExpressionValueKind.Null || right.Kind == ExpressionValueKind.Null)
        {
            if (left.Kind == right.Kind)
            {
                return 0;
            }

            var nullComparison = left.Kind == ExpressionValueKind.Null ? -1 : 1;
            return nullOrder == ReportNullSortOrder.First ? nullComparison : -nullComparison;
        }

        if (left.Kind == ExpressionValueKind.String || right.Kind == ExpressionValueKind.String)
        {
            return context.Culture.CompareInfo.Compare(left.AsString(), right.AsString());
        }

        if (left.Kind == ExpressionValueKind.Date || right.Kind == ExpressionValueKind.Date)
        {
            return left.AsDate().CompareTo(right.AsDate());
        }

        if (left.Kind == ExpressionValueKind.Boolean || right.Kind == ExpressionValueKind.Boolean)
        {
            return left.AsBoolean().CompareTo(right.AsBoolean());
        }

        return left.AsNumber().CompareTo(right.AsNumber());
    }

    public static int CompareObjects(
        object? left,
        object? right,
        ReportProcessingContext context,
        ReportNullSortOrder nullOrder = ReportNullSortOrder.Last)
        => Compare(ExpressionValue.FromObject(left), ExpressionValue.FromObject(right), context, nullOrder);
}
