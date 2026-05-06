window.tmSpreadsheetGrid = window.tmSpreadsheetGrid || {};

window.tmSpreadsheetGrid.observeViewport = function (grid, dotNetRef) {
    if (!grid || !dotNetRef) return;

    if (typeof grid.__tmSpreadsheetViewportCleanup === "function") {
        grid.__tmSpreadsheetViewportCleanup();
    }

    let frame = 0;
    const notify = () => {
        if (frame) return;
        frame = requestAnimationFrame(() => {
            frame = 0;
            dotNetRef.invokeMethodAsync(
                "OnSpreadsheetViewportChanged",
                grid.scrollLeft || 0,
                grid.clientWidth || 0
            ).catch(() => {
                // Component was disposed before the queued viewport update ran.
            });
        });
    };

    const resizeObserver = typeof ResizeObserver !== "undefined"
        ? new ResizeObserver(notify)
        : null;

    grid.addEventListener("scroll", notify, { passive: true });
    if (resizeObserver) {
        resizeObserver.observe(grid);
    }

    grid.__tmSpreadsheetViewportCleanup = () => {
        grid.removeEventListener("scroll", notify);
        if (resizeObserver) {
            resizeObserver.disconnect();
        }
        if (frame) {
            cancelAnimationFrame(frame);
            frame = 0;
        }
        delete grid.__tmSpreadsheetViewportCleanup;
    };

    notify();
};

window.tmSpreadsheetGrid.disposeViewportObserver = function (grid) {
    if (!grid || typeof grid.__tmSpreadsheetViewportCleanup !== "function") return;
    grid.__tmSpreadsheetViewportCleanup();
};

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
