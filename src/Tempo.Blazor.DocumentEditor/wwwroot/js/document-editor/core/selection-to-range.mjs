// Phase D — core/selection-to-range.mjs
// `createSelectionToRange({createSelectionSnapshot})` factory →
//   `selectionToRange(selection)` — coerces a selection (any shape — collapsed,
//   text range, object) into `{blockId, start, end, region, headerFooterId,
//   tableId, cellId}`. Collapsed selections keep `start === end === snapshot.offset`.
//   Text-range selections normalise anchor/focus into ascending start/end. Active
//   table id/cell id wins over passive anchor/focus values.

export function createSelectionToRange(options) {
    const opts = options || {};
    if (typeof opts.createSelectionSnapshot !== 'function') {
        throw new TypeError(
            'createSelectionToRange requires options.createSelectionSnapshot (function)');
    }
    const { createSelectionSnapshot } = opts;

    return function selectionToRange(selection) {
        const snapshot = createSelectionSnapshot(selection || {});
        if (snapshot.isCollapsed !== false) {
            return {
                blockId: snapshot.blockId,
                start: Number(snapshot.offset || 0),
                end: Number(snapshot.offset || 0),
                region: snapshot.region || 'Body',
                headerFooterId: snapshot.headerFooterId || null,
                tableId: snapshot.activeTableId || snapshot.tableId || null,
                cellId: snapshot.activeTableCellId || snapshot.cellId || null,
            };
        }
        const anchor = snapshot.anchor || {};
        const focus = snapshot.focus || {};
        const start = Math.min(Number(anchor.offset || 0), Number(focus.offset || 0));
        const end = Math.max(Number(anchor.offset || 0), Number(focus.offset || 0));
        return {
            blockId: focus.blockId || anchor.blockId || snapshot.blockId,
            start,
            end,
            region: snapshot.region || focus.region || anchor.region || 'Body',
            headerFooterId: snapshot.headerFooterId
                || focus.headerFooterId || anchor.headerFooterId || null,
            tableId: snapshot.activeTableId || snapshot.tableId
                || focus.tableId || anchor.tableId || null,
            cellId: snapshot.activeTableCellId || snapshot.cellId
                || focus.cellId || anchor.cellId || null,
        };
    };
}
