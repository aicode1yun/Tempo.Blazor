import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../pagination.mjs';

// Phase 3 (perf+rendering fix 2026-06-08): the block-level layout cache must (a) reuse every block
// when nothing changed, (b) recompute only the changed block (and any block whose flow position
// shifted) on an edit, and (c) always produce byte-identical output to a fresh full layout.

test('re-laying out an unchanged model reuses every cached block', () => {
    const cache = new Map();
    const model = buildModel(20);

    const first = layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });
    assert.equal(first.cacheStats.hits, 0, 'cold pass has no hits');
    assert.ok(first.cacheStats.misses >= 20, 'cold pass lays out every block');

    const second = layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });
    assert.equal(second.cacheStats.misses, 0, 'warm pass recomputes nothing');
    assert.equal(second.cacheStats.hits, first.cacheStats.misses, 'warm pass reuses every block');

    assert.equal(blocksJson(second), blocksJson(first), 'cached output is identical');
});

test('a height-preserving edit recomputes only the edited block and reuses the rest', () => {
    const cache = new Map();
    const model = buildModel(20);
    layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });

    // Swap a single character (same glyph width under the deterministic metrics) so the line count
    // and therefore every following block's flow position are unchanged.
    const edited = structuredClone(model);
    // 'keeps' -> 'keepX': one glyph swap, identical width/length under the deterministic metrics.
    edited.body.blocks[10].content.runs[0].text =
        edited.body.blocks[10].content.runs[0].text.replace('keeps', 'keepX');

    const incremental = layoutCanvasDocument(edited, { fontMetrics: metrics(), layoutCache: cache });
    assert.equal(incremental.cacheStats.misses, 1, 'only the edited block is recomputed');
    assert.ok(!incremental.cacheStats.reusedBlockIds.includes('p10'), 'the edited block is not reused');
    assert.ok(incremental.cacheStats.reusedBlockIds.includes('p9'), 'preceding blocks are reused');
    assert.ok(incremental.cacheStats.reusedBlockIds.includes('p11'), 'following blocks are reused (height unchanged)');

    const fresh = layoutCanvasDocument(edited, { fontMetrics: metrics() });
    assert.equal(blocksJson(incremental), blocksJson(fresh), 'incremental output equals a fresh full layout');
});

test('a height-changing edit recomputes the edited block and everything after it', () => {
    const cache = new Map();
    const model = buildModel(20);
    layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });

    const edited = structuredClone(model);
    // Add many words so the block grows by at least one line, shifting all following blocks down.
    edited.body.blocks[5].content.runs[0].text += ' ' + Array.from({ length: 40 }, () => 'extra').join(' ');

    const incremental = layoutCanvasDocument(edited, { fontMetrics: metrics(), layoutCache: cache });
    assert.ok(!incremental.cacheStats.reusedBlockIds.includes('p5'), 'edited block is recomputed');
    assert.ok(!incremental.cacheStats.reusedBlockIds.includes('p6'), 'the next block shifts and is recomputed');
    assert.ok(!incremental.cacheStats.reusedBlockIds.includes('p19'), 'the last block shifts and is recomputed');
    assert.ok(incremental.cacheStats.reusedBlockIds.includes('p4'), 'blocks before the edit are reused');

    const fresh = layoutCanvasDocument(edited, { fontMetrics: metrics() });
    assert.equal(blocksJson(incremental), blocksJson(fresh), 'incremental output equals a fresh full layout');
});

test('removing a block prunes its cache entry', () => {
    const cache = new Map();
    const model = buildModel(10);
    layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });
    assert.ok(cache.has('p9'));

    const edited = structuredClone(model);
    edited.body.blocks.splice(9, 1);
    layoutCanvasDocument(edited, { fontMetrics: metrics(), layoutCache: cache });
    assert.ok(!cache.has('p9'), 'removed block must be evicted from the cache');
});

function buildModel(count) {
    return {
        documentId: 'phase3-incremental',
        pageSettings: { width: 600, height: 900, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 8 },
        body: {
            blocks: Array.from({ length: count }, (_, index) => ({
                id: `p${index}`,
                type: 'paragraph',
                order: index + 1,
                paragraphProperties: { alignment: 'left', lineSpacing: 1.1 },
                content: { type: 'paragraph', runs: [{ id: `p${index}-run`, type: 'text', text: `Paragraph ${index + 1} keeps deterministic descriptive contract text here.`, marks: [] }] },
            })),
        },
    };
}

function blocksJson(layout) {
    return JSON.stringify(layout.blocks);
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
