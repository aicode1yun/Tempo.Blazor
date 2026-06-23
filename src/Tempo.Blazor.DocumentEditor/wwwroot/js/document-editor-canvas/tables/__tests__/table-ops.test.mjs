import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { applyTableCommand } from '../table-ops.mjs';

test('table commands insert and delete rows and columns with table-cell selection', () => {
    const model = createTableModel();
    const selection = collapsed('cell-11-p', 0);

    const insertedRow = applyTableCommand(model, selection, 'addTableRow');
    assert.equal(insertedRow.changed, true);
    assert.equal(insertedRow.model.body.blocks[0].content.table.rows.length, 3);
    assert.equal(insertedRow.selection.focus.blockId.startsWith('table-ops-r2'), true);

    const insertedColumn = applyTableCommand(insertedRow.model, insertedRow.selection, 'addTableColumn');
    assert.equal(insertedColumn.changed, true);
    assert.equal(insertedColumn.model.body.blocks[0].content.table.rows[0].cells.length, 3);

    const deletedColumn = applyTableCommand(insertedColumn.model, insertedColumn.selection, 'deleteTableColumn');
    assert.equal(deletedColumn.changed, true);
    assert.equal(deletedColumn.model.body.blocks[0].content.table.rows[0].cells.length, 2);

    const deletedRow = applyTableCommand(deletedColumn.model, deletedColumn.selection, 'deleteTableRow');
    assert.equal(deletedRow.changed, true);
    assert.equal(deletedRow.model.body.blocks[0].content.table.rows.length, 2);
});

test('table commands merge, split, resize and format cells through undoable runtime transactions', () => {
    let model = createTableModel();
    let selection = { anchor: { blockId: 'cell-11-p', offset: 0 }, focus: { blockId: 'cell-22-p', offset: 1 } };
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    assert.equal(runtime.execCommand('mergeCells').result.changed, true);
    assert.equal(model.body.blocks[0].content.table.rows[0].cells[0].columnSpan, 2);
    assert.equal(model.body.blocks[0].content.table.rows[0].cells[0].rowSpan, 2);

    assert.equal(runtime.execCommand('splitCell').result.changed, true);
    assert.equal(model.body.blocks[0].content.table.rows[0].cells[0].columnSpan, 1);

    assert.equal(runtime.execCommand('resizeColumn', { width: 168 }).result.changed, true);
    assert.equal(model.body.blocks[0].content.table.rows[0].cells[0].width, 168);

    assert.equal(runtime.execCommand('setCellFormat', { backgroundColor: '#e0f2fe', verticalAlignment: 'middle', alignment: 'center' }).result.changed, true);
    assert.equal(model.body.blocks[0].content.table.rows[0].cells[0].backgroundColor, '#e0f2fe');
    assert.equal(model.body.blocks[0].content.table.rows[0].cells[0].verticalAlignment, 1);
    assert.equal(model.body.blocks[0].content.table.rows[0].cells[0].blocks[0].paragraphProperties.alignment, 1);

    assert.equal(runtime.execCommand('undo').result.changed, true);
    assert.notEqual(model.body.blocks[0].content.table.rows[0].cells[0].backgroundColor, '#e0f2fe');
    assert.equal(runtime.execCommand('redo').result.changed, true);
    assert.equal(model.body.blocks[0].content.table.rows[0].cells[0].backgroundColor, '#e0f2fe');
});

test('navigate table cell moves selection without changing model content', () => {
    const before = createTableModel();
    const result = applyTableCommand(before, collapsed('cell-11-p', 0), 'navigateTableCell', { direction: 'next' });

    assert.equal(result.changed, false);
    assert.equal(result.selection.focus.blockId, 'cell-12-p');
    assert.deepEqual(before, createTableModel());
});

test('Tab from the last table cell appends a row and moves to the new first cell', () => {
    const before = createTableModel();
    const result = applyTableCommand(before, collapsed('cell-22-p', 1), 'navigateTableCell', { direction: 'next' });

    assert.equal(result.changed, true);
    assert.equal(result.model.body.blocks[0].content.table.rows.length, 3);
    assert.match(result.selection.focus.blockId, /^table-ops-r3c1/);
});

test('cell formatting applies to the selected table cell range', () => {
    const model = createTableModel();
    const selection = { anchor: { blockId: 'cell-11-p', offset: 0 }, focus: { blockId: 'cell-22-p', offset: 1 } };
    const result = applyTableCommand(model, selection, 'setCellFormat', { backgroundColor: '#dcfce7', verticalAlignment: 'bottom' });
    const cells = result.model.body.blocks[0].content.table.rows.flatMap(row => row.cells);

    assert.equal(result.changed, true);
    assert.equal(cells.every(cell => cell.backgroundColor === '#dcfce7'), true);
    assert.equal(cells.every(cell => cell.verticalAlignment === 2), true);
});

test('advanced table commands sort rows, calculate formulas and update margins and borders', () => {
    let model = createNumericTableModel();
    let selection = collapsed('total-p', 0);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    assert.equal(runtime.execCommand('sortTable', { columnIndex: 1, direction: 'descending' }).result.changed, true);
    assert.equal(model.body.blocks[0].content.table.rows[1].cells[0].blocks[0].content.runs[0].text, 'B');

    assert.equal(runtime.execCommand('setTableFormula', { formula: 'SUM', columnIndex: 1 }).result.changed, true);
    assert.equal(model.body.blocks[0].content.table.rows[3].cells[1].blocks[0].content.runs[0].text, '12');

    assert.equal(runtime.execCommand('setCellMargins', { padding: 14 }).result.changed, true);
    assert.equal(model.body.blocks[0].content.table.rows[3].cells[1].padding, 14);

    assert.equal(runtime.execCommand('setCellBorders', { top: '#0f766e', bottom: '#0f766e' }).result.changed, true);
    assert.equal(model.body.blocks[0].content.table.rows[3].cells[1].borders.top, '#0f766e');

    assert.equal(runtime.execCommand('undo').result.changed, true);
    assert.notEqual(model.body.blocks[0].content.table.rows[3].cells[1].borders?.top, '#0f766e');
});

test('table conversion commands round-trip text and table structures', () => {
    let model = {
        version: 1,
        body: {
            blocks: [{
                id: 'csv-p',
                type: 'paragraph',
                order: 1,
                paragraphProperties: {},
                content: { type: 'paragraph', runs: [{ id: 'csv-run', type: 'text', text: 'Name,Value\nA,1\nB,2', marks: [] }] },
            }],
        },
    };
    let selection = collapsed('csv-p', 0);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    assert.equal(runtime.execCommand('convertTextToTable', { delimiter: ',' }).result.changed, true);
    assert.equal(model.body.blocks[0].type, 'table');
    selection = collapsed(model.body.blocks[0].content.table.rows[0].cells[0].blocks[0].id, 0);
    assert.equal(runtime.execCommand('convertTableToText', { delimiter: ',' }).result.changed, true);
    assert.equal(model.body.blocks[0].type, 'paragraph');
    assert.match(model.body.blocks[0].content.runs[0].text, /Name,Value/);
});

function createTableModel() {
    return {
        documentId: 'phase-14-table-ops-test',
        version: 1,
        body: {
            blocks: [{
                id: 'table-ops',
                type: 'table',
                order: 1,
                content: {
                    type: 'table',
                    table: {
                        layout: { cellPadding: 8 },
                        rows: [
                            { id: 'row-1', cells: [cell('cell-11', 'A'), cell('cell-12', 'B')] },
                            { id: 'row-2', cells: [cell('cell-21', 'C'), cell('cell-22', 'D')] },
                        ],
                    },
                },
            }],
        },
    };
}

function createNumericTableModel() {
    return {
        documentId: 'phase-e12-table-ops-test',
        version: 1,
        body: {
            blocks: [{
                id: 'table-numeric',
                type: 'table',
                order: 1,
                content: {
                    type: 'table',
                    table: {
                        layout: { cellPadding: 8, repeatHeaderRows: true, headerRow: true, totalRow: true },
                        rows: [
                            { id: 'header-row', cells: [cell('name-h', 'Name', true), cell('value-h', 'Value', true)] },
                            { id: 'row-a', cells: [cell('name-a', 'A'), cell('value-a', '3')] },
                            { id: 'row-b', cells: [cell('name-b', 'B'), cell('value-b', '9')] },
                            { id: 'total-row', cells: [cell('total-label', 'Total'), cell('total', '')] },
                        ],
                    },
                },
            }],
        },
    };
}

function cell(id, text, isHeader = false) {
    return {
        id,
        columnSpan: 1,
        rowSpan: 1,
        isHeader,
        merge: { isOrigin: true, originCellId: null },
        blocks: [{
            id: `${id}-p`,
            type: 'paragraph',
            order: 1,
            paragraphProperties: { alignment: 0 },
            content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
        }],
    };
}

function collapsed(blockId, offset) {
    return {
        anchor: { blockId, offset },
        focus: { blockId, offset },
    };
}

function createHistory() {
    const undo = [];
    const redo = [];
    return {
        push(transaction) {
            undo.push(transaction);
            redo.length = 0;
        },
        undo() {
            const transaction = undo.pop();
            if (transaction) {
                redo.push(transaction);
            }

            return transaction || null;
        },
        redo() {
            const transaction = redo.pop();
            if (transaction) {
                undo.push(transaction);
            }

            return transaction || null;
        },
        snapshot() {
            return { undoDepth: undo.length, redoDepth: redo.length };
        },
    };
}
