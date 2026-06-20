import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../../layout/pagination.mjs';
import { selectionRectsForRange } from '../../../document-editor/core-engine/selection-overlay.mjs';
import { normalizeSelectionLayout } from '../selection-controller.mjs';

// B8 (UX fix 2026-06-11): selecting text inside a table cell must produce selection rects so the floating
// mini toolbar can anchor to the cell (the toolbar's bounding rect derives from these). The cell paragraph
// blocks are flattened into layout.blocks, so the shared selectionRectsForRange maps them.

test('a text selection inside a table cell produces selection rects (mini toolbar can anchor in tables)', () => {
    const model = createTableModel();
    const layout = normalizeSelectionLayout(layoutCanvasDocument(model, { fontMetrics: createDeterministicMetrics() }));

    const cellParagraphId = 'table-b8-r1c1-p';
    const rects = selectionRectsForRange(layout, { blockId: cellParagraphId, offset: 0 }, { blockId: cellParagraphId, offset: 6 });

    assert.ok(rects.length >= 1, 'selecting text inside a cell must produce at least one selection rect');
    assert.ok(rects.every(rect => rect.rect.width > 0 && rect.rect.height > 0), 'cell selection rects must have a positive size');
});

function createTableModel() {
    return {
        documentId: 'b8-table-cell-selection',
        pageSettings: { width: 720, height: 960, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11 },
        body: {
            blocks: [{
                id: 'table-b8',
                type: 'table',
                order: 1,
                content: {
                    type: 'table',
                    table: {
                        layout: { cellPadding: 6 },
                        rows: [
                            { id: 'row-1', cells: [cell('table-b8-r1c1', 'Header A', true), cell('table-b8-r1c2', 'Header B', true)] },
                            { id: 'row-2', cells: [cell('table-b8-r2c1', 'Wrapped value for left cell'), cell('table-b8-r2c2', 'Right value')] },
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
