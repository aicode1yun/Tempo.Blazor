// Phase D — render/text-overlap.mjs
// `overlapArea(a, b)` — pixel area of the intersection of two `{x,y,width,height}`
//   rects. 0 when they don't overlap.
// `createTextOverlapArea({document, NodeFilter})` → `textOverlapArea(root, rect)`
//   — sums up `overlapArea` between `rect` and every visible text node line rect
//   inside `root`, skipping nodes whose ancestor is an editor floating element
//   (toolbar, figure, side panel). Returns total intersection area, used by the
//   chrome to pick the page where floating UI should anchor.
// `document` and `NodeFilter` are injected so the helper is testable in a non-DOM
// realm; default to globals when available.

const FLOATING_ANCESTOR_SELECTOR = 'figure, [role="menu"], [data-testid*="toolbar"], '
    + '.tm-document-editor__floating-root, [data-testid="document-side-panel"]';

export function overlapArea(a, b) {
    const width = Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x));
    const height = Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
    return width * height;
}

export function createTextOverlapArea(options) {
    const opts = options || {};
    const doc = opts.document
        || (typeof document !== 'undefined' ? document : null);
    const NF = opts.NodeFilter
        || (typeof NodeFilter !== 'undefined' ? NodeFilter : null);

    return function textOverlapArea(root, rect) {
        if (!root || !doc || !NF || typeof doc.createTreeWalker !== 'function') return 0;
        let total = 0;
        const walker = doc.createTreeWalker(root, NF.SHOW_TEXT, {
            acceptNode(node) {
                const parent = node.parentElement;
                if (!node.nodeValue || !node.nodeValue.trim()) return NF.FILTER_REJECT;
                if (!parent || parent.closest(FLOATING_ANCESTOR_SELECTOR)) {
                    return NF.FILTER_REJECT;
                }
                return NF.FILTER_ACCEPT;
            },
        });
        while (walker.nextNode()) {
            const range = doc.createRange();
            range.selectNodeContents(walker.currentNode);
            Array.from(range.getClientRects()).forEach(function (lineRect) {
                total += overlapArea(rect, {
                    x: lineRect.x || lineRect.left || 0,
                    y: lineRect.y || lineRect.top || 0,
                    width: lineRect.width || 0,
                    height: lineRect.height || 0,
                });
            });
        }
        return total;
    };
}
