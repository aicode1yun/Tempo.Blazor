// Phase D — render/dom-selection.mjs
// Tests against the live DOM `Selection`/`Range` for editor ownership and surface
// classification. Pure of closure state — takes the editor instance (with `.root`)
// and a Selection-like object and reports whether the selection is editable or
// targets a text surface.

const ELEMENT_NODE = typeof Node !== 'undefined' ? Node.ELEMENT_NODE : 1;

function nodeOrParentElement(node) {
    if (!node) return null;
    return node.nodeType === ELEMENT_NODE ? node : node.parentElement;
}

export function selectionBelongsToEditor(inst, selection) {
    if (!inst || !inst.root || !selection || selection.rangeCount === 0) return false;
    const range = selection.getRangeAt(0);
    const start = nodeOrParentElement(range.startContainer);
    const end = nodeOrParentElement(range.endContainer);
    return !!(start && end
        && typeof inst.root.contains === 'function'
        && inst.root.contains(start) && inst.root.contains(end));
}

const NON_TEXT_SURFACE_SELECTOR = '.tm-wysiwyg-image, figure, [data-object-id], '
    + '.tm-wysiwyg-layout-object, .tm-wysiwyg-drawing-anchor';
const TEXT_SURFACE_SELECTOR = '.tm-wysiwyg-page__body[contenteditable], '
    + '.tm-wysiwyg-page__header[contenteditable], '
    + '.tm-wysiwyg-page__footer[contenteditable], '
    + '.tm-wysiwyg-table-cell, .tm-wysiwyg-block[data-block-id]';

export function selectionTargetsTextSurface(inst, selection) {
    if (!selectionBelongsToEditor(inst, selection) || selection.isCollapsed) return false;
    const range = selection.getRangeAt(0);
    const common = nodeOrParentElement(range.commonAncestorContainer);
    if (!common || typeof common.closest !== 'function') return false;
    if (common.closest(NON_TEXT_SURFACE_SELECTOR)) return false;
    return !!common.closest(TEXT_SURFACE_SELECTOR);
}
