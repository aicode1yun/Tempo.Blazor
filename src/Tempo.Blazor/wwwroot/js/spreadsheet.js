window.tmSpreadsheetGrid = window.tmSpreadsheetGrid || {};

window.tmSpreadsheetGrid.ensureCellVisible = function (grid, cell, options) {
    if (!grid || !cell) return;

    const rowHeaderWidth = options?.rowHeaderWidth ?? options?.RowHeaderWidth ?? 40;
    const columnHeaderHeight = options?.columnHeaderHeight ?? options?.ColumnHeaderHeight ?? 20;

    const left = cell.left ?? cell.Left ?? 0;
    const top = cell.top ?? cell.Top ?? 0;
    const right = cell.right ?? cell.Right ?? left;
    const bottom = cell.bottom ?? cell.Bottom ?? top;
    const frozenRow = cell.frozenRow ?? cell.FrozenRow ?? false;
    const frozenColumn = cell.frozenColumn ?? cell.FrozenColumn ?? false;

    let nextScrollLeft = grid.scrollLeft;
    let nextScrollTop = grid.scrollTop;

    if (!frozenColumn) {
        const visibleLeft = grid.scrollLeft + rowHeaderWidth;
        const visibleRight = grid.scrollLeft + grid.clientWidth;

        if (left < visibleLeft) {
            nextScrollLeft = left - rowHeaderWidth;
        } else if (right > visibleRight) {
            nextScrollLeft = right - grid.clientWidth;
        }
    }

    if (!frozenRow) {
        const visibleTop = grid.scrollTop + columnHeaderHeight;
        const visibleBottom = grid.scrollTop + grid.clientHeight;

        if (top < visibleTop) {
            nextScrollTop = top - columnHeaderHeight;
        } else if (bottom > visibleBottom) {
            nextScrollTop = bottom - grid.clientHeight;
        }
    }

    nextScrollLeft = Math.max(0, nextScrollLeft);
    nextScrollTop = Math.max(0, nextScrollTop);

    if (nextScrollLeft !== grid.scrollLeft || nextScrollTop !== grid.scrollTop) {
        grid.scrollTo({
            left: nextScrollLeft,
            top: nextScrollTop,
            behavior: "auto"
        });
    }
};
