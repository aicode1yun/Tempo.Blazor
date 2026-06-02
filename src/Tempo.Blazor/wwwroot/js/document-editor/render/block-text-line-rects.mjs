// Phase D — render/block-text-line-rects.mjs
// `createBlockTextLineRectsFromDom({blockText, rectFromGeometry, document})` →
//   `blockTextLineRectsFromDom(blockElement, block)` — produces per-line bounding
//   rects + char-offset ranges for a paragraph by collapsing the DOM Range's
//   getClientRects() and apportioning the text length evenly across the surviving
//   rects. Falls back to a single `getBoundingClientRect` when Range isn't
//   available. Returns `[]` for elements with no measurable rect.
//
// `document` is injected (defaults to `blockElement.ownerDocument` then the
// global) so the helper is testable in a JSDOM-less realm.

export function createBlockTextLineRectsFromDom(options) {
    const opts = options || {};
    for (const key of ['blockText', 'rectFromGeometry']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createBlockTextLineRectsFromDom requires options.${key} (function)`);
        }
    }
    const { blockText, rectFromGeometry } = opts;
    const fallbackDoc = opts.document || null;

    return function blockTextLineRectsFromDom(blockElement, block) {
        if (!blockElement || typeof blockElement.getBoundingClientRect !== 'function') {
            return [];
        }
        const textLength = blockText(block).length;
        let rects = [];
        const ownerDocument = blockElement.ownerDocument
            || fallbackDoc
            || (typeof document !== 'undefined' ? document : null);
        if (ownerDocument && typeof ownerDocument.createRange === 'function') {
            try {
                const range = ownerDocument.createRange();
                range.selectNodeContents(blockElement);
                rects = Array.from(range.getClientRects())
                    .map(rectFromGeometry)
                    .filter(function (rect) { return rect.width > 0.5 && rect.height > 0.5; });
                if (range.detach) range.detach();
            } catch (_) {
                rects = [];
            }
        }
        if (!rects.length) {
            const fallback = rectFromGeometry(blockElement.getBoundingClientRect());
            if (fallback.width > 0.5 && fallback.height > 0.5) rects = [fallback];
        }
        if (!rects.length) return [];
        const perLine = Math.max(1, Math.ceil(Math.max(1, textLength) / rects.length));
        return rects.map(function (rect, index) {
            const start = textLength === 0 ? 0 : Math.min(textLength, index * perLine);
            const end = index === rects.length - 1
                ? textLength
                : Math.min(textLength, (index + 1) * perLine);
            return {
                rect,
                start,
                end: Math.max(start, end),
                empty: textLength === 0,
            };
        });
    };
}
