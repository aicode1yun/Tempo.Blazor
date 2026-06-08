import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../pagination.mjs';
import { hyphenationBreaksForWord, hyphenateTokenToFit } from '../hyphenation.mjs';

test('manual soft hyphen creates a hyphenated line segment before fallback splitting', () => {
    const model = {
        documentId: 'phase-e12-hyphenation-manual',
        pageSettings: { width: 220, height: 360, marginTop: 24, marginRight: 24, marginBottom: 24, marginLeft: 24 },
        hyphenation: { enabled: true, mode: 'manual', consecutiveLimit: 2, minPrefix: 3, minSuffix: 3 },
        body: {
            blocks: [paragraph('p1', 'Alpha international\u00ADization omega')],
        },
    };

    const layout = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const segments = layout.blocks.flatMap(block => block.segments || []);
    const hyphenated = segments.find(segment => segment.hyphenated === true);

    assert.ok(hyphenated);
    assert.equal(hyphenated.text.endsWith('-'), true);
    assert.equal(hyphenated.hyphenation.manual, true);
});

test('manual soft hyphens with multiple breakpoints render a visible hyphen when wrapping', () => {
    const model = {
        documentId: 'phase-e12-hyphenation-multiple-manual',
        pageSettings: { width: 112, height: 360, marginTop: 24, marginRight: 24, marginBottom: 24, marginLeft: 24 },
        hyphenation: { enabled: true, mode: 'manual', consecutiveLimit: 3, minPrefix: 2, minSuffix: 2 },
        body: {
            blocks: [paragraph('p1', 'edi\u00ADtor\u00ADial')],
        },
    };

    const layout = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const hyphenated = layout.blocks
        .flatMap(block => block.segments || [])
        .filter(segment => segment.hyphenated === true);

    assert.ok(hyphenated.length >= 1);
    assert.equal(hyphenated.some(segment => segment.text.endsWith('-')), true);
});

test('automatic hyphenation exposes valid break points and respects consecutive limit', () => {
    const breaks = hyphenationBreaksForWord('characteristically', { enabled: true, mode: 'auto', minPrefix: 3, minSuffix: 3 });
    assert.ok(breaks.length > 0);

    const result = hyphenateTokenToFit(
        { start: 0, type: 'word' },
        'characteristically',
        { fontSize: 16 },
        { measureText: (text, style) => ({ width: text.length * Number(style.fontSize || 16) * 0.5, height: 20 }) },
        62,
        { enabled: true, mode: 'auto', consecutiveLimit: 1, minPrefix: 3, minSuffix: 3 },
        { consecutiveCount: 1 },
    );
    assert.equal(result, null);
});

test('manual hyphenation can break at the second soft hyphen without dropping visible text', () => {
    const text = 'edi\u00ADtor\u00ADial';
    const result = hyphenateTokenToFit(
        { start: 0, type: 'word' },
        text,
        { fontSize: 10 },
        { measureText: value => ({ width: value.replace(/\u00AD/g, '').length * 5, height: 12 }) },
        40,
        { enabled: true, mode: 'manual', consecutiveLimit: 2, minPrefix: 2, minSuffix: 2 },
        { consecutiveCount: 0 },
    );

    assert.ok(result);
    assert.equal(result.text, 'editor-');
    assert.equal(result.remainderText, 'ial');
    assert.equal(result.hyphenation.manual, true);
});

function paragraph(id, text) {
    return {
        id,
        type: 'paragraph',
        order: 1,
        paragraphProperties: {},
        content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
    };
}

function metrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, text.replace(/\u00AD/g, '').length * fontSize * 0.54),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
