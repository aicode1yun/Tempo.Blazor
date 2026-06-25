// Phase R.4.6c — core-engine/edit-table.mjs
// Pure table structure mutators for the model-owned surface: build a table, insert it at
// the caret, add rows/columns. Each cell holds an editable paragraph, so caret navigation
// and typing inside cells reuse the existing edit-model + caret stops (no special path).
//
//   createTableModel(rows, cols, idBase?)            → a { type:'table', content:{ rows } } block
//   firstCellParagraphId(table)                      → the paragraph id to place the caret in
//   insertTableAfterBlock(model, blockId, table, d)  → { ok, structural, tableId }
//   addTableRow(model, tableId, atIndex?, deps)      → { ok, structural }
//   addTableColumn(model, tableId, atIndex?, deps)   → { ok, structural }
//   findTableContaining(model, blockId)              → the table block owning a cell block

import { asArray } from '../core/helpers.mjs';

let cellSeq = 0;
function uniqueCellId(tableId, r, c) { cellSeq += 1; return tableId + '-r' + r + '-c' + c + '-' + cellSeq; }

function makeCell(cellId) {
    return {
        id: cellId,
        type: 'tableCell',
        rowSpan: 1,
        colSpan: 1,
        style: {},
        blocks: [{
            id: cellId + '-p',
            type: 'paragraph',
            content: { type: 'paragraph', runs: [{ id: cellId + '-run', kind: 'text', text: '' }] },
        }],
    };
}

export function createTableModel(rowCount, colCount, idBase) {
    const rows = Math.max(1, Number(rowCount) || 1);
    const cols = Math.max(1, Number(colCount) || 1);
    const id = idBase || ('table-' + (++cellSeq));
    const rowList = [];
    for (let r = 0; r < rows; r++) {
        const cells = [];
        for (let c = 0; c < cols; c++) cells.push(makeCell(id + '-r' + r + '-c' + c));
        rowList.push({ id: id + '-row' + r, cells: cells });
    }
    return { id: id, type: 'table', content: { type: 'table', rows: rowList } };
}

export function firstCellParagraphId(table) {
    const cell = table && table.content && asArray(table.content.rows)[0] && asArray(asArray(table.content.rows)[0].cells)[0];
    const para = cell && asArray(cell.blocks)[0];
    return para ? para.id : null;
}

export function insertTableAfterBlock(model, blockId, table, deps) {
    const container = deps.findBlockContainer(model, blockId);
    if (container && Array.isArray(container.blocks)) {
        container.blocks.splice(container.index + 1, 0, table);
    } else if (model && model.body && Array.isArray(model.body.blocks)) {
        model.body.blocks.push(table);
    } else {
        return { ok: false };
    }
    return { ok: true, structural: true, tableId: table.id };
}

function findTableBlock(model, tableId) {
    const block = asArray(model && model.body && model.body.blocks).find(function (b) { return b.id === tableId && b.type === 'table'; });
    return block || null;
}

export function addTableRow(model, tableId, atIndex, deps) {
    const table = (deps && deps.findBlock ? deps.findBlock(model, tableId) : findTableBlock(model, tableId));
    if (!table || table.type !== 'table') return { ok: false };
    const rows = asArray(table.content && table.content.rows);
    const colCount = rows[0] ? asArray(rows[0].cells).length : 1;
    const at = (atIndex == null) ? rows.length : Math.max(0, Math.min(rows.length, Number(atIndex) || 0));
    const cells = [];
    for (let c = 0; c < colCount; c++) cells.push(makeCell(uniqueCellId(table.id, at, c)));
    rows.splice(at, 0, { id: table.id + '-row-' + (++cellSeq), cells: cells });
    table.content.rows = rows;
    return { ok: true, structural: true };
}

export function addTableColumn(model, tableId, atIndex, deps) {
    const table = (deps && deps.findBlock ? deps.findBlock(model, tableId) : findTableBlock(model, tableId));
    if (!table || table.type !== 'table') return { ok: false };
    asArray(table.content && table.content.rows).forEach(function (row, r) {
        const cells = asArray(row.cells);
        const at = (atIndex == null) ? cells.length : Math.max(0, Math.min(cells.length, Number(atIndex) || 0));
        cells.splice(at, 0, makeCell(uniqueCellId(table.id, r, at)));
        row.cells = cells;
    });
    return { ok: true, structural: true };
}

export function findTableContaining(model, blockId) {
    let result = null;
    asArray(model && model.body && model.body.blocks).forEach(function (b) {
        if (result || b.type !== 'table') return;
        asArray(b.content && b.content.rows).forEach(function (row) {
            asArray(row.cells).forEach(function (cell) {
                if (asArray(cell.blocks).some(function (cb) { return cb.id === blockId; })) result = b;
            });
        });
    });
    return result;
}

// ----- R.5.9 advanced table editing -------------------------------------------------------

export function cellFirstParagraphId(cell) {
    const b = cell && asArray(cell.blocks)[0];
    return b ? b.id : null;
}

// Locates the cell owning `blockId` + its grid position.
export function locateCell(model, blockId) {
    let found = null;
    asArray(model && model.body && model.body.blocks).forEach(function (table) {
        if (found || table.type !== 'table') return;
        asArray(table.content && table.content.rows).forEach(function (row, ri) {
            asArray(row.cells).forEach(function (cell, ci) {
                if (!found && asArray(cell.blocks).some(function (b) { return b.id === blockId; })) {
                    found = { table: table, row: row, rowIndex: ri, cell: cell, cellIndex: ci };
                }
            });
        });
    });
    return found;
}

// Tab navigation: the first paragraph id of the next (dir>0) / previous (dir<0) cell in reading
// order. Returns null when stepping past the last cell (the caller may append a row).
export function adjacentCellParagraphId(model, blockId, dir) {
    const loc = locateCell(model, blockId);
    if (!loc) return null;
    const rows = asArray(loc.table.content.rows);
    let ri = loc.rowIndex, ci = loc.cellIndex;
    if (dir > 0) {
        ci += 1;
        if (ci >= asArray(rows[ri].cells).length) { ri += 1; ci = 0; }
        if (ri >= rows.length) return null;
    } else {
        ci -= 1;
        if (ci < 0) { ri -= 1; if (ri < 0) return null; ci = asArray(rows[ri].cells).length - 1; }
    }
    return cellFirstParagraphId(asArray(rows[ri].cells)[ci]);
}

export function deleteTableRow(model, blockId) {
    const loc = locateCell(model, blockId);
    if (!loc) return { ok: false };
    const rows = asArray(loc.table.content.rows);
    if (rows.length <= 1) return { ok: false, reason: 'last-row' };
    rows.splice(loc.rowIndex, 1);
    const target = rows[Math.min(loc.rowIndex, rows.length - 1)];
    const cells = asArray(target.cells);
    return { ok: true, structural: true, caretBlockId: cellFirstParagraphId(cells[Math.min(loc.cellIndex, cells.length - 1)]) };
}

export function deleteTableColumn(model, blockId) {
    const loc = locateCell(model, blockId);
    if (!loc) return { ok: false };
    const rows = asArray(loc.table.content.rows);
    const colCount = asArray(rows[0] && rows[0].cells).length;
    if (colCount <= 1) return { ok: false, reason: 'last-column' };
    rows.forEach(function (row) { const cells = asArray(row.cells); if (loc.cellIndex < cells.length) cells.splice(loc.cellIndex, 1); });
    const newRow = asArray(rows[loc.rowIndex].cells);
    return { ok: true, structural: true, caretBlockId: cellFirstParagraphId(newRow[Math.min(loc.cellIndex, newRow.length - 1)]) };
}

// Horizontal merge: absorb the cell to the right (colSpan grows — the layout already spans
// columnWidths across colSpan). The right cell's non-empty paragraphs are appended.
export function mergeCellRight(model, blockId) {
    const loc = locateCell(model, blockId);
    if (!loc) return { ok: false };
    const cells = asArray(loc.row.cells);
    const right = cells[loc.cellIndex + 1];
    if (!right) return { ok: false, reason: 'no-right-cell' };
    loc.cell.colSpan = Math.max(1, Number(loc.cell.colSpan || 1)) + Math.max(1, Number(right.colSpan || 1));
    asArray(right.blocks).forEach(function (b) {
        const text = b && b.content && asArray(b.content.runs).map(function (r) { return r.text || ''; }).join('');
        if (text) loc.cell.blocks.push(b);
    });
    cells.splice(loc.cellIndex + 1, 1);
    return { ok: true, structural: true, caretBlockId: cellFirstParagraphId(loc.cell) };
}

// R.5.9 — vertical merge: absorb the cell directly below (rowSpan grows; the layout skips the
// covered grid column in the row below). The below cell's non-empty paragraphs are appended.
export function mergeCellDown(model, blockId) {
    const loc = locateCell(model, blockId);
    if (!loc) return { ok: false };
    const rows = asArray(loc.table.content.rows);
    const belowRow = rows[loc.rowIndex + 1];
    const below = belowRow && asArray(belowRow.cells)[loc.cellIndex];
    if (!below) return { ok: false, reason: 'no-cell-below' };
    loc.cell.rowSpan = Math.max(1, Number(loc.cell.rowSpan || 1)) + Math.max(1, Number(below.rowSpan || 1));
    asArray(below.blocks).forEach(function (b) {
        const text = b && b.content && asArray(b.content.runs).map(function (r) { return r.text || ''; }).join('');
        if (text) loc.cell.blocks.push(b);
    });
    belowRow.cells.splice(loc.cellIndex, 1);
    return { ok: true, structural: true, caretBlockId: cellFirstParagraphId(loc.cell) };
}

// Split a vertically-merged cell back into single rows.
export function splitCellVertical(model, blockId) {
    const loc = locateCell(model, blockId);
    if (!loc) return { ok: false };
    const span = Math.max(1, Number(loc.cell.rowSpan || 1));
    if (span <= 1) return { ok: false, reason: 'not-merged' };
    loc.cell.rowSpan = 1;
    const rows = asArray(loc.table.content.rows);
    for (let i = 1; i < span; i++) {
        const r = rows[loc.rowIndex + i];
        if (r) r.cells.splice(Math.min(loc.cellIndex, asArray(r.cells).length), 0, makeCell(uniqueCellId(loc.table.id, loc.rowIndex + i, loc.cellIndex)));
    }
    return { ok: true, structural: true, caretBlockId: cellFirstParagraphId(loc.cell) };
}

// R.5.9 — the cell ids inside the rectangular range spanned by two cells (cell-range selection).
export function cellRangeIds(model, fromBlockId, toBlockId) {
    const a = locateCell(model, fromBlockId);
    const b = locateCell(model, toBlockId);
    if (!a || !b || a.table !== b.table) return [];
    const r0 = Math.min(a.rowIndex, b.rowIndex), r1 = Math.max(a.rowIndex, b.rowIndex);
    const c0 = Math.min(a.cellIndex, b.cellIndex), c1 = Math.max(a.cellIndex, b.cellIndex);
    const rows = asArray(a.table.content.rows);
    const ids = [];
    for (let ri = r0; ri <= r1; ri++) {
        const cells = asArray(rows[ri] && rows[ri].cells);
        for (let ci = c0; ci <= c1 && ci < cells.length; ci++) ids.push(cells[ci].id);
    }
    return ids;
}

// Resize a table column: set the width on every single-column cell in the column.
export function setColumnWidth(model, tableId, columnIndex, width) {
    const table = asArray(model && model.body && model.body.blocks).find(function (b) { return b.id === tableId && b.type === 'table'; });
    if (!table) return { ok: false };
    const col = Math.max(0, Number(columnIndex) || 0);
    const w = Math.max(20, Number(width) || 0);
    asArray(table.content.rows).forEach(function (row) {
        const cell = asArray(row.cells)[col];
        if (cell && Math.max(1, Number(cell.colSpan || 1)) === 1) cell.width = w;
    });
    return { ok: true, structural: false };
}

// Split a horizontally-merged cell back into single columns.
export function splitCellHorizontal(model, blockId) {
    const loc = locateCell(model, blockId);
    if (!loc) return { ok: false };
    const span = Math.max(1, Number(loc.cell.colSpan || 1));
    if (span <= 1) return { ok: false, reason: 'not-merged' };
    loc.cell.colSpan = 1;
    const cells = asArray(loc.row.cells);
    const extra = [];
    for (let i = 1; i < span; i++) extra.push(makeCell(uniqueCellId(loc.table.id, loc.rowIndex, loc.cellIndex + i)));
    Array.prototype.splice.apply(cells, [loc.cellIndex + 1, 0].concat(extra));
    return { ok: true, structural: true, caretBlockId: cellFirstParagraphId(loc.cell) };
}
