// Phase D — objects/table-controller.mjs
// `createTableControllerFactory({ findBlock, buildIndexes, createOperation,
//   pointerHitTest })` → `createTableController(model, options)` → table command API
//   (insertRow/Column, delete, merge/split cells, cell style, resize, insert text in
//   cell, hit-test, context menu). Each mutating command edits the model in place,
//   records an UpdateTableCell operation, rebuilds indexes, and returns the next
//   selection. Engine-state deps are injected; everything else is a pure import.

import { asArray, asText, sortObject } from '../core/helpers.mjs';
import { tableColumnCount } from '../core/text-helpers.mjs';
import { findTableInfoByCellId, findTableInfoByBlockId } from '../core/model-finders.mjs';
import { createSelectionSnapshot } from '../core/selection-snapshot.mjs';
import { OperationTypes } from '../history/operation-types.mjs';
import { insertTextRun } from '../core/insert-text-run.mjs';
import { blockText } from '../core/text-helpers.mjs';
import { importBlock } from '../core/block-import.mjs';

export function createTableControllerFactory(options) {
    const opts = options || {};
    for (const key of ['findBlock', 'buildIndexes', 'createOperation', 'pointerHitTest']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createTableControllerFactory requires options.${key} (function)`);
        }
    }
    const { findBlock, buildIndexes, createOperation, pointerHitTest } = opts;

    function createEmptyTableCell(tableId, rowIndex, columnIndex) {
        const cellId = tableId + '-r' + rowIndex + '-c' + columnIndex;
        return {
            id: cellId,
            type: 'tableCell',
            rowSpan: 1,
            colSpan: 1,
            width: null,
            height: null,
            style: {},
            blocks: [importBlock({
                Id: cellId + '-p',
                Type: 'Paragraph',
                Content: { Inlines: [{ Id: cellId + '-r', Text: '' }] },
            }, cellId + '-block')],
        };
    }

    function findTableBlock(model, tableId) {
        const block = findBlock(model, tableId);
        return block && block.type === 'table' ? block : null;
    }

    return function createTableController(model) {
        const committedOperations = [];

        function tableInfoFromSelection(selection) {
            const snapshot = createSelectionSnapshot(selection || {});
            return snapshot.cellId
                ? findTableInfoByCellId(model, snapshot.cellId)
                : findTableInfoByBlockId(model, snapshot.blockId);
        }

        function ensureRowsAndCells(table) {
            const columnCount = tableColumnCount(table);
            asArray(table.content && table.content.rows).forEach(function (row, rowIndex) {
                if (!Array.isArray(row.cells)) row.cells = [];
                while (row.cells.length < columnCount) {
                    row.cells.push(createEmptyTableCell(table.id, rowIndex, row.cells.length));
                }
                row.cells.forEach(function (cell, columnIndex) {
                    if (!cell.id) cell.id = table.id + '-r' + rowIndex + '-c' + columnIndex;
                    cell.type = 'tableCell';
                    cell.rowSpan = Math.max(1, Number(cell.rowSpan || 1));
                    cell.colSpan = Math.max(1, Number(cell.colSpan || 1));
                    if (!cell.style) cell.style = {};
                    if (!Array.isArray(cell.blocks) || cell.blocks.length === 0) {
                        cell.blocks = [createEmptyTableCell(table.id, rowIndex, columnIndex).blocks[0]];
                    }
                });
            });
            buildIndexes(model);
        }

        function record(type, payload) {
            const op = createOperation(type || OperationTypes.UpdateTableCell, payload || {}, { source: 'table-command' });
            committedOperations.push(op.toJSON());
            buildIndexes(model);
            return op;
        }

        function insertRow(selection, where) {
            const info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            const table = info.table;
            const columnCount = tableColumnCount(table);
            const insertIndex = where === 'above' ? info.rowIndex : info.rowIndex + 1;
            const row = { id: table.id + '-row-' + Date.now() + '-' + insertIndex, type: 'tableRow', cells: [] };
            for (let c = 0; c < columnCount; c++) row.cells.push(createEmptyTableCell(table.id, insertIndex, c));
            table.content.rows.splice(insertIndex, 0, row);
            ensureRowsAndCells(table);
            const op = record(OperationTypes.UpdateTableCell, { tableId: table.id, action: where === 'above' ? 'insert-row-above' : 'insert-row-below' });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: row.cells[0].blocks[0].id, cellId: row.cells[0].id, tableId: table.id, offset: 0, isCollapsed: true }) };
        }

        function insertColumn(selection, where) {
            const info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            const table = info.table;
            const insertIndex = where === 'left' ? info.columnIndex : info.columnIndex + 1;
            asArray(table.content.rows).forEach(function (row, rowIndex) {
                row.cells.splice(insertIndex, 0, createEmptyTableCell(table.id, rowIndex, insertIndex));
            });
            ensureRowsAndCells(table);
            const target = table.content.rows[info.rowIndex].cells[insertIndex];
            const op = record(OperationTypes.UpdateTableCell, { tableId: table.id, action: where === 'left' ? 'insert-column-left' : 'insert-column-right' });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: target.blocks[0].id, cellId: target.id, tableId: table.id, offset: 0, isCollapsed: true }) };
        }

        function deleteRow(selection, rowIndex) {
            const info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            const table = info.table;
            const index = Math.max(0, Math.min(asArray(table.content.rows).length - 1, Number(rowIndex ?? info.rowIndex)));
            if (asArray(table.content.rows).length > 1) table.content.rows.splice(index, 1);
            ensureRowsAndCells(table);
            const fallback = table.content.rows[Math.min(index, table.content.rows.length - 1)].cells[0];
            const op = record(OperationTypes.UpdateTableCell, { tableId: table.id, action: 'delete-row' });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: fallback.blocks[0].id, cellId: fallback.id, tableId: table.id, offset: 0, isCollapsed: true }) };
        }

        function deleteColumn(selection, columnIndex) {
            const info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            const table = info.table;
            const index = Math.max(0, Math.min(tableColumnCount(table) - 1, Number(columnIndex ?? info.columnIndex)));
            asArray(table.content.rows).forEach(function (row) {
                if (row.cells.length > 1) row.cells.splice(index, 1);
            });
            ensureRowsAndCells(table);
            const fallback = table.content.rows[0].cells[Math.min(index, table.content.rows[0].cells.length - 1)];
            const op = record(OperationTypes.UpdateTableCell, { tableId: table.id, action: 'delete-column' });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: fallback.blocks[0].id, cellId: fallback.id, tableId: table.id, offset: 0, isCollapsed: true }) };
        }

        function mergeCells(selection, cellIds) {
            const info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            const ids = asArray(cellIds).length ? asArray(cellIds) : [info.cell.id];
            const cells = ids.map(function (id) { return findTableInfoByCellId(model, id); }).filter(Boolean);
            if (cells.length < 2) return { ok: true, operation: record(OperationTypes.UpdateTableCell, { tableId: info.table.id, action: 'merge-cells-noop' }), selection: createSelectionSnapshot(selection || {}) };
            const first = cells[0].cell;
            first.colSpan = cells.length;
            cells.slice(1).forEach(function (cellInfo) {
                cellInfo.row.cells = cellInfo.row.cells.filter(function (cell) { return cell.id !== cellInfo.cell.id; });
            });
            ensureRowsAndCells(info.table);
            const op = record(OperationTypes.UpdateTableCell, { tableId: info.table.id, action: 'merge-cells', cellIds: ids });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: first.blocks[0].id, cellId: first.id, tableId: info.table.id, offset: 0, isCollapsed: true }) };
        }

        function splitCell(selection, cellId) {
            const info = cellId ? findTableInfoByCellId(model, cellId) : tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            const span = Math.max(1, Number(info.cell.colSpan || 1));
            info.cell.colSpan = 1;
            for (let i = 1; i < span; i++) {
                info.row.cells.splice(info.columnIndex + i, 0, createEmptyTableCell(info.table.id, info.rowIndex, info.columnIndex + i));
            }
            ensureRowsAndCells(info.table);
            const op = record(OperationTypes.UpdateTableCell, { tableId: info.table.id, action: 'split-cell', cellId: info.cell.id });
            return { ok: true, operation: op, selection: createSelectionSnapshot({ blockId: info.cell.blocks[0].id, cellId: info.cell.id, tableId: info.table.id, offset: 0, isCollapsed: true }) };
        }

        function setCellStyle(selection, style) {
            const info = tableInfoFromSelection(selection);
            if (!info) return { ok: false, error: { code: 'missing-table-selection' } };
            info.cell.style = Object.assign({}, info.cell.style || {}, style || {});
            const op = record(OperationTypes.UpdateTableCell, { tableId: info.table.id, cellId: info.cell.id, action: 'cell-style', style: info.cell.style });
            return { ok: true, operation: op, selection: createSelectionSnapshot(selection || { blockId: info.cell.blocks[0].id, cellId: info.cell.id, tableId: info.table.id }) };
        }

        function resizeTable(tableId, width) {
            const table = findTableBlock(model, tableId);
            if (!table) return { ok: false, error: { code: 'missing-table', tableId } };
            if (!table.content.style) table.content.style = {};
            table.content.style.width = Math.max(80, Number(width || 0) || Number(table.content.style.width || 320));
            const op = record(OperationTypes.UpdateTableCell, { tableId: table.id, action: 'resize-table', width: table.content.style.width });
            return { ok: true, operation: op, tableId: table.id, width: table.content.style.width };
        }

        function insertTextInCell(selection, text) {
            const snapshot = createSelectionSnapshot(selection || {});
            const block = findBlock(model, snapshot.blockId);
            if (!block || block.type !== 'paragraph') return { ok: false, error: { code: 'missing-cell-paragraph' }, selection: snapshot };
            insertTextRun(block, Math.max(0, Math.min(blockText(block).length, snapshot.offset || blockText(block).length)), asText(text), {});
            const next = createSelectionSnapshot(Object.assign({}, snapshot, {
                offset: Math.max(0, Math.min(blockText(block).length, Number(snapshot.offset || 0) + asText(text).length)),
                isCollapsed: true,
            }));
            record(OperationTypes.UpdateTableCell, { cellId: snapshot.cellId, action: 'insert-text' });
            return { ok: true, selection: next };
        }

        function hitTest(layout, x, y) {
            const hit = pointerHitTest(model, layout, x, y);
            if (hit.type !== 'tableCell') return hit;
            return Object.assign({}, hit, { selection: createSelectionSnapshot(Object.assign({}, hit.position, { cellId: hit.cellId, tableId: hit.tableId, isCellSelection: true })) });
        }

        function createContextMenu(selection, contextOptions) {
            const viewport = (contextOptions && contextOptions.viewport) || {};
            return sortObject({
                isReadable: true,
                position: { x: Math.min(Number(viewport.width || 1280) - 240, 24), y: 24 },
                items: ['insertRowAbove', 'insertRowBelow', 'insertColumnLeft', 'insertColumnRight', 'deleteRow', 'deleteColumn', 'mergeCells', 'splitCell', 'cellBackground', 'cellBorder', 'resizeTable'].map(function (id) {
                    return { commandId: id, isEnabled: !!tableInfoFromSelection(selection) || id === 'resizeTable' };
                }),
            });
        }

        return {
            insertRowAbove: function (selection) { return insertRow(selection, 'above'); },
            insertRowBelow: function (selection) { return insertRow(selection, 'below'); },
            insertColumnLeft: function (selection) { return insertColumn(selection, 'left'); },
            insertColumnRight: function (selection) { return insertColumn(selection, 'right'); },
            deleteRow,
            deleteColumn,
            mergeCells,
            splitCell,
            setCellBackground: function (selection, color) { return setCellStyle(selection, { background: color }); },
            setCellBorder: function (selection, border) { return setCellStyle(selection, { border }); },
            resizeTable,
            insertTextInCell,
            hitTest,
            createContextMenu,
            getCommittedOperations: function () { return committedOperations.slice(); },
        };
    };
}
