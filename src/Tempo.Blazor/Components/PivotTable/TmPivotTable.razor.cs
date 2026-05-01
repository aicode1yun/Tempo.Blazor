using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Abstractions.PivotTable;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Components.PivotTable;

/// <summary>
/// A fully-featured pivot table component that transforms flat data into a matrix view.
/// Supports multi-level row and column dimensions, multiple aggregations, filters, and totals.
/// </summary>
/// <typeparam name="TItem">The type of the data item.</typeparam>
public partial class TmPivotTable<TItem>
{
    // ── State ────────────────────────────────────────────────────
    private PivotTableResult? _result;
    private bool _isLoading;
    private bool _hasInitialized;

    // ── Parameters: Data ─────────────────────────────────────────

    /// <summary>Source data items for in-memory pivot transformation.</summary>
    [Parameter] public IEnumerable<TItem>? Items { get; set; }

    /// <summary>Server-side data provider. When set, overrides Items and calls GetPivotDataAsync.</summary>
    [Parameter] public IPivotDataProvider<TItem>? DataProvider { get; set; }

    /// <summary>All available field definitions for the pivot table.</summary>
    [Parameter] public List<PivotField<TItem>> Fields { get; set; } = [];

    // ── Parameters: Configuration ────────────────────────────────

    /// <summary>Keys of fields placed in the row area.</summary>
    [Parameter] public List<string> RowFieldKeys { get; set; } = [];

    /// <summary>Keys of fields placed in the column area.</summary>
    [Parameter] public List<string> ColumnFieldKeys { get; set; } = [];

    /// <summary>Value field configurations for the data area.</summary>
    [Parameter] public List<PivotValueFieldConfiguration> ValueFields { get; set; } = [];

    /// <summary>Filter field configurations. Key = field key, Value = allowed values.</summary>
    [Parameter] public Dictionary<string, List<object?>> FilterFields { get; set; } = [];

    // ── Parameters: Behaviour ────────────────────────────────────

    /// <summary>When true, shows the configuration panel. Default: true.</summary>
    [Parameter] public bool ShowConfigurationPanel { get; set; } = true;

    /// <summary>When true, shows the filter bar. Default: true.</summary>
    [Parameter] public bool ShowFilterBar { get; set; } = true;

    /// <summary>When true, shows a checkbox tree instead of the Unused zone in the configuration panel. Default: false.</summary>
    [Parameter] public bool ShowFieldTree { get; set; }

    /// <summary>When true, enables drag-and-drop in the configuration panel. Default: true.</summary>
    [Parameter] public bool AllowDragDrop { get; set; } = true;

    /// <summary>When true, shows a loading spinner instead of the table.</summary>
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>Heading shown in the empty state when no data is available.</summary>
    [Parameter] public string? EmptyTitle { get; set; }

    // ── Parameters: Events ───────────────────────────────────────

    /// <summary>Fires when the pivot configuration changes.</summary>
    [Parameter] public EventCallback<PivotTableConfiguration> OnConfigurationChanged { get; set; }

    // ── Parameters: Templates ────────────────────────────────────

    /// <summary>Custom template for data cells (inner matrix values).</summary>
    [Parameter] public RenderFragment<PivotCellContext>? DataCellTemplate { get; set; }

    /// <summary>Custom template for row dimension header cells.</summary>
    [Parameter] public RenderFragment<PivotRowHeaderContext>? RowHeaderTemplate { get; set; }

    /// <summary>Custom template for column dimension header cells.</summary>
    [Parameter] public RenderFragment<PivotColumnHeaderContext>? ColumnHeaderTemplate { get; set; }

    // ── Parameters: Styling ──────────────────────────────────────

    /// <summary>Additional CSS class applied to the wrapper div.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes to apply to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Computed helpers ─────────────────────────────────────────

    private bool ShowLoading => _isLoading || IsLoading;

    private bool HasData => _result is not null && _result.LeafRowCount > 0 && _result.LeafColumnCount > 0;

    private int RowDimensionCount => RowFieldKeys.Count;
    private int ColumnDimensionCount => ColumnFieldKeys.Count;

    /// <summary>Number of header rows in thead (column dims + value field row).</summary>
    private int HeaderRowCount => Math.Max(1, ColumnDimensionCount + 1);

    // ── Lifecycle ─────────────────────────────────────────────────

    /// <summary>Initializes the pivot table and loads initial data.</summary>
    protected override async Task OnInitializedAsync()
    {
        if (_hasInitialized) return;

        if (DataProvider is not null)
            await RefreshDataAsync();
        else
            RefreshClientData();

        _hasInitialized = true;
    }

    /// <summary>Re-computes the pivot when configuration parameters change.</summary>
    protected override async Task OnParametersSetAsync()
    {
        if (!_hasInitialized) return;

        if (DataProvider is not null)
            await RefreshDataAsync();
        else
            RefreshClientData();
    }

    // ── Data Refresh ─────────────────────────────────────────────

    private void RefreshClientData()
    {
        if (Items is null || Fields.Count == 0)
        {
            _result = null;
            return;
        }

        var config = BuildConfiguration();
        _result = PivotEngine.Transform(Items, config, Fields);
    }

    private async Task RefreshDataAsync()
    {
        if (DataProvider is null || Fields.Count == 0)
        {
            _result = null;
            return;
        }

        _isLoading = true;
        StateHasChanged();

        try
        {
            var config = BuildConfiguration();
            var query = new PivotQuery<TItem>
            {
                Items = Items,
                Configuration = config,
                Fields = Fields
            };

            _result = await DataProvider.GetPivotDataAsync(query);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnConfigurationPanelChangedAsync(PivotTableConfiguration config)
    {
        RowFieldKeys = config.RowFieldKeys;
        ColumnFieldKeys = config.ColumnFieldKeys;
        ValueFields = config.ValueFields;
        FilterFields = config.FilterFields;

        await OnConfigurationChanged.InvokeAsync(config);

        if (DataProvider is not null)
            await RefreshDataAsync();
        else
            RefreshClientData();
    }

    private async Task OnSortChangedAsync((string FieldKey, PivotSortDirection Direction, PivotSortBy SortBy) args)
    {
        await OnConfigurationChanged.InvokeAsync(BuildConfiguration());

        if (DataProvider is not null)
            await RefreshDataAsync();
        else
            RefreshClientData();
    }

    private PivotTableConfiguration BuildConfiguration() => new()
    {
        RowFieldKeys = RowFieldKeys.ToList(),
        ColumnFieldKeys = ColumnFieldKeys.ToList(),
        ValueFields = ValueFields.ToList(),
        FilterFields = new Dictionary<string, List<object?>>(FilterFields)
    };

    // ═══════════════════════════════════════════════════════════════
    //  Render Fragments
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Renders the thead section with column headers and value field headers.</summary>
    private RenderFragment RenderHeader() => builder =>
    {
        if (_result is null) return;

        var seq = 0;
        var colDimCount = ColumnFieldKeys.Count;
        var valueFieldCount = _result.ValueFieldCount;
        var rowDimCount = RowFieldKeys.Count;

        // Corner spans all header rows and all row dimension columns
        var cornerRowSpan = colDimCount + 1;
        var cornerColSpan = Math.Max(1, rowDimCount);

        if (colDimCount == 0)
        {
            // No column dimensions: single header row with just value field names
            builder.OpenElement(seq++, "tr");

            builder.OpenElement(seq++, "th");
            builder.AddAttribute(seq++, "class", "tm-pivot-corner");
            builder.AddAttribute(seq++, "rowspan", cornerRowSpan);
            builder.AddAttribute(seq++, "colspan", cornerColSpan);
            builder.CloseElement();

            for (var v = 0; v < valueFieldCount; v++)
            {
                builder.OpenElement(seq++, "th");
                builder.AddAttribute(seq++, "class", "tm-pivot-value-header");
                builder.AddContent(seq++, GetValueFieldDisplayName(v));
                builder.CloseElement();
            }

            builder.CloseElement();
            return;
        }

        // Column dimension header rows (one per level)
        for (var level = 0; level < colDimCount; level++)
        {
            builder.OpenElement(seq++, "tr");

            if (level == 0)
            {
                builder.OpenElement(seq++, "th");
                builder.AddAttribute(seq++, "class", "tm-pivot-corner");
                builder.AddAttribute(seq++, "rowspan", cornerRowSpan);
                builder.AddAttribute(seq++, "colspan", cornerColSpan);
                builder.CloseElement();
            }

            RenderColumnLevelNodes(_result.Columns, level, valueFieldCount, ref seq, builder);
            builder.CloseElement();
        }

        // Value field header row
        builder.OpenElement(seq++, "tr");
        RenderValueFieldHeaderCells(_result.Columns, ref seq, builder);
        builder.CloseElement();
    };

    private void RenderColumnLevelNodes(List<PivotColumnNode> nodes, int targetLevel, int valueFieldCount, ref int seq, RenderTreeBuilder builder)
    {
        foreach (var node in nodes)
        {
            if (node.Level == targetLevel)
            {
                var effectiveColSpan = node.ColSpan * valueFieldCount;

                if (ColumnHeaderTemplate is not null)
                {
                    var colPath = GetColumnPath(_result!.Columns, node.ColIndex);
                    var ctx = new PivotColumnHeaderContext
                    {
                        Text = node.DisplayValue,
                        ColumnFieldValues = colPath?.Select(n => n.DisplayValue).ToList() ?? new List<string>(),
                        Level = node.Level,
                        ColumnIndex = node.ColIndex,
                        ColSpan = effectiveColSpan
                    };

                    builder.OpenElement(seq++, "th");
                    builder.AddAttribute(seq++, "class", "tm-pivot-col-header");
                    if (effectiveColSpan > 1)
                        builder.AddAttribute(seq++, "colspan", effectiveColSpan);
                    builder.AddContent(seq++, ColumnHeaderTemplate, ctx);
                    builder.CloseElement();
                }
                else
                {
                    builder.OpenElement(seq++, "th");
                    builder.AddAttribute(seq++, "class", "tm-pivot-col-header");
                    if (effectiveColSpan > 1)
                        builder.AddAttribute(seq++, "colspan", effectiveColSpan);
                    builder.AddContent(seq++, node.DisplayValue);
                    builder.CloseElement();
                }
            }
            else if (node.Children.Count > 0)
            {
                RenderColumnLevelNodes(node.Children, targetLevel, valueFieldCount, ref seq, builder);
            }
        }
    }

    private void RenderValueFieldHeaderCells(List<PivotColumnNode> nodes, ref int seq, RenderTreeBuilder builder)
    {
        var leaves = FlattenColumnLeaves(nodes);
        foreach (var _ in leaves)
        {
            for (var v = 0; v < _result!.ValueFieldCount; v++)
            {
                builder.OpenElement(seq++, "th");
                builder.AddAttribute(seq++, "class", "tm-pivot-value-header");
                builder.AddContent(seq++, GetValueFieldDisplayName(v));
                builder.CloseElement();
            }
        }
    }

    private string GetValueFieldDisplayName(int index)
    {
        if (index < 0 || index >= ValueFields.Count)
            return string.Empty;

        var config = ValueFields[index];
        if (!string.IsNullOrEmpty(config.DisplayName))
            return config.DisplayName;

        var field = Fields.FirstOrDefault(f => f.Key == config.FieldKey);
        return field?.Title ?? config.FieldKey;
    }

    /// <summary>Renders table body rows with proper rowspan for row dimensions.</summary>
    private RenderFragment RenderBody() => builder =>
    {
        if (_result is null) return;

        var seq = 0;
        RenderRowNodesRecursive(_result.Rows, [], ref seq, builder);
    };

    private void RenderRowNodesRecursive(List<PivotRowNode> nodes, List<PivotRowNode> currentPath, ref int seq, RenderTreeBuilder builder)
    {
        foreach (var node in nodes)
        {
            var path = new List<PivotRowNode>(currentPath) { node };

            if (node.IsLeaf)
            {
                builder.OpenElement(seq++, "tr");

                // Render row dimension cells
                foreach (var pathNode in path)
                {
                    if (pathNode.RowIndex == node.RowIndex)
                    {
                        if (RowHeaderTemplate is not null)
                        {
                            var rowCtx = new PivotRowHeaderContext
                            {
                                Text = pathNode.DisplayValue,
                                RowFieldValues = path.Select(n => n.DisplayValue).ToList(),
                                Level = pathNode.Level,
                                RowIndex = node.RowIndex,
                                RowSpan = pathNode.RowSpan
                            };

                            builder.OpenElement(seq++, "td");
                            builder.AddAttribute(seq++, "class", "tm-pivot-row-dim");
                            if (pathNode.RowSpan > 1)
                                builder.AddAttribute(seq++, "rowspan", pathNode.RowSpan);
                            builder.AddContent(seq++, RowHeaderTemplate, rowCtx);
                            builder.CloseElement();
                        }
                        else
                        {
                            builder.OpenElement(seq++, "td");
                            builder.AddAttribute(seq++, "class", "tm-pivot-row-dim");
                            if (pathNode.RowSpan > 1)
                                builder.AddAttribute(seq++, "rowspan", pathNode.RowSpan);
                            builder.AddContent(seq++, pathNode.DisplayValue);
                            builder.CloseElement();
                        }
                    }
                }

                // Render data cells
                for (var c = 0; c < _result!.LeafColumnCount; c++)
                {
                    var colPath = GetColumnPath(_result.Columns, c);
                    var colFieldValues = colPath?.Select(n => n.DisplayValue).ToList() ?? new List<string>();

                    for (var v = 0; v < _result.ValueFieldCount; v++)
                    {
                        var cell = _result.Cells[node.RowIndex, c * _result.ValueFieldCount + v];

                        if (DataCellTemplate is not null)
                        {
                            var cellCtx = new PivotCellContext
                            {
                                Value = cell.RawValue,
                                FormattedValue = cell.FormattedValue,
                                IsNull = cell.IsNull,
                                RowIndex = node.RowIndex,
                                ColumnIndex = c,
                                RowFieldValues = path.Select(n => n.DisplayValue).ToList(),
                                ColumnFieldValues = colFieldValues,
                                ValueFieldIndex = v
                            };

                            builder.OpenElement(seq++, "td");
                            builder.AddAttribute(seq++, "class", $"tm-pivot-cell {(cell.IsNull ? "tm-pivot-cell-null" : "")}");
                            builder.AddContent(seq++, DataCellTemplate, cellCtx);
                            builder.CloseElement();
                        }
                        else
                        {
                            builder.OpenElement(seq++, "td");
                            builder.AddAttribute(seq++, "class", $"tm-pivot-cell {(cell.IsNull ? "tm-pivot-cell-null" : "")}");
                            builder.AddContent(seq++, cell.IsNull ? "–" : cell.FormattedValue);
                            builder.CloseElement();
                        }
                    }
                }

                builder.CloseElement();
            }
            else
            {
                RenderRowNodesRecursive(node.Children, path, ref seq, builder);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Template helpers
    // ═══════════════════════════════════════════════════════════════

    private static List<PivotColumnNode>? GetColumnPath(List<PivotColumnNode> nodes, int targetIndex, int startIndex = 0)
    {
        var current = startIndex;
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
            {
                if (current == targetIndex)
                    return new List<PivotColumnNode> { node };
                current++;
            }
            else
            {
                var path = GetColumnPath(node.Children, targetIndex, current);
                if (path is not null)
                {
                    path.Insert(0, node);
                    return path;
                }
                current += node.ColSpan;
            }
        }
        return null;
    }

    private static List<PivotRowNode>? GetRowPath(List<PivotRowNode> nodes, int targetIndex, int startIndex = 0)
    {
        var current = startIndex;
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
            {
                if (current == targetIndex)
                    return new List<PivotRowNode> { node };
                current++;
            }
            else
            {
                var path = GetRowPath(node.Children, targetIndex, current);
                if (path is not null)
                {
                    path.Insert(0, node);
                    return path;
                }
                current += node.RowSpan;
            }
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Utilities
    // ═══════════════════════════════════════════════════════════════

    private static List<PivotColumnNode> FlattenColumnLeaves(List<PivotColumnNode> nodes)
    {
        var result = new List<PivotColumnNode>();
        CollectLeafColumns(nodes, result);
        return result;
    }

    private static void CollectLeafColumns(List<PivotColumnNode> nodes, List<PivotColumnNode> result)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Count == 0)
                result.Add(node);
            else
                CollectLeafColumns(node.Children, result);
        }
    }
}
