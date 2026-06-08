import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../pagination.mjs';

test('pagination creates additional canvas pages and keeps text inside page bodies', () => {
    const layout = layoutCanvasDocument({
        documentId: 'phase-6-pagination',
        pageSettings: { width: 360, height: 240, marginTop: 32, marginRight: 36, marginBottom: 32, marginLeft: 36 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11, paragraphSpacingAfter: 8 },
        body: {
            blocks: [
                paragraph('intro', longText(), 1),
                { id: 'manual-break', type: 'pageBreak', order: 2, content: { type: 'pageBreak' } },
                paragraph('after-break', 'This paragraph starts after a manual page break and uses the same body frame.', 3),
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    assert.ok(layout.pages.length >= 3);
    for (const rect of layout.textRects) {
        const page = layout.pages[rect.pageIndex];
        assert.ok(rect.rect.x >= page.body.x - 0.001);
        assert.ok(rect.rect.y >= page.body.y - 0.001);
        assert.ok(rect.rect.x + rect.rect.width <= page.body.x + page.body.width + 0.001);
        assert.ok(rect.rect.y + rect.rect.height <= page.body.y + page.body.height + 0.001);
    }
});

test('list labels are laid out separately from wrapped list item text', () => {
    const layout = layoutCanvasDocument({
        documentId: 'phase-6-list-layout',
        pageSettings: { width: 420, height: 520, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11, paragraphSpacingAfter: 8 },
        body: {
            blocks: [
                {
                    id: 'ordered-list',
                    type: 'list',
                    order: 1,
                    paragraphProperties: { alignment: 'left' },
                    content: {
                        type: 'list',
                        list: { ordered: true, indentLevel: 1, startNumber: 3 },
                        runs: [{ id: 'ordered-list-run', type: 'text', text: 'A wrapped list item keeps the number outside the text measure.', marks: [] }],
                    },
                },
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    assert.equal(layout.listLabels.length, 1);
    const label = layout.listLabels[0];
    const firstSegment = layout.blocks[0].segments[0];
    assert.equal(label.text, 'c.');
    assert.ok(label.x + label.width < firstSegment.rect.x);
});

test('empty paragraphs keep a measurable caret stop for immediate typing after Enter', () => {
    const layout = layoutCanvasDocument({
        documentId: 'phase-8-empty-paragraph-caret',
        pageSettings: { width: 420, height: 520, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11, paragraphSpacingAfter: 8 },
        body: {
            blocks: [
                paragraph('filled', 'Filled paragraph.', 1),
                paragraph('empty-after-enter', '', 2),
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    const empty = layout.blocks.find(block => block.blockId === 'empty-after-enter');
    assert.ok(empty);
    assert.equal(empty.lines.length, 1);
    assert.equal(empty.segments.length, 0);
    assert.equal(empty.caretStops.length, 1);
    assert.equal(empty.caretStops[0].offset, 0);
    assert.equal(empty.caretStops[0].blockId, 'empty-after-enter');
});

test('paragraphs ending with a soft break expose a terminal caret stop on the next line', () => {
    const layout = layoutCanvasDocument({
        documentId: 'phase-8-soft-break-terminal-caret',
        pageSettings: { width: 420, height: 520, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11, paragraphSpacingAfter: 8 },
        body: {
            blocks: [
                paragraph('soft-break', 'World\n', 1),
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    const block = layout.blocks.find(item => item.blockId === 'soft-break');
    assert.ok(block);
    const terminal = block.caretStops.find(stop => stop.offset === 'World\n'.length);
    assert.ok(terminal);
    assert.equal(terminal.blockId, 'soft-break');
    assert.ok(block.lines.length >= 2);
    assert.equal(terminal.lineId, block.lines.at(-1).id);
});

function paragraph(id, text, order) {
    return {
        id,
        type: 'paragraph',
        order,
        paragraphProperties: { alignment: 'left', lineSpacing: 1.1 },
        content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
    };
}

function longText() {
    return Array.from({ length: 10 }, () => 'Pagination keeps long paragraphs flowing through available page bodies without overlapping adjacent lines.').join(' ');
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, Array.from(text).reduce((sum, ch) => sum + (/\s/.test(ch) ? fontSize * 0.32 : fontSize * 0.52), 0)),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
