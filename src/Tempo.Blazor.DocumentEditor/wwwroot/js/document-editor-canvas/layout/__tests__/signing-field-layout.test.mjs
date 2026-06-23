import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../pagination.mjs';

// A signing field run lays out as an atomic inline box (boxWidth x boxHeight) that flows with the
// surrounding text — it reserves its full width (never measured as empty text) and never breaks
// inside (plan S2.5). Caret stops exist before and after the box.

function modelWithInlineSigningField() {
    return {
        documentId: 'signing-field-layout',
        body: {
            blocks: [
                {
                    id: 'p1',
                    type: 'paragraph',
                    content: {
                        type: 'paragraph',
                        runs: [
                            { id: 'r1', type: 'text', text: 'Sign here: ', marks: [] },
                            {
                                id: 'r2',
                                type: 'signingField',
                                text: '',
                                marks: [],
                                signingField: { uuid: 'field-1', fieldType: 'signature', submitterUuid: 'signer', boxWidth: 180, boxHeight: 44 },
                            },
                            { id: 'r3', type: 'text', text: ' (required)', marks: [] },
                        ],
                    },
                },
            ],
        },
    };
}

function findSigningSegment(layout) {
    for (const block of layout.blocks) {
        for (const segment of block.segments || []) {
            if (segment.kind === 'signingField' || segment.signingField) {
                return { block, segment };
            }
        }
    }

    return null;
}

test('a signing field run reserves an atomic box of its declared size on the text line', () => {
    const layout = layoutCanvasDocument(modelWithInlineSigningField(), { fontMetrics: createDeterministicMetrics() });
    const found = findSigningSegment(layout);

    assert.ok(found, 'a signing field segment is laid out');
    assert.equal(found.segment.kind, 'signingField');
    assert.equal(Math.round(found.segment.rect.width), 180, 'the box reserves its full declared width (not measured as empty text)');
    assert.ok(found.segment.rect.height >= 44, 'the line height accommodates the box height');
    assert.equal(found.segment.runId, 'r2');
    assert.ok(found.segment.signingField, 'the segment carries the signing field payload');
    assert.equal(found.segment.signingField.uuid, 'field-1');
    assert.equal(found.segment.signingField.fieldType, 'signature');
});

test('the signing field shares the line with the surrounding text (single line)', () => {
    const layout = layoutCanvasDocument(modelWithInlineSigningField(), { fontMetrics: createDeterministicMetrics() });
    const found = findSigningSegment(layout);
    const textSegments = found.block.segments.filter(segment => segment.runId === 'r1' || segment.runId === 'r3');

    assert.ok(textSegments.length >= 1, 'the surrounding text is laid out');
    for (const text of textSegments) {
        assert.ok(Math.abs(text.rect.y - found.segment.rect.y) < found.segment.rect.height,
            'text and field box sit on the same line');
    }
});

test('caret stops exist before and after the signing field box', () => {
    const layout = layoutCanvasDocument(modelWithInlineSigningField(), { fontMetrics: createDeterministicMetrics() });
    const found = findSigningSegment(layout);
    const stops = found.block.caretStops || [];
    const fieldStops = stops.filter(stop => stop.runId === 'r2' || stop.objectBoundary === true);

    assert.ok(fieldStops.length >= 2, 'there are caret stops bracketing the field box');
});

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, text.length * fontSize * 0.55),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
