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

test('a page-fixed flow-affecting image above the flow position never rewinds the cursor (no text overlap)', () => {
    // Regression (2026-06-12 video): switching a page-anchored badge (fixed Y near the page top)
    // to a flow-affecting wrap mode rewound cursorY to the image bottom, so the next paragraph
    // painted on top of the previous one.
    const pageSettings = { width: 600, height: 900, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 };
    const fixedTopBottom = {
        id: 'fixed-img',
        type: 'image',
        order: 2,
        content: {
            type: 'image',
            image: {
                objectId: 'fixed-img',
                url: 'data:image/png;base64,iVBORw0KGgo=',
                layout: {
                    kind: 2,
                    wrapMode: 'TopBottom',
                    transform: { width: 96, height: 54 },
                    position: { x: 20, y: 250, verticalRelativeTo: 'page' },
                    anchor: { fixedOnPage: true },
                },
            },
        },
    };
    const longText = Array.from({ length: 14 }, () => 'Long paragraph text that pushes the flow cursor well below the fixed image position.').join(' ');
    const layout = layoutCanvasDocument({
        documentId: 'backward-flow-guard',
        pageSettings,
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 10 },
        body: { blocks: [paragraph('pA', longText, 1), fixedTopBottom, paragraph('pB', 'Following paragraph must continue below paragraph A.', 3)] },
    }, { fontMetrics: createDeterministicMetrics() });

    const blockA = layout.blocks.find(item => item.blockId === 'pA');
    const blockB = layout.blocks.find(item => item.blockId === 'pB');
    assert.ok(blockA && blockB, 'both paragraphs must be laid out');
    if (blockB.pageIndex === blockA.pageIndex) {
        const aBottom = blockA.rect.y + blockA.rect.height;
        assert.ok(
            blockB.rect.y >= aBottom - 0.5,
            `paragraph B (y=${blockB.rect.y}) must start below paragraph A (bottom=${aBottom})`);
    }
});

test('an inline image near the page bottom breaks to the next page instead of painting into the footer', () => {
    // Regression: the standalone drawing-run branch had no overflow check at all, and the image
    // branch ignored the caption, so images near the body bottom painted across the footer band.
    const pageSettings = { width: 600, height: 500, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 };
    const bodyBottom = 500 - 48;
    const drawingImage = {
        id: 'img-drawing',
        type: 'paragraph',
        order: 2,
        content: {
            type: 'paragraph',
            runs: [{
                id: 'img-drawing-run',
                type: 'drawing',
                drawing: {
                    kind: 'image',
                    image: {
                        objectId: 'img-drawing',
                        url: 'data:image/png;base64,iVBORw0KGgo=',
                        caption: 'Caption that must stay inside the body band.',
                        layout: { kind: 0, wrapMode: 'Inline', transform: { width: 220, height: 124 }, position: {} },
                    },
                },
            }],
        },
    };
    const filler = Array.from({ length: 16 }, () => 'Filler sentence occupying page body vertical space here.').join(' ');
    const layout = layoutCanvasDocument({
        documentId: 'image-footer-overflow',
        pageSettings,
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 10 },
        body: { blocks: [paragraph('filler', filler, 1), drawingImage] },
    }, { fontMetrics: createDeterministicMetrics() });

    const image = layout.objectLayouts.find(item => item.blockId === 'img-drawing');
    assert.ok(image, 'image must be laid out');
    const footprintBottom = image.captionRect
        ? image.captionRect.y + image.captionRect.height
        : image.rect.y + image.rect.height;
    assert.ok(
        footprintBottom <= bodyBottom + 0.5,
        `image footprint (bottom=${footprintBottom}) must not cross the body bottom (${bodyBottom}); pageIndex=${image.pageIndex}`);
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
