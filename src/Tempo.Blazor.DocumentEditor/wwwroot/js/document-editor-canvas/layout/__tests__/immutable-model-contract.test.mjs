import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument, normalizeTextBlock } from '../pagination.mjs';

// Phase N4.4-N4.6 (canvas perf 2026-07-10): block content signatures and text-block normalization
// are memoized BY OBJECT REFERENCE. That is only sound because the model is immutable-by-convention
// (mutators clone touched blocks via cloneBlockForWrite; unchanged blocks share references). These
// tests cement the convention: an in-place mutation without a new block reference must NOT
// propagate into the layout — and `strictSignatures` restores content-hash comparison for debugging.

function buildModel(count) {
    return {
        documentId: 'n4-immutable-contract',
        pageSettings: { width: 600, height: 900, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 8 },
        body: {
            blocks: Array.from({ length: count }, (_, index) => ({
                id: `p${index}`,
                type: 'paragraph',
                order: index + 1,
                paragraphProperties: { alignment: 'left', lineSpacing: 1.1 },
                content: { type: 'paragraph', runs: [{ id: `p${index}-run`, type: 'text', text: `Paragraph ${index + 1} shares deterministic contract text.`, marks: [] }] },
            })),
        },
    };
}

function metrics() {
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

function blockText(layout, blockId) {
    return layout.blocks
        .filter(block => String(block.blockId || block.id || '') === blockId)
        .flatMap(block => (block.lines || []).flatMap(line => (line.segments || []).map(segment => segment.text || '')))
        .join('');
}

test('in-place block mutation WITHOUT a new reference does not propagate (documented convention)', () => {
    const cache = new Map();
    const model = buildModel(5);
    layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });

    // Violate the copy-on-write convention on purpose: mutate the block object in place.
    model.body.blocks[2].content.runs[0].text = 'Mutated in place without cloning.';
    const relayout = layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });

    assert.ok(!blockText(relayout, 'p2').includes('Mutated in place'),
        'reference-based signatures must serve the cached layout for an unchanged block reference');
    assert.equal(relayout.cacheStats.misses, 0, 'no block may be recomputed for same references');
});

test('strictSignatures option restores content-hash comparison (in-place mutation propagates)', () => {
    const cache = new Map();
    const model = buildModel(5);
    layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache, strictSignatures: true });

    model.body.blocks[2].content.runs[0].text = 'Mutated in place without cloning.';
    const relayout = layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache, strictSignatures: true });

    assert.ok(blockText(relayout, 'p2').includes('Mutated in place'),
        'strict signatures must detect the in-place content change');
});

test('copy-on-write edit (cloned block, shared siblings) propagates and reuses the rest', () => {
    const cache = new Map();
    const model = buildModel(5);
    layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });

    // The convention the engine follows: clone the touched block, share every other reference.
    const edited = { ...model, body: { ...model.body, blocks: model.body.blocks.slice() } };
    const block = structuredClone(edited.body.blocks[2]);
    block.content.runs[0].text = 'Cloned block carries the edit.';
    edited.body.blocks[2] = block;

    const relayout = layoutCanvasDocument(edited, { fontMetrics: metrics(), layoutCache: cache });
    assert.ok(blockText(relayout, 'p2').includes('Cloned block carries the edit'),
        'a cloned block must re-lay-out with its new content');
    assert.ok(relayout.cacheStats.reusedBlockIds.includes('p1'), 'unchanged siblings are reused');
});

test('TABLE blocks are never signature-memoized: an in-place cell mutation propagates', () => {
    // The interactive table flow mutates cell structure in place somewhere (Phase14 tables E2E,
    // bisected 2026-07-10), so table signatures always re-hash — even for an unchanged reference.
    const cache = new Map();
    const model = buildModel(2);
    model.body.blocks.push({
        id: 'tbl',
        type: 'table',
        content: {
            type: 'table',
            table: {
                layout: { cellPadding: 5 },
                rows: [{
                    id: 'r1',
                    cells: [{
                        id: 'c1',
                        width: 120,
                        blocks: [{ id: 'c1-p', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'c1-r', type: 'text', text: 'Cell', marks: [] }] } }],
                    }, {
                        id: 'c2',
                        width: 120,
                        blocks: [{ id: 'c2-p', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'c2-r', type: 'text', text: 'Other', marks: [] }] } }],
                    }],
                }],
            },
        },
    });
    const first = layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });
    const widthOf = (layout) => layout.blocks.find(block => block.type === 'table')?.table?.cells?.[0]?.rect?.width || 0;
    const before = widthOf(first);

    // In-place mutation WITHOUT a new block reference — tables must still pick it up.
    model.body.blocks[2].content.table.rows[0].cells[0].width = 320;
    const relayout = layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });
    assert.notEqual(widthOf(relayout), before, 'in-place table mutation must invalidate the table layout');
});

test('normalizeTextBlock memoizes by block reference and revalidates on theme change', () => {
    const model = buildModel(1);
    const block = model.body.blocks[0];
    const service = metrics();

    const first = normalizeTextBlock(model, block, service);
    const second = normalizeTextBlock(model, block, service);
    assert.strictEqual(second, first, 'same (block, theme, styles, metrics) must return the memoized object');

    const rethemed = { ...model, theme: { ...model.theme, bodyFontFamily: 'Georgia, serif' } };
    const third = normalizeTextBlock(rethemed, block, service);
    assert.notStrictEqual(third, first, 'a new theme reference must rebuild the normalization');
    assert.equal(third.content.runs[0].style.fontFamily, 'Georgia, serif',
        'the rebuilt normalization reflects the new theme');
    assert.strictEqual(normalizeTextBlock(rethemed, block, service), third,
        'the rebuilt normalization is memoized for the new theme');
});
