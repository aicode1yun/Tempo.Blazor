// Phase D — render/dom-text-mapping.mjs
// DOM ↔ logical-offset mapping helpers used by the WYSIWYG selection bridge.
//
// `isInlineBreakNode(node)` — true when `node` is a `<br data-inline-break>`,
//   the marker we render between paragraph runs to force a soft break.
// `isCaretPlaceholderNode(node)` — true when `node` is a `<br data-caret-placeholder>`,
//   inserted into empty paragraphs so the caret has somewhere to land.
// `domLogicalLength(node)` — counts model characters covered by `node` and its
//   descendants. Text nodes contribute their `nodeValue.length`; inline-break
//   markers count as 1; caret placeholders count as 0.
// `domBoundaryLogicalOffset(root, node, offset)` — walks from `root` to `node`,
//   summing `domLogicalLength` of preceding siblings until it reaches `node`,
//   then adds the within-node `offset`. Clamps text-node offsets to
//   `nodeValue.length` and element offsets to `childNodes.length`.
// `createFindTextNodeFactory({document, NodeFilter})` → `findTextNode(root)` —
//   tree walker that yields the first non-null text node under `root`.

export function isInlineBreakNode(node) {
    return !!(node
        && node.nodeType === 1
        && String(node.tagName || '').toLowerCase() === 'br'
        && node.getAttribute
        && node.getAttribute('data-inline-break') !== null);
}

export function isCaretPlaceholderNode(node) {
    return !!(node
        && node.nodeType === 1
        && String(node.tagName || '').toLowerCase() === 'br'
        && node.getAttribute
        && node.getAttribute('data-caret-placeholder') !== null);
}

export function domLogicalLength(node) {
    if (!node) return 0;
    if (node.nodeType === 3) return node.nodeValue ? node.nodeValue.length : 0;
    if (isInlineBreakNode(node)) return 1;
    if (isCaretPlaceholderNode(node)) return 0;
    let total = 0;
    const children = node.childNodes || [];
    for (let i = 0; i < children.length; i++) {
        total += domLogicalLength(children[i]);
    }
    return total;
}

export function domBoundaryLogicalOffset(root, node, offset) {
    if (!root || !node) return 0;
    if (root === node) {
        if (node.nodeType === 3) {
            return Math.max(0, Math.min(
                node.nodeValue ? node.nodeValue.length : 0,
                Number(offset || 0)));
        }
        const ownChildren = node.childNodes || [];
        const childLimit = Math.max(0, Math.min(ownChildren.length, Number(offset || 0)));
        let ownTotal = 0;
        for (let ownIndex = 0; ownIndex < childLimit; ownIndex++) {
            ownTotal += domLogicalLength(ownChildren[ownIndex]);
        }
        return ownTotal;
    }

    const children = root.childNodes || [];
    let total = 0;
    for (let index = 0; index < children.length; index++) {
        const child = children[index];
        if (child === node || (child.contains && child.contains(node))) {
            return total + domBoundaryLogicalOffset(child, node, offset);
        }
        total += domLogicalLength(child);
    }
    return total;
}

export function createFindTextNodeFactory(options) {
    const opts = options || {};
    if (!opts.document || typeof opts.document.createTreeWalker !== 'function') {
        throw new TypeError(
            'createFindTextNodeFactory requires options.document (with createTreeWalker)');
    }
    if (!opts.NodeFilter
        || typeof opts.NodeFilter.SHOW_TEXT !== 'number'
        || typeof opts.NodeFilter.FILTER_ACCEPT !== 'number'
        || typeof opts.NodeFilter.FILTER_REJECT !== 'number') {
        throw new TypeError(
            'createFindTextNodeFactory requires options.NodeFilter (with SHOW_TEXT, FILTER_ACCEPT, FILTER_REJECT)');
    }
    const { document: doc, NodeFilter } = opts;
    return function findTextNode(root) {
        const walker = doc.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                return node.nodeValue !== null
                    ? NodeFilter.FILTER_ACCEPT
                    : NodeFilter.FILTER_REJECT;
            },
        });
        return walker.nextNode();
    };
}
