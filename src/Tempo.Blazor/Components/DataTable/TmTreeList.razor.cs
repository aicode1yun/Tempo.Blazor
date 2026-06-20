using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.DataTable;

/// <summary>
/// A hierarchical data table that displays flat data with parent/child relationships
/// as an expandable tree. Supports columns, row selection, inline editing, pagination,
/// and drag-to-select multi-selection.
/// </summary>
public partial class TmTreeList<TItem>
{
    private readonly List<TmTreeListColumn<TItem>> _columns = [];
    private List<TreeListItemContext<TItem>> _rows = [];
    private readonly HashSet<object> _expandedIds = new();

    // ── Parameters: data ─────────────────────────────────────────

    /// <summary>Flat list of items. Each item must expose an id and a parent id.</summary>
    [Parameter] public IEnumerable<TItem>? Items { get; set; }

    /// <summary>Function that returns the unique id of an item.</summary>
    [Parameter] public Func<TItem, object> IdSelector { get; set; } = default!;

    /// <summary>Function that returns the parent id, or <c>null</c> for root items.</summary>
    [Parameter] public Func<TItem, object?> ParentIdSelector { get; set; } = default!;

    /// <summary>Column definitions (TmTreeListColumn children).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Alternative column definitions provided as a list (useful when CascadingValue does not propagate).</summary>
    [Parameter] public List<TmTreeListColumn<TItem>>? Columns { get; set; }

    // ── Parameters: behaviour ────────────────────────────────────

    /// <summary>Initial set of expanded item ids. Mutated by user interaction.</summary>
    [Parameter] public IReadOnlySet<object>? ExpandedIds { get; set; }

    /// <summary>Enables row click selection.</summary>
    [Parameter] public bool Selectable { get; set; }

    /// <summary>Enables multi-selection via Ctrl/Shift click and drag-to-select.</summary>
    [Parameter] public bool MultiSelect { get; set; }

    /// <summary>Enables inline row editing via double-click.</summary>
    [Parameter] public bool Editable { get; set; }

    /// <summary>Indent size per level in rem units. Default: 1.5.</summary>
    [Parameter] public double IndentSize { get; set; } = 1.5;

    /// <summary>Page size for pagination. 0 disables pagination.</summary>
    [Parameter] public int PageSize { get; set; }

    /// <summary>Current page when pagination is enabled.</summary>
    [Parameter] public int CurrentPage { get; set; } = 1;

    /// <summary>Optional page-size dropdown values. When null the dropdown is hidden.</summary>
    [Parameter] public IReadOnlyList<int>? PageSizeOptions { get; set; }

    /// <summary>Additional CSS class for the wrapper element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Optional sort key selector. When set, siblings are sorted by this value.</summary>
    [Parameter] public Func<TItem, object>? SortBy { get; set; }

    /// <summary>When true, sorts in descending order. Default is ascending.</summary>
    [Parameter] public bool SortDescending { get; set; }

    /// <summary>Optional filter predicate. When set, only matching rows and their ancestors are visible.</summary>
    [Parameter] public Func<TItem, bool>? Filter { get; set; }

    /// <summary>Additional attributes spread onto the wrapper element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Events ───────────────────────────────────────────────────

    /// <summary>Fires when a row is clicked (only when <see cref="Selectable"/> is true).</summary>
    [Parameter] public EventCallback<TItem> OnRowSelect { get; set; }

    /// <summary>Fires when the selection changes (multi-select mode).</summary>
    [Parameter] public EventCallback<IReadOnlySet<object>> OnSelectionChange { get; set; }

    /// <summary>Fires when an item is expanded.</summary>
    [Parameter] public EventCallback<object> OnExpand { get; set; }

    /// <summary>Fires when an item is collapsed.</summary>
    [Parameter] public EventCallback<object> OnCollapse { get; set; }

    /// <summary>Fires when an inline edit is committed.</summary>
    [Parameter] public EventCallback<TItem> OnRowEdit { get; set; }

    // ── Selection state ──────────────────────────────────────────

    private TreeListItemContext<TItem>? _selectedContext;
    private readonly HashSet<object> _selectedIds = new();
    private object? _lastSelectedId;
    private object? _shiftAnchorId;

    // ── Inline editing state ─────────────────────────────────────

    private object? _editingRowId;

    // ── Drag-to-select state ─────────────────────────────────────

    private bool _dragSelecting;
    private object? _dragStartId;
    private readonly HashSet<object> _dragSelectedIds = new();

    // ── Pagination state ─────────────────────────────────────────

    private int _currentPage = 1;
    private int _pageSize;
    private int _totalVisibleCount;

    /// <summary>Total pages computed from visible rows and page size.</summary>
    private int TotalPages => PageSize > 0 && _totalVisibleCount > 0
        ? (int)Math.Ceiling(_totalVisibleCount / (double)PageSize)
        : 1;

    // ── Lifecycle ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ExpandedIds is not null)
        {
            _expandedIds.Clear();
            foreach (var id in ExpandedIds)
                _expandedIds.Add(id);
        }

        if (Columns is not null)
        {
            _columns.Clear();
            foreach (var col in Columns)
                if (!_columns.Contains(col))
                    _columns.Add(col);
        }

        _currentPage = Math.Max(1, CurrentPage);
        _pageSize = PageSize;

        RebuildTree();
    }

    // ── Column registry ──────────────────────────────────────────

    public void AddColumn(TmTreeListColumn<TItem> column)
    {
        if (_columns.Contains(column)) return;

        _columns.Add(column);
        StateHasChanged();
    }

    // ── Tree building ────────────────────────────────────────────

    private void RebuildTree()
    {
        if (Items is null || IdSelector is null || ParentIdSelector is null)
        {
            _rows = [];
            return;
        }

        var items = Items;
        var effectiveExpandedIds = new HashSet<object>(_expandedIds);

        // Apply filter: keep matching items and all their ancestors
        if (Filter is not null)
        {
            var list = items.ToList();
            var idMap = new Dictionary<object, TItem>();
            foreach (var item in list)
            {
                var id = IdSelector(item);
                if (id is not null) idMap[id] = item;
            }

            var keepIds = new HashSet<object>();
            foreach (var item in list)
            {
                if (Filter(item))
                {
                    var current = item;
                    while (true)
                    {
                        var id = IdSelector(current);
                        if (id is null) break;
                        keepIds.Add(id);
                        var pid = ParentIdSelector(current);
                        if (pid is null || !idMap.TryGetValue(pid, out var parent)) break;
                        current = parent;
                    }
                }
            }

            items = list.Where(x => keepIds.Contains(IdSelector(x)!)).ToList();

            // Auto-expand ancestors of filtered items so they remain visible
            foreach (var id in keepIds)
                effectiveExpandedIds.Add(id);
        }

        _rows = TreeListHelper.BuildTree(items, IdSelector, ParentIdSelector, effectiveExpandedIds, SortBy, SortDescending).ToList();
        _totalVisibleCount = _rows.Count(r => r.IsVisible);
    }

    /// <summary>Visible rows with pagination applied when <see cref="PageSize"/> is set.</summary>
    private IReadOnlyList<TreeListItemContext<TItem>> _visibleRows
    {
        get
        {
            var visible = _rows.Where(r => r.IsVisible).ToList();

            if (PageSize > 0 && visible.Count > 0)
            {
                var start = (_currentPage - 1) * PageSize;
                if (start >= visible.Count)
                {
                    start = 0;
                    _currentPage = 1;
                }
                var count = Math.Min(PageSize, visible.Count - start);
                return visible.Slice(start, count);
            }

            return visible;
        }
    }

    // ── Expand / Collapse ────────────────────────────────────────

    private async Task ToggleExpandAsync(TreeListItemContext<TItem> ctx)
    {
        var id = ctx.Id;

        if (ctx.IsExpanded)
        {
            _expandedIds.Remove(id);
            await OnCollapse.InvokeAsync(id);
        }
        else
        {
            _expandedIds.Add(id);
            await OnExpand.InvokeAsync(id);
        }

        RebuildTree();
        StateHasChanged();
    }

    // ── Selection ────────────────────────────────────────────────

    private async Task HandleRowClickAsync(TreeListItemContext<TItem> ctx, MouseEventArgs e)
    {
        if (!Selectable && !MultiSelect) return;

        var id = ctx.Id;

        if (MultiSelect)
        {
            if (e.ShiftKey && _shiftAnchorId is not null)
            {
                // Range selection
                _selectedIds.Clear();
                _selectedIds.UnionWith(_dragSelectedIds);

                var allVisible = _rows.Where(r => r.IsVisible).ToList();
                var anchorIdx = allVisible.FindIndex(r => EqualityComparer<object>.Default.Equals(r.Id, _shiftAnchorId));
                var currentIdx = allVisible.FindIndex(r => EqualityComparer<object>.Default.Equals(r.Id, id));

                if (anchorIdx >= 0 && currentIdx >= 0)
                {
                    var lo = Math.Min(anchorIdx, currentIdx);
                    var hi = Math.Max(anchorIdx, currentIdx);
                    for (int i = lo; i <= hi; i++)
                        _selectedIds.Add(allVisible[i].Id);
                }
            }
            else if (e.CtrlKey)
            {
                // Toggle individual
                if (!_selectedIds.Add(id))
                    _selectedIds.Remove(id);
                _shiftAnchorId = id;
            }
            else
            {
                // Single click in multi-select = clear and select one
                _selectedIds.Clear();
                _selectedIds.Add(id);
                _shiftAnchorId = id;
            }

            _selectedContext = ctx;
            _lastSelectedId = id;
            await OnSelectionChange.InvokeAsync(_selectedIds);
            await OnRowSelect.InvokeAsync(ctx.Item);
        }
        else
        {
            _selectedContext = ctx;
            await OnRowSelect.InvokeAsync(ctx.Item);
        }

        StateHasChanged();
    }

    // ── Drag-to-select ───────────────────────────────────────────

    private void HandleRowMouseDown(TreeListItemContext<TItem> ctx)
    {
        if (!MultiSelect) return;
        _dragSelecting = true;
        _dragStartId = ctx.Id;
        _dragSelectedIds.Clear();
        _dragSelectedIds.UnionWith(_selectedIds);
    }

    private void HandleRowMouseEnter(TreeListItemContext<TItem> ctx)
    {
        if (!MultiSelect || !_dragSelecting || _dragStartId is null) return;

        var allVisible = _rows.Where(r => r.IsVisible).ToList();
        var startIdx = allVisible.FindIndex(r => EqualityComparer<object>.Default.Equals(r.Id, _dragStartId));
        var currentIdx = allVisible.FindIndex(r => EqualityComparer<object>.Default.Equals(r.Id, ctx.Id));

        if (startIdx < 0 || currentIdx < 0) return;

        _selectedIds.Clear();
        _selectedIds.UnionWith(_dragSelectedIds);

        var lo = Math.Min(startIdx, currentIdx);
        var hi = Math.Max(startIdx, currentIdx);
        for (int i = lo; i <= hi; i++)
            _selectedIds.Add(allVisible[i].Id);

        _ = OnSelectionChange.InvokeAsync(_selectedIds);
        StateHasChanged();
    }

    private void HandleRowMouseUp(TreeListItemContext<TItem> ctx)
    {
        if (!MultiSelect || !_dragSelecting) return;
        _dragSelecting = false;
        _dragStartId = null;
        _dragSelectedIds.Clear();
        _shiftAnchorId = ctx.Id;
        _ = OnSelectionChange.InvokeAsync(_selectedIds);
    }

    // ── Inline editing ───────────────────────────────────────────

    private async Task HandleRowDoubleClickAsync(TreeListItemContext<TItem> ctx)
    {
        if (!Editable) return;
        _editingRowId = ctx.Id;
        StateHasChanged();
    }

    private bool IsEditing(TreeListItemContext<TItem> ctx) =>
        Editable && _editingRowId is not null &&
        EqualityComparer<object>.Default.Equals(_editingRowId, ctx.Id);

    private async Task CommitEditAsync()
    {
        if (_editingRowId is null) return;
        var ctx = _rows.FirstOrDefault(r => EqualityComparer<object>.Default.Equals(r.Id, _editingRowId));
        _editingRowId = null;
        if (ctx is not null)
            await OnRowEdit.InvokeAsync(ctx.Item);
        StateHasChanged();
    }

    private void HandleEditKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            _editingRowId = null;
            StateHasChanged();
        }
        else if (e.Key == "Enter")
        {
            _ = CommitEditAsync();
        }
    }

    // ── Pagination handlers ──────────────────────────────────────

    private async Task HandlePageChangeAsync(int page)
    {
        _currentPage = page;
        CurrentPage = page;
        await OnPageChange.InvokeAsync(page);
        StateHasChanged();
    }

    private async Task HandlePageSizeChangeAsync(int size)
    {
        _pageSize = size;
        PageSize = size;
        _currentPage = 1;
        CurrentPage = 1;
        await OnPageSizeChange.InvokeAsync(size);
        StateHasChanged();
    }

    [Parameter] public EventCallback<int> OnPageChange { get; set; }
    [Parameter] public EventCallback<int> OnPageSizeChange { get; set; }

    // ── CSS helpers ──────────────────────────────────────────────

    private static string GetAlignClass(ColumnAlign align) => align switch
    {
        ColumnAlign.Center => "tm-tree-list-cell--center",
        ColumnAlign.Right => "tm-tree-list-cell--right",
        _ => "",
    };

    private static string GetColumnWidthStyle(TmTreeListColumn<TItem> col) =>
        string.IsNullOrEmpty(col.Width) ? "" : $"width: {col.Width};";

    private string GetRowClass(TreeListItemContext<TItem> ctx)
    {
        var cls = "";
        if (ctx.Level == 0) cls += " tm-tree-list-row--root";
        if (ctx.HasChildren) cls += " tm-tree-list-row--parent";
        if (Selectable && !MultiSelect && _selectedContext == ctx) cls += " tm-tree-list-row--selected";
        if (MultiSelect && _selectedIds.Contains(ctx.Id)) cls += " tm-tree-list-row--selected";
        if (IsEditing(ctx)) cls += " tm-tree-list-row--editing";
        return cls.Trim();
    }

    private string GetEditingCellClass(TreeListItemContext<TItem> ctx, TmTreeListColumn<TItem> col) =>
        IsEditing(ctx) && col.Editable ? "tm-tree-list-cell--editing" : "";

    private static string FormatValue(object? value, string? format)
    {
        if (value is null) return string.Empty;
        if (string.IsNullOrEmpty(format)) return value.ToString() ?? string.Empty;
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, format, value);
    }
}
