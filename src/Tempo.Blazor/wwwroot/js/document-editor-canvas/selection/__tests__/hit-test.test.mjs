import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../../layout/pagination.mjs';
import {
    caretRectForPosition,
    hitTestOnPage,
    normalizeSelectionLayout,
    tableCellRectsForSelectionRange,
    tableResizeHandleAt,
} from '../selection-controller.mjs';
import { createSelectionTestLayout } from './selection-test-helpers.mjs';

test('point hit-test resolves exact caret stops on the requested canvas page', () => {
    const { layout } = createSelectionTestLayout();
    const selectionLayout = normalizeSelectionLayout(layout);
    const firstStop = selectionLayout.blocks[0].caretStops.find(stop => stop.offset > 4);
    const hit = hitTestOnPage(selectionLayout, firstStop.pageIndex, firstStop.rect.x + 2, firstStop.rect.y + firstStop.rect.height / 2);

    assert.equal(hit.blockId, firstStop.blockId);
    assert.equal(hit.offset, firstStop.offset);
    assert.equal(hit.pageIndex, firstStop.pageIndex);
});

test('model position maps back to a measurable caret rectangle', () => {
    const { layout } = createSelectionTestLayout();
    const selectionLayout = normalizeSelectionLayout(layout);
    const rect = caretRectForPosition(selectionLayout, { blockId: 'paragraph-1', offset: 6 });

    assert.equal(rect.pageIndex, 0);
    assert.ok(rect.rect.x > 0);
    assert.ok(rect.rect.y > 0);
    assert.ok(rect.rect.height >= 12);
});

test('table cell hit-test keeps clicks in blank cell space inside the owning cell', () => {
    const layout = normalizeSelectionLayout(layoutCanvasDocument(createTableModel(), { fontMetrics: createDeterministicMetrics() }));
    const table = layout.blocks.find(block => block.type === 'table');
    const leftCell = table.table.cells.find(cell => cell.cellId === 'table-hit-r1c1');
    const hit = hitTestOnPage(
        layout,
        leftCell.pageIndex,
        leftCell.rect.x + leftCell.rect.width - 8,
        leftCell.rect.y + leftCell.rect.height / 2);

    assert.equal(hit.blockId, 'table-hit-r1c1-p');
});

test('table cell range and resize handle resolve from canvas table geometry', () => {
    const model = createTableModel();
    model.body.blocks[0].content.table.rows.push({
        id: 'table-hit-row-2',
        cells: [
            tableCell('table-hit-r2c1', 'Lower left'),
            tableCell('table-hit-r2c2', 'Lower right'),
        ],
    });
    const layout = normalizeSelectionLayout(layoutCanvasDocument(model, { fontMetrics: createDeterministicMetrics() }));
    const table = layout.blocks.find(block => block.type === 'table');
    const firstCell = table.table.cells.find(cell => cell.cellId === 'table-hit-r1c1');
    const rangeRects = tableCellRectsForSelectionRange(
        layout,
        model,
        { anchor: { blockId: 'table-hit-r1c1-p', offset: 0 }, focus: { blockId: 'table-hit-r2c2-p', offset: 1 } });
    const handle = tableResizeHandleAt(
        layout,
        firstCell.pageIndex,
        firstCell.rect.x + firstCell.rect.width,
        firstCell.rect.y + firstCell.rect.height / 2);

    assert.equal(rangeRects.length, 4);
    assert.equal(rangeRects.every(item => item.rect.width > 0 && item.rect.height > 0), true);
    assert.equal(handle.cellId, 'table-hit-r1c1');
    assert.equal(handle.columnIndex, 0);
    assert.equal(handle.width, firstCell.rect.width);
});

function createTableModel() {
    return {
        documentId: 'phase-14-table-hit-test',
        pageSettings: { width: 720, height: 960, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11 },
        body: {
            blocks: [{
                id: 'table-hit',
                type: 'table',
                order: 1,
                content: {
                    type: 'table',
                    table: {
                        layout: { width: 360, cellPadding: 6 },
                        rows: [{
                            id: 'table-hit-row-1',
                            cells: [
                                tableCell('table-hit-r1c1', 'Left'),
                                tableCell('table-hit-r1c2', 'Right'),
                            ],
                        }],
                    },
                },
            }],
        },
    };
}

function tableCell(id, text) {
    return {
        id,
        columnSpan: 1,
        rowSpan: 1,
        blocks: [{
            id: `${id}-p`,
            type: 'paragraph',
            paragraphProperties: { alignment: 0 },
            content: { type: 'paragraph', runs: [{ id: `${id}-r`, type: 'text', text, marks: [] }] },
        }],
    };
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, text.length * fontSize * 0.5),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
