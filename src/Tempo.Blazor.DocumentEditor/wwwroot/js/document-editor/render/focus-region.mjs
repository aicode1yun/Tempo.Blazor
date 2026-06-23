// Phase D — render/focus-region.mjs
// DOM-walking helpers that resolve the focus region (Body/Header/Footer/TableCell/Image)
// and the active block/cell/comment/revision/object ids from a DOM hit target.
// Works against any standard DOM (browser, jsdom, headless stubs that expose `closest`).

export function isElementNode(value) {
    const elementNode = typeof Node !== 'undefined' ? Node.ELEMENT_NODE : 1;
    return value && value.nodeType === elementNode;
}

export function getFocusRegionFromElement(root, element) {
    const node = isElementNode(element) ? element : element && element.parentElement;
    if (!node || root && !root.contains(node)) return 'Body';
    if (node.closest && node.closest(
        'figure.tm-wysiwyg-image, .tm-render-image-widget, '
        + '.tm-wysiwyg-inline-drawing[data-object-id], '
        + '.tm-wysiwyg-object-layer-item[data-object-id], '
        + '.tm-wysiwyg-object-selection-overlay[data-object-id], '
        + '.tm-wysiwyg-object-guides-overlay[data-object-id]')) return 'Image';
    if (node.closest && node.closest('td[data-cell-id], [data-table-cell-id], [data-cell-id]')) {
        return 'TableCell';
    }
    const explicit = node.closest && node.closest('[data-render-region]');
    if (explicit) return explicit.getAttribute('data-render-region') || 'Body';
    if (node.closest && node.closest(
        '.tm-render-header-region, [data-render-frame="header"], '
        + '[data-render-frame="header-content"]')) return 'Header';
    if (node.closest && node.closest(
        '.tm-render-footer-region, [data-render-frame="footer"], '
        + '[data-render-frame="footer-content"]')) return 'Footer';
    return 'Body';
}

export function getFocusTargetDetails(root, element, region) {
    const node = isElementNode(element) ? element : element && element.parentElement;
    const details = {
        region: region || 'Body',
        headerFooterId: '',
        activeTableCellId: '',
        activeTableId: '',
        activeImageBlockId: '',
        activeCommentId: '',
        activeRevisionId: '',
        activeObjectId: '',
        textBlockId: '',
        hitTargetKind: String(region || 'Body').toLowerCase(),
    };
    if (!node || root && !root.contains(node)) return details;
    const regionNode = node.closest && node.closest('[data-hf-id]');
    details.headerFooterId = regionNode && regionNode.getAttribute('data-hf-id') || '';
    const cell = node.closest && node.closest(
        'td[data-cell-id], [data-table-cell-id], [data-cell-id]');
    details.activeTableCellId = cell
        && (cell.getAttribute('data-cell-id') || cell.getAttribute('data-table-cell-id')) || '';
    const table = cell && cell.closest && cell.closest(
        'table[data-block-id], .tm-wysiwyg-block[data-block-id]');
    details.activeTableId = table && table.getAttribute('data-block-id') || '';
    const comment = node.closest && node.closest(
        '.tm-document-inline--comment-anchor[data-comment-id], '
        + '[data-testid="document-comment-marker"][data-comment-id]');
    details.activeCommentId = comment && comment.getAttribute('data-comment-id') || '';
    const revision = node.closest && node.closest(
        '.tm-wysiwyg-revision[data-revision-id], '
        + '.tm-document-inline--revision[data-revision-id], '
        + '[data-testid="document-revision-marker"][data-revision-id]');
    details.activeRevisionId = revision && revision.getAttribute('data-revision-id') || '';
    const image = node.closest && node.closest(
        'figure.tm-wysiwyg-image, .tm-render-image-widget, '
        + '.tm-wysiwyg-inline-drawing[data-object-id], '
        + '.tm-wysiwyg-object-layer-item[data-object-id], '
        + '.tm-wysiwyg-object-selection-overlay[data-object-id], '
        + '.tm-wysiwyg-object-guides-overlay[data-object-id]');
    details.activeImageBlockId = image
        && (image.getAttribute('data-block-id')
            || image.getAttribute('data-render-block-id')
            || image.getAttribute('data-model-id')) || '';
    details.activeObjectId = image
        && (image.getAttribute('data-render-object-id')
            || image.getAttribute('data-object-id')
            || details.activeImageBlockId) || '';
    const textBlock = !details.activeImageBlockId
        && node.closest && node.closest('.tm-wysiwyg-block[data-block-id]');
    details.textBlockId = textBlock && textBlock.getAttribute('data-block-id') || '';
    if (details.textBlockId) {
        details.hitTargetKind = details.activeTableCellId ? 'tableCell' : 'text';
    }
    if (details.activeCommentId) details.hitTargetKind = 'comment';
    if (details.activeRevisionId) details.hitTargetKind = 'revision';
    return details;
}
