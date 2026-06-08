import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../pagination.mjs';

test('multi-column text flows through configured columns before paginating', () => {
    const layout = layoutCanvasDocument({
        documentId: 'e3-column-flow',
        pageSettings: pageSettings(),
        theme: theme(),
        sections: [
            {
                id: 'news',
                order: 0,
                pageSettings: pageSettings(),
                properties: {
                    columns: { count: 2, spacing: 28, separatorLine: true },
                },
                blocks: [
                    paragraph('story', repeated('Column layout keeps a readable newspaper measure with stable line wrapping.', 42), 1),
                ],
            },
        ],
    }, { fontMetrics: createDeterministicMetrics() });

    assert.equal(layout.pages[0].columns.length, 2);
    const block = layout.blocks.find(item => item.blockId === 'story');
    assert.ok(block);
    const columns = new Set(block.lines.map(line => Number(line.columnIndex ?? -1)));
    assert.ok(columns.has(0));
    assert.ok(columns.has(1));
    assert.ok(block.lines.some(line => line.rect.x >= layout.pages[0].columns[1].x - 0.001));
});

test('column break starts following content in the next column on the same page', () => {
    const layout = layoutCanvasDocument({
        documentId: 'e3-column-break',
        pageSettings: pageSettings(),
        theme: theme(),
        sections: [
            {
                id: 'columns',
                order: 0,
                pageSettings: pageSettings(),
                properties: {
                    columns: { count: 2, spacing: 24, separatorLine: true },
                },
                blocks: [
                    paragraph('before-break', 'The first column contains a short intro.', 1),
                    {
                        id: 'column-break',
                        type: 'pageBreak',
                        order: 2,
                        sectionId: 'columns',
                        content: { type: 'pageBreak', pageBreak: { breakType: 'column' } },
                    },
                    paragraph('after-break', 'This paragraph begins at the top of the second column.', 3),
                ],
            },
        ],
    }, { fontMetrics: createDeterministicMetrics() });

    const before = layout.blocks.find(item => item.blockId === 'before-break');
    const after = layout.blocks.find(item => item.blockId === 'after-break');
    assert.ok(before);
    assert.ok(after);
    assert.equal(before.pageIndex, 0);
    assert.equal(after.pageIndex, 0);
    assert.equal(after.lines[0].columnIndex, 1);
    assert.ok(after.rect.x >= layout.pages[0].columns[1].x - 0.001);
});

test('newspaper balance distributes short final column content evenly', () => {
    const layout = layoutCanvasDocument({
        documentId: 'e3-balanced-columns',
        pageSettings: pageSettings(),
        theme: theme(),
        sections: [
            {
                id: 'columns',
                order: 0,
                pageSettings: pageSettings(),
                properties: {
                    columns: { count: 2, spacing: 24, separatorLine: true, balance: true },
                },
                blocks: [
                    paragraph('balanced-story', repeated('Balanced columns keep the final page calm and professionally even.', 12), 1),
                ],
            },
        ],
    }, { fontMetrics: createDeterministicMetrics() });

    const block = layout.blocks.find(item => item.blockId === 'balanced-story');
    assert.ok(block);
    const counts = [0, 0];
    for (const line of block.lines) {
        counts[Number(line.columnIndex || 0) || 0] += 1;
    }

    assert.ok(counts[0] > 0, 'The first balanced column should contain lines.');
    assert.ok(counts[1] > 0, 'The second balanced column should contain lines.');
    assert.ok(Math.abs(counts[0] - counts[1]) <= 1, `Expected balanced line counts, got ${counts.join('/')}.`);
    assert.ok(block.lines.some(line => line.rect.x >= layout.pages[0].columns[1].x - 0.001));
});

function pageSettings() {
    return {
        width: 420,
        height: 300,
        marginTop: 36,
        marginRight: 36,
        marginBottom: 36,
        marginLeft: 36,
    };
}

function paragraph(id, text, order) {
    return {
        id,
        sectionId: 'columns',
        type: 'paragraph',
        order,
        paragraphProperties: { alignment: 'left', lineSpacing: 1.05 },
        content: {
            type: 'paragraph',
            runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }],
        },
    };
}

function repeated(text, count) {
    return Array.from({ length: count }, () => text).join(' ');
}

function theme() {
    return { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11, paragraphSpacingAfter: 4 };
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, Array.from(text).reduce((sum, ch) => sum + (/\s/.test(ch) ? fontSize * 0.32 : fontSize * 0.5), 0)),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.2),
            };
        },
    };
}
