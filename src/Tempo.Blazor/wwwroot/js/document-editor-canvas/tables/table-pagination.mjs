export function createTablePaginationState(context = {}) {
    const page = context.page || { index: 0, body: { y: 0, height: 900 } };
    return {
        page,
        cursorY: Number(context.y ?? page.body?.y ?? 0) || 0,
        pageIndex: Number(page.index || 0) || 0,
        firstPageIndex: Number(page.index || 0) || 0,
        splitCount: 0,
    };
}

export function ensureTableRowPage(state, rowHeight, context = {}, options = {}) {
    const body = state.page?.body || {};
    const bottom = Number(body.bottom ?? (Number(body.y || 0) + Number(body.height || 0))) || 0;
    const startsAtTop = Math.abs((Number(state.cursorY || 0) || 0) - (Number(body.y || 0) || 0)) < 0.0001;
    if (startsAtTop || state.cursorY + rowHeight <= bottom + 0.0001) {
        return { moved: false, repeatedHeader: false };
    }

    const nextIndex = state.pageIndex + 1;
    const nextPage = typeof context.ensurePage === 'function'
        ? context.ensurePage(nextIndex)
        : clonePageForIndex(state.page, nextIndex);
    state.page = nextPage;
    state.pageIndex = Number(nextPage.index || nextIndex) || nextIndex;
    state.cursorY = Number(nextPage.body?.y || 0) || 0;
    state.splitCount += 1;
    return {
        moved: true,
        repeatedHeader: options.repeatHeader === true,
        page: nextPage,
    };
}

export function tableHeaderRows(measuredRows, table) {
    const layout = table?.layout || table?.Layout || {};
    const explicitCount = Math.max(0, Math.trunc(Number(layout.repeatHeaderRowCount ?? layout.RepeatHeaderRowCount ?? 0) || 0));
    if (explicitCount > 0) {
        return measuredRows.slice(0, explicitCount);
    }

    return measuredRows.filter(row => row.rowIndex === 0 || row.measuredCells.some(cell => cell.cell?.isHeader === true || cell.cell?.IsHeader === true));
}

function clonePageForIndex(page, index) {
    return {
        ...(page || {}),
        index,
        body: { ...(page?.body || {}) },
        columns: Array.isArray(page?.columns) ? page.columns.map(column => ({ ...column })) : [],
    };
}
