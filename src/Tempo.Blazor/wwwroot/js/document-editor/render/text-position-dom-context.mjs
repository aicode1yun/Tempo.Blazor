// Phase D — render/text-position-dom-context.mjs
// `createReadTextPositionDomContext({normalizeDropRegionName,
//   normalizeTextExclusionColumnIndex, anchorRegionForNearestTextPosition})` →
//   `readTextPositionDomContext(root, blockElement)` — extracts the region/scope
//   metadata for a hit-tested text position by walking the DOM ancestor chain of
//   the block element: which `data-render-region` it sits in, which table cell/
//   table/page (when present). Returns canonical `{region, anchorRegion,
//   headerFooterId, tableId, cellId, columnIndex, pageIndex}`.

export function createReadTextPositionDomContext(options) {
    const opts = options || {};
    for (const key of ['normalizeDropRegionName', 'normalizeTextExclusionColumnIndex',
        'anchorRegionForNearestTextPosition']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createReadTextPositionDomContext requires options.${key} (function)`);
        }
    }
    const {
        normalizeDropRegionName,
        normalizeTextExclusionColumnIndex,
        anchorRegionForNearestTextPosition,
    } = opts;

    return function readTextPositionDomContext(root, blockElement) {
        const regionNode = blockElement && blockElement.closest
            && blockElement.closest('[data-render-region]');
        const cell = blockElement && blockElement.closest
            && blockElement.closest('td[data-cell-id], [data-table-cell-id], [data-cell-id]');
        const table = cell && cell.closest
            && cell.closest('table[data-block-id], .tm-wysiwyg-table[data-block-id], .tm-wysiwyg-block[data-block-id]');
        const page = blockElement && blockElement.closest
            && blockElement.closest('.tm-wysiwyg-page[data-page-index]');
        const region = normalizeDropRegionName(
            regionNode && regionNode.getAttribute('data-render-region'),
            cell && (cell.getAttribute('data-cell-id') || cell.getAttribute('data-table-cell-id')));
        const columnIndex = normalizeTextExclusionColumnIndex(
            cell && (cell.getAttribute('data-column-index')
                || cell.getAttribute('data-table-column-index')));
        return {
            region,
            anchorRegion: anchorRegionForNearestTextPosition({
                region,
                cellId: (cell && (cell.getAttribute('data-cell-id')
                    || cell.getAttribute('data-table-cell-id'))) || null,
            }),
            headerFooterId: (regionNode && regionNode.getAttribute('data-hf-id')) || '',
            tableId: (table && table.getAttribute('data-block-id')) || '',
            cellId: (cell && (cell.getAttribute('data-cell-id')
                || cell.getAttribute('data-table-cell-id'))) || '',
            columnIndex,
            pageIndex: page ? Number(page.getAttribute('data-page-index') || 0) || 0 : null,
        };
    };
}
