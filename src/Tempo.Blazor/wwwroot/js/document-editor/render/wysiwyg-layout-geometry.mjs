// Phase D — render/wysiwyg-layout-geometry.mjs
// `createIsWysiwygLayoutElementVisible({window?})` →
//   `isWysiwygLayoutElementVisible(element)` — true when the element has a non-zero
//   bounding rect AND (when getComputedStyle is available) is not `display:none` /
//   `visibility:hidden` / `opacity ≤ 0.01`.
// `getWysiwygRectRelativeTo(rect, origin)` — translate a DOMRect-like to a
//   `{x,y,width,height}` relative to `origin`. Accepts `left/top` or `x/y`.
// `createUnionWysiwygRects({asArray})` → `unionWysiwygRects(rects)` — bounding
//   rect of a list; degenerate rects filtered. Returns null when nothing usable.

export function createIsWysiwygLayoutElementVisible(options) {
    const opts = options || {};
    const win = opts.window
        || (typeof window !== 'undefined' ? window : null);
    return function isWysiwygLayoutElementVisible(element) {
        if (!element || typeof element.getBoundingClientRect !== 'function') return false;
        const rect = element.getBoundingClientRect();
        if (!rect || rect.width <= 0.5 || rect.height <= 0.5) return false;
        if (!win || !win.getComputedStyle) return true;
        const style = win.getComputedStyle(element);
        return style.display !== 'none'
            && style.visibility !== 'hidden'
            && Number(style.opacity || 1) > 0.01;
    };
}

export function getWysiwygRectRelativeTo(rect, origin) {
    return {
        x: Number((rect && (rect.left ?? rect.x)) || 0)
            - Number((origin && (origin.left ?? origin.x)) || 0),
        y: Number((rect && (rect.top ?? rect.y)) || 0)
            - Number((origin && (origin.top ?? origin.y)) || 0),
        width: Math.max(0, Number((rect && rect.width) || 0) || 0),
        height: Math.max(0, Number((rect && rect.height) || 0) || 0),
    };
}

export function createUnionWysiwygRects(options) {
    const opts = options || {};
    if (typeof opts.asArray !== 'function') {
        throw new TypeError(
            'createUnionWysiwygRects requires options.asArray (function)');
    }
    const { asArray } = opts;
    return function unionWysiwygRects(rects) {
        const list = asArray(rects).filter(function (rect) {
            return rect && rect.width > 0 && rect.height > 0;
        });
        if (!list.length) return null;
        const left = Math.min.apply(null, list.map(function (rect) { return rect.x; }));
        const top = Math.min.apply(null, list.map(function (rect) { return rect.y; }));
        const right = Math.max.apply(null, list.map(function (rect) {
            return rect.x + rect.width;
        }));
        const bottom = Math.max.apply(null, list.map(function (rect) {
            return rect.y + rect.height;
        }));
        return {
            x: left,
            y: top,
            width: Math.max(0, right - left),
            height: Math.max(0, bottom - top),
        };
    };
}
