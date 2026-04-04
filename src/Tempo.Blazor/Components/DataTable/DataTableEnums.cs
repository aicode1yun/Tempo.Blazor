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
