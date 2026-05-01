using Tempo.Blazor.Abstractions.PivotTable;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Services;

/// <summary>
/// Static engine that transforms flat data into a pivot table matrix.
/// Supports multi-level row/column dimensions, multiple value fields with aggregations,
/// filters, and totals.
/// </summary>
public static class PivotEngine
{
    /// <summary>
    /// Transforms a collection of items into a pivot table result.
    /// </summary>
    /// <typeparam name="TItem">The type of data item.</typeparam>
    /// <param name="items">Source data collection.</param>
    /// <param name="configuration">Pivot table field configuration.</param>
    /// <param name="fields">Available field definitions.</param>
    /// <returns>The computed pivot table result.</returns>
    public static PivotTableResult Transform<TItem>(
        IEnumerable<TItem> items,
        PivotTableConfiguration configuration,
        IReadOnlyList<PivotField<TItem>> fields)
    {
        var rowFields = ResolveFields(configuration.RowFieldKeys, fields);
        var columnFields = ResolveFields(configuration.ColumnFieldKeys, fields);
        var valueFields = ResolveValueFields(configuration.ValueFields, fields);
        var filterFields = ResolveFilterFields(configuration.FilterFields, fields);

        var filtered = ApplyFilters(items, filterFields);

        if (rowFields.Count == 0 && columnFields.Count == 0)
        {
            return new PivotTableResult
            {
                Rows = [],
                Columns = [],
                Cells = new PivotCell[0, 0],
                GrandTotals = [],
                ValueFieldCount = valueFields.Count,
                Configuration = configuration,
                LeafRowCount = 0,
                LeafColumnCount = 0
            };
        }

        var groups = GroupByDimensions(filtered, rowFields, columnFields);
        var aggregatedData = ComputeAggregations(groups, valueFields);

        var rowKeys = aggregatedData.Keys.Select(k => k.RowKey).Distinct(new KeyComparer()).ToList();
        var columnKeys = aggregatedData.Keys.Select(k => k.ColumnKey).Distinct(new KeyComparer()).ToList();

        var (rowTree, rowLeafKeys) = BuildRowTree(rowKeys, rowFields);
        var (columnTree, columnLeafKeys) = BuildColumnTree(columnKeys, columnFields);

        var flatRows = FlattenRows(rowTree);
        var flatColumns = FlattenColumns(columnTree);

        var cells = BuildCellMatrix(flatRows, flatColumns, rowLeafKeys, columnLeafKeys, aggregatedData, valueFields);

        var rowTotals = ComputeRowTotals(flatRows, flatColumns, cells, valueFields);
        var columnTotals = ComputeColumnTotals(flatRows, flatColumns, cells, valueFields);
        var grandTotals = ComputeGrandTotals(flatRows, flatColumns, cells, valueFields);

        AttachRowTotals(flatRows, rowTotals);
        AttachColumnTotals(flatColumns, columnTotals);

        return new PivotTableResult
        {
            Rows = rowTree,
            Columns = columnTree,
            Cells = cells,
            GrandTotals = grandTotals,
            ValueFieldCount = valueFields.Count,
            Configuration = configuration,
            LeafRowCount = flatRows.Count,
            LeafColumnCount = flatColumns.Count
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  Field Resolution
    // ═══════════════════════════════════════════════════════════════

    private static List<PivotField<TItem>> ResolveFields<TItem>(
        IReadOnlyList<string> keys,
        IReadOnlyList<PivotField<TItem>> fields)
    {
        return keys.Select(k => fields.FirstOrDefault(f => f.Key == k)
            ?? throw new InvalidOperationException($"Pivot field '{k}' not found."))
            .ToList();
    }

    private static List<(PivotValueFieldConfiguration Config, PivotField<TItem> Field)> ResolveValueFields<TItem>(
        IReadOnlyList<PivotValueFieldConfiguration> valueConfigs,
        IReadOnlyList<PivotField<TItem>> fields)
    {
        return valueConfigs.Select(cfg =>
        {
            var field = fields.FirstOrDefault(f => f.Key == cfg.FieldKey)
                ?? throw new InvalidOperationException($"Value field '{cfg.FieldKey}' not found.");
            return (cfg, field);
        }).ToList();
    }

    private static List<(PivotField<TItem> Field, List<object?> Values)> ResolveFilterFields<TItem>(
        IReadOnlyDictionary<string, List<object?>> filterConfig,
        IReadOnlyList<PivotField<TItem>> fields)
    {
        return filterConfig.Select(kv =>
        {
            var field = fields.FirstOrDefault(f => f.Key == kv.Key)
                ?? throw new InvalidOperationException($"Filter field '{kv.Key}' not found.");
            return (field, kv.Value);
        }).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Filtering
    // ═══════════════════════════════════════════════════════════════

    private static IEnumerable<TItem> ApplyFilters<TItem>(
        IEnumerable<TItem> items,
        IReadOnlyList<(PivotField<TItem> Field, List<object?> Values)> filters)
    {
        if (filters.Count == 0)
            return items;

        return items.Where(item =>
            filters.All(filter =>
            {
                var value = filter.Field.Accessor(item);
                return filter.Values.Any(v => Equals(v, value));
            }));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Grouping by Dimensions
    // ═══════════════════════════════════════════════════════════════

    private static Dictionary<(DimensionKey RowKey, DimensionKey ColumnKey), List<TItem>> GroupByDimensions<TItem>(
        IEnumerable<TItem> items,
        IReadOnlyList<PivotField<TItem>> rowFields,
        IReadOnlyList<PivotField<TItem>> columnFields)
    {
        return items.GroupBy(item =>
        {
            var rowKey = new DimensionKey(rowFields.Select(f => f.Accessor(item)).ToList());
            var colKey = new DimensionKey(columnFields.Select(f => f.Accessor(item)).ToList());
            return (rowKey, colKey);
        }).ToDictionary(g => g.Key, g => g.ToList());
    }

    private sealed record DimensionKey(IReadOnlyList<object?> Values)
    {
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var v in Values)
                hash.Add(v);
            return hash.ToHashCode();
        }

        public bool Equals(DimensionKey? other)
        {
            if (other is null) return false;
            if (Values.Count != other.Values.Count) return false;
            for (var i = 0; i < Values.Count; i++)
                if (!Equals(Values[i], other.Values[i]))
                    return false;
            return true;
        }
    }

    private sealed class KeyComparer : IEqualityComparer<DimensionKey>
    {
        public bool Equals(DimensionKey? x, DimensionKey? y) => x?.Equals(y) ?? y is null;
        public int GetHashCode(DimensionKey obj) => obj.GetHashCode();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Aggregations
    // ═══════════════════════════════════════════════════════════════

    private static Dictionary<(DimensionKey RowKey, DimensionKey ColumnKey), Dictionary<int, PivotCell>> ComputeAggregations<TItem>(
        Dictionary<(DimensionKey RowKey, DimensionKey ColumnKey), List<TItem>> groups,
        IReadOnlyList<(PivotValueFieldConfiguration Config, PivotField<TItem> Field)> valueFields)
    {
        var result = new Dictionary<(DimensionKey, DimensionKey), Dictionary<int, PivotCell>>();

        foreach (var ((rowKey, colKey), groupItems) in groups)
        {
            var cellValues = new Dictionary<int, PivotCell>();
            for (var i = 0; i < valueFields.Count; i++)
            {
                var (config, field) = valueFields[i];
                var aggregateType = ParseAggregateType(config.Aggregation);
                var rawValue = ComputeSingleAggregate(groupItems, field.Accessor, aggregateType);
                var formattedValue = rawValue is not null
                    ? FormatValue(rawValue, config.Format)
                    : string.Empty;

                cellValues[i] = new PivotCell
                {
                    RawValue = rawValue,
                    FormattedValue = formattedValue,
                    Count = groupItems.Count,
                    IsNull = false
                };
            }
            result[(rowKey, colKey)] = cellValues;
        }

        return result;
    }

    private static AggregateType ParseAggregateType(string aggregation)
    {
        return Enum.TryParse<AggregateType>(aggregation, true, out var result)
            ? result
            : AggregateType.Sum;
    }

    private static object? ComputeSingleAggregate<TItem>(
        IReadOnlyList<TItem> items,
        Func<TItem, object?> accessor,
        AggregateType type)
    {
        if (items.Count == 0)
            return null;

        if (type == AggregateType.Count)
            return items.Count;

        var decimals = ExtractDecimals(items, accessor);
        if (decimals is null || decimals.Count == 0)
            return type == AggregateType.Count ? items.Count : null;

        return type switch
        {
            AggregateType.Sum => decimals.Sum(),
            AggregateType.Average => decimals.Sum() / decimals.Count,
            AggregateType.Min => decimals.Min(),
            AggregateType.Max => decimals.Max(),
            _ => null
        };
    }

    private static List<decimal>? ExtractDecimals<TItem>(
        IReadOnlyList<TItem> items,
        Func<TItem, object?> accessor)
    {
        var result = new List<decimal>(items.Count);
        foreach (var item in items)
        {
            var value = accessor(item);
            if (value is null) continue;
            if (TryConvertToDecimal(value, out var d))
                result.Add(d);
            else
                return null;
        }
        return result;
    }

    private static bool TryConvertToDecimal(object value, out decimal result)
    {
        switch (value)
        {
            case decimal d: result = d; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case double dbl: result = (decimal)dbl; return true;
            case float f: result = (decimal)f; return true;
            case short s: result = s; return true;
            case byte b: result = b; return true;
            default:
                result = 0;
                return false;
        }
    }

    private static string FormatValue(object? value, string? format)
    {
        if (value is null)
            return string.Empty;

        if (!string.IsNullOrEmpty(format) && value is IFormattable formattable)
            return formattable.ToString(format, System.Globalization.CultureInfo.CurrentCulture);

        return value.ToString() ?? string.Empty;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Tree Building (with leaf key lookup)
    // ═══════════════════════════════════════════════════════════════

    private static (List<PivotRowNode> Roots, Dictionary<PivotRowNode, DimensionKey> LeafKeys) BuildRowTree<TItem>(
        IReadOnlyList<DimensionKey> keys,
        IReadOnlyList<PivotField<TItem>> fields)
    {
        if (fields.Count == 0)
        {
            var dummy = new PivotRowNode { Key = null, DisplayValue = string.Empty, Level = 0 };
            return ([dummy], new Dictionary<PivotRowNode, DimensionKey> { [dummy] = new DimensionKey([]) });
        }

        var leafKeys = new Dictionary<PivotRowNode, DimensionKey>();
        var roots = BuildRowTreeRecursive(keys, fields, 0, leafKeys);
        return (roots, leafKeys);
    }

    private static List<PivotRowNode> BuildRowTreeRecursive<TItem>(
        IReadOnlyList<DimensionKey> keys,
        IReadOnlyList<PivotField<TItem>> fields,
        int level,
        Dictionary<PivotRowNode, DimensionKey> leafKeys)
    {
        var distinctAtLevel = keys
            .Select(k => k.Values[level])
            .Distinct()
            .OrderBy(v => v, new NaturalComparer())
            .ToList();

        var nodes = new List<PivotRowNode>();
        foreach (var value in distinctAtLevel)
        {
            var childKeys = keys.Where(k => Equals(k.Values[level], value)).ToList();
            var displayValue = fields[level].FormatValue(value);

            var node = new PivotRowNode
            {
                Key = value,
                DisplayValue = displayValue,
                Level = level
            };

            if (level < fields.Count - 1)
            {
                node.Children = BuildRowTreeRecursive(childKeys, fields, level + 1, leafKeys);
            }
            else
            {
                // Leaf node - store its full dimension key
                foreach (var key in childKeys)
                {
                    leafKeys[node] = key;
                }
            }

            nodes.Add(node);
        }

        return nodes;
    }

    private static (List<PivotColumnNode> Roots, Dictionary<PivotColumnNode, DimensionKey> LeafKeys) BuildColumnTree<TItem>(
        IReadOnlyList<DimensionKey> keys,
        IReadOnlyList<PivotField<TItem>> fields)
    {
        if (fields.Count == 0)
        {
            var dummy = new PivotColumnNode { Key = null, DisplayValue = string.Empty, Level = 0 };
            return ([dummy], new Dictionary<PivotColumnNode, DimensionKey> { [dummy] = new DimensionKey([]) });
        }

        var leafKeys = new Dictionary<PivotColumnNode, DimensionKey>();
        var roots = BuildColumnTreeRecursive(keys, fields, 0, leafKeys);
        return (roots, leafKeys);
    }

    private static List<PivotColumnNode> BuildColumnTreeRecursive<TItem>(
        IReadOnlyList<DimensionKey> keys,
        IReadOnlyList<PivotField<TItem>> fields,
        int level,
        Dictionary<PivotColumnNode, DimensionKey> leafKeys)
    {
        var distinctAtLevel = keys
            .Select(k => k.Values[level])
            .Distinct()
            .OrderBy(v => v, new NaturalComparer())
            .ToList();

        var nodes = new List<PivotColumnNode>();
        foreach (var value in distinctAtLevel)
        {
            var childKeys = keys.Where(k => Equals(k.Values[level], value)).ToList();
            var displayValue = fields[level].FormatValue(value);

            var node = new PivotColumnNode
            {
                Key = value,
                DisplayValue = displayValue,
                Level = level
            };

            if (level < fields.Count - 1)
            {
                node.Children = BuildColumnTreeRecursive(childKeys, fields, level + 1, leafKeys);
            }
            else
            {
                foreach (var key in childKeys)
                {
                    leafKeys[node] = key;
                }
            }

            nodes.Add(node);
        }

        return nodes;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Tree Flattening & Span Computation
    // ═══════════════════════════════════════════════════════════════

    private static List<PivotRowNode> FlattenRows(List<PivotRowNode> tree)
    {
        ComputeRowSpansAndIndices(tree, 0);
        var result = new List<PivotRowNode>();
        CollectLeafRows(tree, result);
        for (var i = 0; i < result.Count; i++)
            result[i].RowIndex = i;
        return result;
    }

    private static int ComputeRowSpansAndIndices(List<PivotRowNode> nodes, int startIndex)
    {
        var total = 0;
        foreach (var node in nodes)
        {
            node.RowIndex = startIndex + total;
            if (node.IsLeaf)
            {
                node.RowSpan = 1;
                total += 1;
            }
            else
            {
                node.RowSpan = ComputeRowSpansAndIndices(node.Children, startIndex + total);
                total += node.RowSpan;
            }
        }
        return total;
    }

    private static void CollectLeafRows(List<PivotRowNode> nodes, List<PivotRowNode> result)
    {
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
                result.Add(node);
            else
                CollectLeafRows(node.Children, result);
        }
    }

    private static List<PivotColumnNode> FlattenColumns(List<PivotColumnNode> tree)
    {
        ComputeColSpansAndIndices(tree, 0);
        var result = new List<PivotColumnNode>();
        CollectLeafColumns(tree, result);
        for (var i = 0; i < result.Count; i++)
            result[i].ColIndex = i;
        return result;
    }

    private static int ComputeColSpansAndIndices(List<PivotColumnNode> nodes, int startIndex)
    {
        var total = 0;
        foreach (var node in nodes)
        {
            node.ColIndex = startIndex + total;
            if (node.IsLeaf)
            {
                node.ColSpan = 1;
                total += 1;
            }
            else
            {
                node.ColSpan = ComputeColSpansAndIndices(node.Children, startIndex + total);
                total += node.ColSpan;
            }
        }
        return total;
    }

    private static void CollectLeafColumns(List<PivotColumnNode> nodes, List<PivotColumnNode> result)
    {
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
                result.Add(node);
            else
                CollectLeafColumns(node.Children, result);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Cell Matrix
    // ═══════════════════════════════════════════════════════════════

    private static PivotCell[,] BuildCellMatrix<TItem>(
        IReadOnlyList<PivotRowNode> flatRows,
        IReadOnlyList<PivotColumnNode> flatColumns,
        Dictionary<PivotRowNode, DimensionKey> rowLeafKeys,
        Dictionary<PivotColumnNode, DimensionKey> columnLeafKeys,
        Dictionary<(DimensionKey RowKey, DimensionKey ColumnKey), Dictionary<int, PivotCell>> aggregatedData,
        IReadOnlyList<(PivotValueFieldConfiguration Config, PivotField<TItem> Field)> valueFields)
    {
        var rowCount = flatRows.Count;
        var colCount = flatColumns.Count * valueFields.Count;
        var cells = new PivotCell[rowCount, colCount];

        for (var r = 0; r < rowCount; r++)
        {
            var rowNode = flatRows[r];
            var rowKey = rowLeafKeys[rowNode];

            for (var c = 0; c < flatColumns.Count; c++)
            {
                var colNode = flatColumns[c];
                var colKey = columnLeafKeys[colNode];

                if (aggregatedData.TryGetValue((rowKey, colKey), out var cellValues))
                {
                    for (var v = 0; v < valueFields.Count; v++)
                    {
                        cells[r, c * valueFields.Count + v] = cellValues.TryGetValue(v, out var cell)
                            ? cell
                            : PivotCell.Null();
                    }
                }
                else
                {
                    for (var v = 0; v < valueFields.Count; v++)
                    {
                        cells[r, c * valueFields.Count + v] = PivotCell.Null();
                    }
                }
            }
        }

        return cells;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Totals
    // ═══════════════════════════════════════════════════════════════

    private static Dictionary<PivotRowNode, Dictionary<int, PivotCell>> ComputeRowTotals<TItem>(
        IReadOnlyList<PivotRowNode> flatRows,
        IReadOnlyList<PivotColumnNode> flatColumns,
        PivotCell[,] cells,
        IReadOnlyList<(PivotValueFieldConfiguration Config, PivotField<TItem> Field)> valueFields)
    {
        var totals = new Dictionary<PivotRowNode, Dictionary<int, PivotCell>>();
        var rowCount = flatRows.Count;
        var colCount = flatColumns.Count;

        for (var r = 0; r < rowCount; r++)
        {
            var rowTotals = new Dictionary<int, PivotCell>();
            for (var v = 0; v < valueFields.Count; v++)
            {
                var values = new List<object?>();
                var count = 0;
                for (var c = 0; c < colCount; c++)
                {
                    var cell = cells[r, c * valueFields.Count + v];
                    if (!cell.IsNull && cell.RawValue is not null)
                    {
                        values.Add(cell.RawValue);
                        count += cell.Count;
                    }
                }

                var aggregateType = ParseAggregateType(valueFields[v].Config.Aggregation);
                var rawValue = values.Count > 0
                    ? ComputeAggregateFromValues(values, aggregateType)
                    : null;

                rowTotals[v] = new PivotCell
                {
                    RawValue = rawValue,
                    FormattedValue = FormatValue(rawValue, valueFields[v].Config.Format),
                    Count = count,
                    IsNull = rawValue is null
                };
            }
            totals[flatRows[r]] = rowTotals;
        }

        return totals;
    }

    private static Dictionary<PivotColumnNode, Dictionary<int, PivotCell>> ComputeColumnTotals<TItem>(
        IReadOnlyList<PivotRowNode> flatRows,
        IReadOnlyList<PivotColumnNode> flatColumns,
        PivotCell[,] cells,
        IReadOnlyList<(PivotValueFieldConfiguration Config, PivotField<TItem> Field)> valueFields)
    {
        var totals = new Dictionary<PivotColumnNode, Dictionary<int, PivotCell>>();
        var rowCount = flatRows.Count;
        var colCount = flatColumns.Count;

        for (var c = 0; c < colCount; c++)
        {
            var colTotals = new Dictionary<int, PivotCell>();
            for (var v = 0; v < valueFields.Count; v++)
            {
                var values = new List<object?>();
                var count = 0;
                for (var r = 0; r < rowCount; r++)
                {
                    var cell = cells[r, c * valueFields.Count + v];
                    if (!cell.IsNull && cell.RawValue is not null)
                    {
                        values.Add(cell.RawValue);
                        count += cell.Count;
                    }
                }

                var aggregateType = ParseAggregateType(valueFields[v].Config.Aggregation);
                var rawValue = values.Count > 0
                    ? ComputeAggregateFromValues(values, aggregateType)
                    : null;

                colTotals[v] = new PivotCell
                {
                    RawValue = rawValue,
                    FormattedValue = FormatValue(rawValue, valueFields[v].Config.Format),
                    Count = count,
                    IsNull = rawValue is null
                };
            }
            totals[flatColumns[c]] = colTotals;
        }

        return totals;
    }

    private static Dictionary<int, PivotCell> ComputeGrandTotals<TItem>(
        IReadOnlyList<PivotRowNode> flatRows,
        IReadOnlyList<PivotColumnNode> flatColumns,
        PivotCell[,] cells,
        IReadOnlyList<(PivotValueFieldConfiguration Config, PivotField<TItem> Field)> valueFields)
    {
        var grandTotals = new Dictionary<int, PivotCell>();
        var rowCount = flatRows.Count;
        var colCount = flatColumns.Count;

        for (var v = 0; v < valueFields.Count; v++)
        {
            var values = new List<object?>();
            var count = 0;
            for (var r = 0; r < rowCount; r++)
            {
                for (var c = 0; c < colCount; c++)
                {
                    var cell = cells[r, c * valueFields.Count + v];
                    if (!cell.IsNull && cell.RawValue is not null)
                    {
                        values.Add(cell.RawValue);
                        count += cell.Count;
                    }
                }
            }

            var aggregateType = ParseAggregateType(valueFields[v].Config.Aggregation);
            var rawValue = values.Count > 0
                ? ComputeAggregateFromValues(values, aggregateType)
                : null;

            grandTotals[v] = new PivotCell
            {
                RawValue = rawValue,
                FormattedValue = FormatValue(rawValue, valueFields[v].Config.Format),
                Count = count,
                IsNull = rawValue is null
            };
        }

        return grandTotals;
    }

    private static object? ComputeAggregateFromValues(IReadOnlyList<object?> values, AggregateType type)
    {
        if (values.Count == 0)
            return null;

        if (type == AggregateType.Count)
            return values.Count;

        var decimals = new List<decimal>();
        foreach (var v in values)
        {
            if (v is null) continue;
            if (TryConvertToDecimal(v, out var d))
                decimals.Add(d);
            else
                return null;
        }

        if (decimals.Count == 0)
            return null;

        return type switch
        {
            AggregateType.Sum => decimals.Sum(),
            AggregateType.Average => decimals.Sum() / decimals.Count,
            AggregateType.Min => decimals.Min(),
            AggregateType.Max => decimals.Max(),
            _ => null
        };
    }

    private static void AttachRowTotals(
        IReadOnlyList<PivotRowNode> flatRows,
        Dictionary<PivotRowNode, Dictionary<int, PivotCell>> totals)
    {
        foreach (var row in flatRows)
        {
            if (totals.TryGetValue(row, out var rowTotals))
            {
                row.Totals = rowTotals;
            }
        }
    }

    private static void AttachColumnTotals(
        IReadOnlyList<PivotColumnNode> flatColumns,
        Dictionary<PivotColumnNode, Dictionary<int, PivotCell>> totals)
    {
        foreach (var col in flatColumns)
        {
            if (totals.TryGetValue(col, out var colTotals))
            {
                col.Totals = colTotals;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Comparers
    // ═══════════════════════════════════════════════════════════════

    private sealed class NaturalComparer : IComparer<object?>
    {
        public int Compare(object? x, object? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            if (x is IComparable cx && y is IComparable cy && x.GetType() == y.GetType())
                return cx.CompareTo(cy);

            return string.Compare(x.ToString(), y.ToString(), StringComparison.CurrentCulture);
        }
    }
}
