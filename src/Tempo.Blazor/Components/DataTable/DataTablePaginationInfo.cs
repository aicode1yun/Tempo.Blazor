namespace Tempo.Blazor.Components.DataTable;

/// <summary>
/// Paging state handed to a host-supplied pagination info template
/// (<see cref="TmDataTable{TItem}.PaginationInfoTemplate"/>).
/// </summary>
/// <param name="CurrentPage">1-based index of the page currently displayed.</param>
/// <param name="TotalPages">Total number of pages.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="TotalCount">Total number of items across all pages.</param>
/// <param name="StartItem">1-based index of the first item on the current page, or 0 when there are no items.</param>
/// <param name="EndItem">1-based index of the last item on the current page, or 0 when there are no items.</param>
public sealed record DataTablePaginationInfo(
    int CurrentPage,
    int TotalPages,
    int PageSize,
    int TotalCount,
    int StartItem,
    int EndItem);
