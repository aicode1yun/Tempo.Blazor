namespace Tempo.Blazor.Components.DataTable;

/// <summary>Filter input type for a TmDataTableColumn.</summary>
public enum FilterType
{
    /// <summary>Text-based filtering with string comparison.</summary>
    Text,
    /// <summary>Numeric filtering with number operators.</summary>
    Number,
    /// <summary>Date filtering with date range operators.</summary>
    Date,
    /// <summary>Boolean filtering with true/false options.</summary>
    Boolean,
    /// <summary>Select dropdown filtering from predefined options.</summary>
    Select
}

/// <summary>Horizontal alignment of cell content in a TmDataTableColumn.</summary>
public enum ColumnAlign
{
    /// <summary>Left-aligned content.</summary>
    Left,
    /// <summary>Center-aligned content.</summary>
    Center,
    /// <summary>Right-aligned content.</summary>
    Right
}

/// <summary>Scroll/pagination mode for data components.</summary>
public enum DataTableScrollMode
{
    /// <summary>Classic pagination with page controls.</summary>
    Pagination,

    /// <summary>Virtualized infinite scroll rendering only visible items.</summary>
    Virtualized
}

/// <summary>
/// High-level preset that controls which chrome elements are rendered by data components
/// such as <see cref="TmDataTable{TItem}"/> and <see cref="TmMultiViewList{TItem}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Full"/> is the default and preserves the existing boolean-parameter behavior:
/// <c>ShowSearch</c>, <c>ShowColumnPicker</c>, <c>ShowViewManager</c>, <c>ShowViewSwitcher</c>,
/// and <c>ShowExternalFilterBuilder</c> control their respective elements.
/// </para>
/// <para>
/// All other modes are higher-level presets that override those booleans for the elements
/// they affect. <c>ShowToolbar</c> still governs whether the toolbar container itself is
/// eligible to render (the container is suppressed when no visible control remains).
/// </para>
/// </remarks>
public enum DataToolbarMode
{
    /// <summary>
    /// Default mode. All toolbar chrome is rendered according to the individual
    /// <c>Show*</c> boolean parameters.
    /// </summary>
    Full,

    /// <summary>
    /// Only the global search input is shown. Column picker, view manager, view switcher,
    /// group picker, and external filter builder are hidden.
    /// </summary>
    SearchOnly,

    /// <summary>
    /// Only action controls are shown: column picker, view manager, view switcher, and
    /// group picker (where applicable). The search input and external filter builder are hidden.
    /// </summary>
    ActionsOnly,

    /// <summary>
    /// No toolbar and no external filter builder are rendered; only the data surface is shown.
    /// </summary>
    ContentOnly
}
