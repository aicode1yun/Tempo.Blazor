// Phase D — render/dom-text-point.mjs
// `createFirstTextPointInElement({document, NodeFilter})` →
//   `firstTextPointInElement(element, preferredOffset)` — locates the first text
//   node under `element` (depth-first) and clamps `preferredOffset` to its length.
//   Text nodes pass through directly. Null/empty inputs return null.
// `createProjectedDomTextPointAtBlockOffset({firstTextPointInElement})` →
//   `projectedDomTextPointAtBlockOffset(block, offset)` — looks up the projected
//   layout segment (`.tm-wysiwyg-layout-segment--projected[data-model-start]
//   [data-model-end]`) covering the given model offset and returns a text point
//   inside it. When no segment strictly contains the offset, falls back to the
//   closest preceding (clamped to end) or following (clamped to start) segment.
//
// `document` + `NodeFilter` are injected so the helper is testable in non-DOM
// realms (uses a minimal stub tree walker).

export function createFirstTextPointInElement(options) {
    const opts = options || {};
    if (!opts.document || typeof opts.document.createTreeWalker !== 'function') {
        throw new TypeError(
            'createFirstTextPointInElement requires options.document (with createTreeWalker)');
    }
    if (!opts.NodeFilter
        || typeof opts.NodeFilter.SHOW_TEXT !== 'number'
        || typeof opts.NodeFilter.FILTER_ACCEPT !== 'number'
        || typeof opts.NodeFilter.FILTER_REJECT !== 'number') {
        throw new TypeError(
            'createFirstTextPointInElement requires options.NodeFilter (SHOW_TEXT, FILTER_ACCEPT, FILTER_REJECT)');
    }
    const { document: doc, NodeFilter } = opts;

    return function firstTextPointInElement(element, preferredOffset) {
        if (!element) return null;
        if (element.nodeType === 3) {
            const textLength = element.nodeValue ? element.nodeValue.length : 0;
            return {
                node: element,
                offset: Math.max(0, Math.min(textLength, Number(preferredOffset || 0))),
            };
        }
        const walker = doc.createTreeWalker(element, NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                return node && node.nodeValue !== null
                    ? NodeFilter.FILTER_ACCEPT
                    : NodeFilter.FILTER_REJECT;
            },
        });
        const node = walker.nextNode();
        if (!node) return null;
        const length = node.nodeValue ? node.nodeValue.length : 0;
        return {
            node,
            offset: Math.max(0, Math.min(length, Number(preferredOffset || 0))),
        };
    };
}

export function createProjectedDomTextPointAtBlockOffset(options) {
    const opts = options || {};
    if (typeof opts.firstTextPointInElement !== 'function') {
        throw new TypeError(
            'createProjectedDomTextPointAtBlockOffset requires options.firstTextPointInElement (function)');
    }
    const { firstTextPointInElement } = opts;

    return function projectedDomTextPointAtBlockOffset(block, offset) {
        if (!block || typeof block.querySelectorAll !== 'function') return null;
        const target = Math.max(0, Number(offset || 0));
        const segments = Array.from(block.querySelectorAll(
            '.tm-wysiwyg-layout-segment--projected[data-model-start][data-model-end]'))
            .map(function (segment, index) {
                const start = Math.max(0,
                    Number(segment.getAttribute('data-model-start') || 0) || 0);
                const end = Math.max(start,
                    Number(segment.getAttribute('data-model-end') || start) || start);
                return { element: segment, start, end, index };
            })
            .filter(function (segment) { return segment.end >= segment.start; })
            .sort(function (a, b) {
                return a.start - b.start || a.end - b.end || a.index - b.index;
            });
        if (!segments.length) return null;

        const containing = segments.find(function (segment) {
            return target >= segment.start
                && target <= segment.end
                && segment.end > segment.start;
        });
        if (containing) {
            return firstTextPointInElement(containing.element, target - containing.start);
        }

        let before = null;
        let after = null;
        segments.forEach(function (segment) {
            if (segment.end <= target
                && (!before || segment.end >= before.end)) before = segment;
            if (segment.start >= target
                && (!after || segment.start < after.start)) after = segment;
        });
        if (before) return firstTextPointInElement(before.element, before.end - before.start);
        if (after) return firstTextPointInElement(after.element, 0);
        return null;
    };
}
