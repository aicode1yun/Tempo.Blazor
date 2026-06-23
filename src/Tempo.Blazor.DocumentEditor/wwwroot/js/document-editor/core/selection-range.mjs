// Phase D — core/selection-range.mjs
// `createSelectionTextRange({createSelectionSnapshot, createLogicalPosition})` factory
// returning `selectionTextRange(selection)` — converts a raw selection-like value
// into a normalised `{blockId, start, end, collapsed, selection}` text range.
//
// Cross-block selections collapse to the focus position (callers handle multi-block
// ranges through a different path). Single-block selections are sorted (start ≤ end).

export function createSelectionTextRange(options) {
    const opts = options || {};
    if (typeof opts.createSelectionSnapshot !== 'function') {
        throw new TypeError(
            'createSelectionTextRange requires options.createSelectionSnapshot (function)');
    }
    if (typeof opts.createLogicalPosition !== 'function') {
        throw new TypeError(
            'createSelectionTextRange requires options.createLogicalPosition (function)');
    }
    const { createSelectionSnapshot, createLogicalPosition } = opts;

    function selectionTextRange(selection) {
        const snapshot = createSelectionSnapshot(selection || {});
        const anchor = createLogicalPosition(snapshot.anchor || snapshot);
        const focus = createLogicalPosition(snapshot.focus || snapshot);
        if (anchor.blockId !== focus.blockId) {
            return {
                blockId: focus.blockId,
                start: focus.offset,
                end: focus.offset,
                collapsed: true,
                selection: snapshot,
            };
        }
        const start = Math.min(anchor.offset, focus.offset);
        const end = Math.max(anchor.offset, focus.offset);
        return {
            blockId: focus.blockId,
            start: start,
            end: end,
            collapsed: start === end,
            selection: snapshot,
        };
    }

    return Object.freeze({ selectionTextRange });
}
