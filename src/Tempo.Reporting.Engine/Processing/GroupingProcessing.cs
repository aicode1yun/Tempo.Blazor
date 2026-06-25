#pragma warning disable MA0048

using Tempo.Reporting.Engine.Expressions;

namespace Tempo.Reporting.Engine.Processing;

/// <summary>Group level definition used by the processing engine.</summary>
public sealed record ReportGroupLevel(
    string Name,
    string Expression,
    IReadOnlyList<ReportSortDefinition>? Sorts = null);

/// <summary>Processed group node containing rows and child groups.</summary>
public sealed record ProcessedGroup
{
    /// <summary>Creates a processed group.</summary>
    public ProcessedGroup(
        string name,
        int level,
        object? key,
        IReadOnlyList<ProcessedDataRow> rows,
        IReadOnlyList<ProcessedGroup> children)
    {
        Name = name;
        Level = level;
        Key = key;
        Rows = rows.ToArray();
        Children = children.ToArray();
    }

    /// <summary>Group definition name.</summary>
    public string Name { get; }

    /// <summary>Zero-based group level.</summary>
    public int Level { get; }

    /// <summary>Evaluated group key.</summary>
    public object? Key { get; }

    /// <summary>Rows included in this group.</summary>
    public IReadOnlyList<ProcessedDataRow> Rows { get; }

    /// <summary>Nested groups.</summary>
    public IReadOnlyList<ProcessedGroup> Children { get; }

    /// <summary>Evaluates an aggregate expression in this group scope.</summary>
    public ExpressionValue Aggregate(string expression, ReportProcessingContext context)
        => ReportAggregateEngine.Evaluate(expression, Rows, context);
}

/// <summary>Builds multi-level group trees from processed rows.</summary>
public static class ReportGroupingEngine
{
    /// <summary>Groups a data set by one or more expression-backed levels.</summary>
    public static IReadOnlyList<ProcessedGroup> Group(
        ProcessedDataSet dataSet,
        IReadOnlyList<ReportGroupLevel> levels,
        ReportProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(context);

        var parsedLevels = levels
            .Select(level => new ParsedGroupLevel(
                level.Name,
                ExpressionParser.Parse(level.Expression),
                level.Sorts ?? [],
                (level.Sorts ?? []).Select(sort => ExpressionParser.Parse(sort.Expression)).ToArray()))
            .ToArray();

        return GroupRows(dataSet.Rows, parsedLevels, 0, context);
    }

    private static IReadOnlyList<ProcessedGroup> GroupRows(
        IReadOnlyList<ProcessedDataRow> rows,
        IReadOnlyList<ParsedGroupLevel> levels,
        int levelIndex,
        ReportProcessingContext context)
    {
        if (levelIndex >= levels.Count)
        {
            return [];
        }

        var level = levels[levelIndex];
        var buckets = new Dictionary<GroupKey, List<ProcessedDataRow>>();
        var order = new List<GroupKey>();

        foreach (var row in rows)
        {
            var value = ReportAggregateEngine.EvaluateForRow(level.Expression, row, [row], context, rows);
            var key = new GroupKey(value.RawValue);
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = [];
                buckets[key] = bucket;
                order.Add(key);
            }

            bucket.Add(row);
        }

        var groups = order
            .Select(key => new ProcessedGroup(
                level.Name,
                levelIndex,
                key.Value,
                buckets[key],
                GroupRows(buckets[key], levels, levelIndex + 1, context)))
            .ToArray();

        return SortGroups(groups, level.Sorts, level.SortExpressions, context);
    }

    private static IReadOnlyList<ProcessedGroup> SortGroups(
        IReadOnlyList<ProcessedGroup> groups,
        IReadOnlyList<ReportSortDefinition> sorts,
        IReadOnlyList<ExpressionNode> sortExpressions,
        ReportProcessingContext context)
    {
        if (sorts.Count == 0)
        {
            return groups;
        }

        return groups
            .Select((group, ordinal) => new GroupSortRow(
                group,
                ordinal,
                sortExpressions.Select(sort => ReportAggregateEngine.Evaluate(sort, group.Rows, context)).ToArray()))
            .OrderBy(row => row, new GroupSortRowComparer(sorts, context))
            .Select(row => row.Group)
            .ToArray();
    }

    private sealed record GroupSortRow(ProcessedGroup Group, int Ordinal, IReadOnlyList<ExpressionValue> Keys);

    private sealed record ParsedGroupLevel(
        string Name,
        ExpressionNode Expression,
        IReadOnlyList<ReportSortDefinition> Sorts,
        IReadOnlyList<ExpressionNode> SortExpressions);

    private sealed class GroupSortRowComparer : IComparer<GroupSortRow>
    {
        private readonly IReadOnlyList<ReportSortDefinition> _sorts;
        private readonly ReportProcessingContext _context;

        public GroupSortRowComparer(IReadOnlyList<ReportSortDefinition> sorts, ReportProcessingContext context)
        {
            _sorts = sorts;
            _context = context;
        }

        public int Compare(GroupSortRow? x, GroupSortRow? y)
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

    private readonly record struct GroupKey(object? Value);
}
