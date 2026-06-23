// Phase D — render/native-caret-range.mjs
// `createNativeCaretRangeFromPoint({document})` → `nativeCaretRangeFromPoint(x, y)`
// — cross-browser caret hit-test. Prefers WebKit's `document.caretRangeFromPoint`,
// falls back to the standard `document.caretPositionFromPoint` (Firefox) by
// constructing a collapsed Range at the returned offset node. Returns null when
// no caret API is available or the point doesn't resolve to a text position.
//
// `document` is injected (defaults to the global `document` when present) so the
// helper is testable in a non-DOM realm.

export function createNativeCaretRangeFromPoint(options) {
    const opts = options || {};
    const doc = opts.document
        || (typeof document !== 'undefined' ? document : null);

    return function nativeCaretRangeFromPoint(x, y) {
        if (!doc) return null;
        if (typeof doc.caretRangeFromPoint === 'function') {
            return doc.caretRangeFromPoint(x, y);
        }
        if (typeof doc.caretPositionFromPoint === 'function') {
            const position = doc.caretPositionFromPoint(x, y);
            if (!position || !position.offsetNode || typeof doc.createRange !== 'function') {
                return null;
            }
            const range = doc.createRange();
            range.setStart(position.offsetNode, position.offset);
            range.collapse(true);
            return range;
        }
        return null;
    };
}
