import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../../render/display-list.mjs';
import { layoutCanvasDocument } from '../../layout/pagination.mjs';

test('canvas table layout renders cells, text and caret stops inside cell content', () => {
    const model = createTableModel();
    const layout = layoutCanvasDocument(model, { fontMetrics: createDeterministicMetrics() });
    const table = layout.blocks.find(block => block.type === 'table');
    const nested = layout.blocks.filter(block => block.cell?.tableId === 'table-14');
    const display = buildDisplayList(model, { pageSettings: model.pageSettings }, { fontMetrics: createDeterministicMetrics() });

    assert.ok(table);
    assert.equal(table.table.rowCount, 2);
    assert.equal(table.table.columnCount, 2);
    assert.equal(table.table.cells.length, 4);
    assert.equal(nested.length, 4);
    assert.ok(nested.every(block => block.rect.x >= table.rect.x));
    assert.ok(nested.every(block => block.rect.x + block.rect.width <= table.rect.x + table.rect.width));
    assert.ok(nested.every(block => block.caretStops.length > 0));
    assert.ok(display.commands.filter(command => command.type === 'tableCell').length >= 4);
    assert.ok(display.commands.some(command => command.type === 'textRun' && command.blockId === 'table-14-r1c1-p'));
});

function createTableModel() {
    return {
        documentId: 'phase-14-table-layout-test',
        pageSettings: { width: 720, height: 960, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11 },
        body: {
            blocks: [{
                id: 'table-14',
                type: 'table',
                order: 1,
                content: {
                    type: 'table',
                    table: {
                        layout: { cellPadding: 6, backgroundColor: 'rgba(248, 250, 252, 0.9)' },
                        rows: [
                            { id: 'row-1', cells: [cell('table-14-r1c1', 'Header A', true), cell('table-14-r1c2', 'Header B', true)] },
                            { id: 'row-2', cells: [cell('table-14-r2c1', 'Wrapped value for left cell'), cell('table-14-r2c2', 'Right value')] },
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
        merge: { isOrigin: true },
        backgroundColor: isHeader ? 'rgba(226, 232, 240, 0.84)' : null,
        verticalAlignment: 'top',
        padding: 6,
        blocks: [{
            id: `${id}-p`,
            type: 'paragraph',
            order: 1,
            paragraphProperties: { alignment: isHeader ? 1 : 0, lineSpacing: 1.1 },
            content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
        }],
    };
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, text.length * fontSize * 0.52),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
