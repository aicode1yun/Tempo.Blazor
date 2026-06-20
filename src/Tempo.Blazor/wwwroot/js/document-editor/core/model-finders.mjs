// Phase D — core/model-finders.mjs
// Tree walkers that locate blocks, cells, and tables in a document model. All pure
// (no closure over engine state, no mutation of the model). Mirrors the inline
// `_findBlockContainer` / `_findCell` / `_findTableInfo*` family from the legacy IIFE.

import { asArray, asText } from './helpers.mjs';

// Returns { blocks, index, block } for the parent array that contains the block with the
// given id (including nested table cells). Returns null when not found.
export function findBlockContainer(model, blockId) {
    function scan(blocks) {
        const list = asArray(blocks);
        for (let i = 0; i < list.length; i++) {
            const block = list[i];
            if (block && block.id === blockId) return { blocks: list, index: i, block };
            if (block && block.type === 'table') {
                const rows = asArray(block.content && block.content.rows);
                for (const row of rows) {
                    const cells = asArray(row.cells);
                    for (const cell of cells) {
                        const nested = scan(cell.blocks);
                        if (nested) return nested;
                    }
                }
            }
        }
        return null;
    }
    let found = scan(model && model.body && model.body.blocks);
    if (found) return found;
    for (const header of asArray(model && model.headers)) {
        found = scan(header.blocks);
        if (found) return found;
    }
    for (const footer of asArray(model && model.footers)) {
        found = scan(footer.blocks);
        if (found) return found;
    }
    return null;
}

// Returns the table cell with the given id, scanning body + headers + footers and nested
// tables. Returns null when not found.
export function findCell(model, cellId) {
    let found = null;
    function scan(blocks) {
        for (const block of asArray(blocks)) {
            if (!block || block.type !== 'table') continue;
            for (const row of asArray(block.content && block.content.rows)) {
                for (const cell of asArray(row.cells)) {
                    if (cell.id === cellId) found = cell;
                    scan(cell.blocks);
                }
            }
        }
    }
    scan(model && model.body && model.body.blocks);
    for (const region of asArray(model && model.headers)) scan(region.blocks);
    for (const region of asArray(model && model.footers)) scan(region.blocks);
    return found;
}

// Generic table-info finder. `predicate(table, row, cell, rowIndex, columnIndex)` returns
// true when the cell of interest is reached. Returns { table, row, cell, rowIndex, columnIndex }
// or null. Stops on the first match.
export function findTableInfo(model, predicate) {
    let found = null;
    function scan(blocks) {
        for (const block of asArray(blocks)) {
            if (found) return;
            if (!block || block.type !== 'table') continue;
            const rows = asArray(block.content && block.content.rows);
            for (let r = 0; r < rows.length; r++) {
                const cells = asArray(rows[r].cells);
                for (let c = 0; c < cells.length; c++) {
                    const cell = cells[c];
                    if (predicate(block, rows[r], cell, r, c)) {
                        found = { table: block, row: rows[r], cell, rowIndex: r, columnIndex: c };
                        return;
                    }
                    scan(cell.blocks);
                    if (found) return;
                }
            }
        }
    }
    scan(model && model.body && model.body.blocks);
    for (const region of asArray(model && model.headers)) scan(region.blocks);
    for (const region of asArray(model && model.footers)) scan(region.blocks);
    return found;
}

export function findTableInfoByCellId(model, cellId) {
    return findTableInfo(model, (_table, _row, cell) => cell.id === cellId);
}

export function findTableInfoByBlockId(model, blockId) {
    return findTableInfo(model, (_table, _row, cell) =>
        asArray(cell.blocks).some(block => block && block.id === blockId));
}

// Returns the table block with the given id by scanning the document tree (without the
// `model.indexes` cache that the legacy `_findBlock` shortcuts through — this keeps the
// helper closure-free; callers that want index caching can layer it on top).
export function findTableBlockByScan(model, tableId) {
    const id = asText(tableId);
    if (!model || !id) return null;
    const container = findBlockContainer(model, id);
    return container && container.block && container.block.type === 'table' ? container.block : null;
}
