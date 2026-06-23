import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../pagination.mjs';

test('alignment, paragraph spacing, indents, and hanging indents affect line placement', () => {
    const layout = layoutCanvasDocument({
        documentId: 'phase-6-alignment-spacing',
        pageSettings: { width: 560, height: 760, marginTop: 60, marginRight: 60, marginBottom: 60, marginLeft: 60 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11, paragraphSpacingAfter: 6 },
        body: {
            blocks: [
                paragraph('left', 'Left aligned paragraph.', { alignment: 'left', spacingAfter: 12 }),
                paragraph('center', 'Centered paragraph.', { alignment: 'center', leftIndent: 18, rightIndent: 18, spacingBefore: 10, spacingAfter: 12 }),
                paragraph('right', 'Right aligned paragraph.', { alignment: 'right', firstLineIndent: 24, spacingAfter: 12 }),
                paragraph('hanging', 'Hanging indent continues on following wrapped lines with a stable left edge for readable list-like prose.', { alignment: 'left', leftIndent: 36, firstLineIndent: -18, spacingAfter: 0 }),
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    const blocks = new Map(layout.blocks.map(block => [block.blockId, block]));
    const leftLine = blocks.get('left').lines[0];
    const centerLine = blocks.get('center').lines[0];
    const rightLine = blocks.get('right').lines[0];
    const hangingLines = blocks.get('hanging').lines;

    assert.equal(leftLine.rect.x, 60);
    assert.ok(centerLine.rect.x > leftLine.rect.x);
    assert.ok(rightLine.rect.x > centerLine.rect.x);
    assert.ok(blocks.get('center').rect.y > blocks.get('left').rect.y + blocks.get('left').rect.height);
    assert.ok(hangingLines.length >= 2);
    assert.ok(hangingLines[0].rect.x < hangingLines[1].rect.x);
});

test('justify marks non-final lines with gap expansion metadata', () => {
    const layout = layoutCanvasDocument({
        documentId: 'phase-6-justify',
        pageSettings: { width: 420, height: 760, marginTop: 60, marginRight: 60, marginBottom: 60, marginLeft: 60 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11, paragraphSpacingAfter: 6 },
        body: {
            blocks: [
                paragraph('justify', 'Justified text spreads words across the measure while the final line remains natural.', { alignment: 'justify' }),
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    const lines = layout.blocks[0].lines;
    assert.ok(lines.length >= 2);
    assert.ok(lines.slice(0, -1).some(line => line.justify?.enabled === true));
});

function paragraph(id, text, paragraphProperties) {
    return {
        id,
        type: 'paragraph',
        order: id === 'left' ? 1 : id === 'center' ? 2 : id === 'right' ? 3 : 4,
        paragraphProperties,
        content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
    };
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
