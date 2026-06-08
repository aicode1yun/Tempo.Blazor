import assert from 'node:assert/strict';
import test from 'node:test';
import { graphemeBoundaries, graphemeCount } from '../../../document-editor/layout/grapheme.mjs';
import { layoutCanvasDocument } from '../pagination.mjs';

test('long paragraphs wrap into deterministic line boxes without text overlaps', () => {
    const layout = layoutCanvasDocument(createModel({
        id: 'wrap-paragraph',
        text: 'Canvas layout measures text runs, breaks words softly, and keeps every line box inside the document body even when the paragraph is deliberately long.',
        pageWidth: 360,
    }), { fontMetrics: createDeterministicMetrics() });

    const lines = layout.blocks.flatMap(block => block.lines || []);
    assert.ok(lines.length >= 4);
    assertNoTextOverlap(layout.textRects);
    for (const line of lines) {
        const page = layout.pages[line.pageIndex];
        assert.ok(line.rect.x >= page.body.x);
        assert.ok(line.rect.x + line.rect.width <= page.body.x + page.body.width + 0.001);
    }
});

test('hard breaks create separate line boxes and grapheme segmentation keeps clusters intact', () => {
    const layout = layoutCanvasDocument(createModel({
        id: 'hard-breaks',
        text: 'First line\nSecond line with e\u0301 and 👨‍👩‍👧 cluster',
        pageWidth: 520,
    }), { fontMetrics: createDeterministicMetrics() });
    const lines = layout.blocks.flatMap(block => block.lines || []);
    const hardBreakLine = lines.find(line => line.hardBreak === true);

    assert.ok(hardBreakLine);
    assert.equal(graphemeCount('e\u0301'), 1);
    assert.deepEqual(graphemeBoundaries('👨‍👩‍👧'), [0, '👨‍👩‍👧'.length]);
});

test('math runs are laid out as one atomic segment without overlapping adjacent text', () => {
    const layout = layoutCanvasDocument({
        documentId: 'phase-e8-math-atomic-layout',
        pageSettings: { width: 620, height: 620, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 16, paragraphSpacingAfter: 8 },
        body: {
            blocks: [
                {
                    id: 'math-paragraph',
                    type: 'paragraph',
                    order: 1,
                    paragraphProperties: { alignment: 'left', lineSpacing: 1.15 },
                    content: {
                        type: 'paragraph',
                        runs: [
                            { id: 'before-math', type: 'text', text: 'Inline equation: ', marks: [] },
                            {
                                id: 'structured-math',
                                type: 'math',
                                text: '',
                                marks: [],
                                math: {
                                    mathId: 'structured-math',
                                    displayMode: 'inline',
                                    content: {
                                        elements: [{
                                            type: 'nary',
                                            operator: '∑',
                                            lowerLimit: { elements: [{ type: 'run', text: 'i=1', style: 'italic' }] },
                                            upperLimit: { elements: [{ type: 'run', text: 'n', style: 'italic' }] },
                                            base: {
                                                elements: [{
                                                    type: 'fraction',
                                                    numerator: { elements: [{ type: 'run', text: 'a+b', style: 'italic' }] },
                                                    denominator: { elements: [{ type: 'run', text: 'c+d', style: 'italic' }] },
                                                }],
                                            },
                                        }],
                                    },
                                },
                            },
                            { id: 'after-math', type: 'text', text: ' without DOM contenteditable.', marks: [] },
                        ],
                    },
                },
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    const block = layout.blocks.find(item => item.blockId === 'math-paragraph');
    const mathSegments = block.segments.filter(segment => segment.kind === 'math');
    assert.equal(mathSegments.length, 1);
    assert.ok(mathSegments[0].rect.width > 10);
    assert.ok(mathSegments[0].rect.height > 18);
    assertNoTextOverlap(layout.textRects);
});

function createModel({ id, text, pageWidth }) {
    return {
        documentId: 'phase-6-line-breaking',
        pageSettings: { width: pageWidth, height: 620, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11.5, paragraphSpacingAfter: 8 },
        body: {
            blocks: [
                {
                    id,
                    type: 'paragraph',
                    order: 1,
                    paragraphProperties: { alignment: 'left', lineSpacing: 1.15 },
                    content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
                },
            ],
        },
    };
}

function assertNoTextOverlap(rects) {
    const ordered = rects
        .map(item => ({ id: item.id, pageIndex: item.pageIndex, ...item.rect }))
        .filter(rect => rect.width > 0 && rect.height > 0);
    for (let i = 0; i < ordered.length; i++) {
        for (let j = i + 1; j < ordered.length; j++) {
            const left = ordered[i];
            const right = ordered[j];
            if (left.pageIndex !== right.pageIndex) {
                continue;
            }

            const overlapX = Math.min(left.x + left.width, right.x + right.width) - Math.max(left.x, right.x);
            const overlapY = Math.min(left.y + left.height, right.y + right.height) - Math.max(left.y, right.y);
            assert.ok(overlapX <= 0.01 || overlapY <= 0.01, `${left.id} overlaps ${right.id}`);
        }
    }
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
