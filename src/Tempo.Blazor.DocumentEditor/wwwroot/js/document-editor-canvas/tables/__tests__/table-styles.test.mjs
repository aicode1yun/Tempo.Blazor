import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../../render/display-list.mjs';
import { layoutCanvasDocument } from '../../layout/pagination.mjs';
import { resolveTableCellStyle } from '../table-styles.mjs';

test('table style applies header, total, row banding and page repeated header cells', () => {
    const model = createPagedTableModel();
    const layout = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const table = layout.blocks.find(block => block.type === 'table');
    const display = buildDisplayList(model, { pageSettings: model.pageSettings }, { fontMetrics: metrics() });
    const cells = display.commands.filter(command => command.type === 'tableCell');

    assert.ok(table.table.splitCount > 0);
    assert.ok(table.table.repeatedHeaderRows > 0);
    assert.ok(cells.some(cell => cell.isRepeatedHeader === true && cell.pageIndex > 0));
    assert.ok(cells.some(cell => cell.bandedRow === true));
    assert.ok(cells.some(cell => cell.isTotal === true));
});

test('table style resolver honors explicit cell background over conditional styles', () => {
    const style = resolveTableCellStyle({
        layout: {
            headerRow: true,
            bandedRows: true,
            style: { headerBackgroundColor: '#111111', bandedRowBackgroundColor: '#222222' },
        },
    }, { backgroundColor: '#abcdef' }, 0, 0, 4);

    assert.equal(style.backgroundColor, '#abcdef');
});

function createPagedTableModel() {
    return {
        documentId: 'phase-e12-table-style-test',
        pageSettings: { width: 360, height: 260, marginTop: 24, marginRight: 24, marginBottom: 24, marginLeft: 24 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11 },
        body: {
            blocks: [{
                id: 'table-e12',
                type: 'table',
                order: 1,
                content: {
                    type: 'table',
                    table: {
                        layout: {
                            cellPadding: 5,
                            repeatHeaderRows: true,
                            headerRow: true,
                            totalRow: true,
                            bandedRows: true,
                            style: {
                                headerBackgroundColor: '#dbeafe',
                                totalBackgroundColor: '#dcfce7',
                                bandedRowBackgroundColor: '#f8fafc',
                                borderColor: '#2563eb',
                            },
                        },
                        rows: [
                            { id: 'row-1', cells: [cell('h1', 'Name', true), cell('h2', 'Value', true)] },
                            ...Array.from({ length: 8 }, (_, index) => ({ id: `row-${index + 2}`, cells: [cell(`r${index}c1`, `Item ${index + 1}`), cell(`r${index}c2`, String(index + 1))] })),
                            { id: 'row-total', cells: [cell('t1', 'Total'), cell('t2', '36')] },
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
        padding: 5,
        blocks: [{
            id: `${id}-p`,
            type: 'paragraph',
            order: 1,
            paragraphProperties: { alignment: 0 },
            content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
        }],
    };
}

function metrics() {
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
