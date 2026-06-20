// Phase D — render/selection-rect.mjs
// `selectedDomRect(selection)` — returns the bounding rectangle of the current DOM
// selection (across all client rects, filtered to non-degenerate). Falls back to
// `getBoundingClientRect()` when `getClientRects()` is empty. Returns null for
// collapsed/empty selections.
//
// The returned shape is the union rect: { left, top, width, height, right, bottom }.

export function selectedDomRect(selection) {
    if (!selection
        || !selection.rangeCount
        || typeof selection.getRangeAt !== 'function'
        || selection.isCollapsed) return null;
    const range = selection.getRangeAt(0);
    const rects = Array.from(range.getClientRects ? range.getClientRects() : [])
        .filter(function (rect) {
            return rect && rect.width > 0.5 && rect.height > 0.5;
        });
    if (rects.length === 0) {
        const fallback = range.getBoundingClientRect ? range.getBoundingClientRect() : null;
        if (fallback && fallback.width > 0.5 && fallback.height > 0.5) rects.push(fallback);
    }
    if (rects.length === 0) return null;
    const left = Math.min.apply(null, rects.map(function (rect) { return rect.left; }));
    const top = Math.min.apply(null, rects.map(function (rect) { return rect.top; }));
    const right = Math.max.apply(null, rects.map(function (rect) { return rect.right; }));
    const bottom = Math.max.apply(null, rects.map(function (rect) { return rect.bottom; }));
    return {
        left: left,
        top: top,
        width: right - left,
        height: bottom - top,
        right: right,
        bottom: bottom,
    };
}
