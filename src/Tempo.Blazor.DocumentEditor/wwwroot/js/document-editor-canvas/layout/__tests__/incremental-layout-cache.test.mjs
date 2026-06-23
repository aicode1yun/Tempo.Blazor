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

test('a table block is reused from the cache when an unrelated block is edited', () => {
    const cache = new Map();
    const model = buildModel(6);
    model.body.blocks.splice(3, 0, buildTableBlock('tbl'));
    layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });
    assert.ok(cache.has('tbl'), 'the table layout must be cached');

    // Width-preserving edit in a paragraph BELOW the table: the table's incoming flow state is
    // unchanged, so its (expensive) layout must be reused.
    const edited = structuredClone(model);
    const below = edited.body.blocks.find(block => block.id === 'p4');
    below.content.runs[0].text = below.content.runs[0].text.replace('keeps', 'keepX');

    const incremental = layoutCanvasDocument(edited, { fontMetrics: metrics(), layoutCache: cache });
    assert.ok(incremental.cacheStats.reusedBlockIds.includes('tbl'), 'unchanged table is reused');

    const fresh = layoutCanvasDocument(edited, { fontMetrics: metrics() });
    assert.equal(blocksJson(incremental), blocksJson(fresh), 'cached table output equals a fresh full layout');
});

test('editing a table cell recomputes the table and matches a fresh layout', () => {
    const cache = new Map();
    const model = buildModel(4);
    model.body.blocks.splice(2, 0, buildTableBlock('tbl'));
    layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });

    const edited = structuredClone(model);
    const table = edited.body.blocks.find(block => block.id === 'tbl');
    table.content.table.rows[1].cells[0].blocks[0].content.runs[0].text = 'Edited cell text that is much longer than before so the row grows.';

    const incremental = layoutCanvasDocument(edited, { fontMetrics: metrics(), layoutCache: cache });
    assert.ok(!incremental.cacheStats.reusedBlockIds.includes('tbl'), 'edited table must be recomputed');

    const fresh = layoutCanvasDocument(edited, { fontMetrics: metrics() });
    assert.equal(blocksJson(incremental), blocksJson(fresh), 'recomputed table output equals a fresh full layout');
});

function buildTableBlock(id) {
    const cell = (cellId, text) => ({
        id: cellId,
        blocks: [{
            id: `${cellId}-p`,
            type: 'paragraph',
            content: { type: 'paragraph', runs: [{ id: `${cellId}-run`, type: 'text', text, marks: [] }] },
        }],
    });
    return {
        id,
        type: 'table',
        order: 0,
        content: {
            type: 'table',
            table: {
                layout: { cellPadding: 5 },
                rows: [
                    { id: `${id}-r1`, cells: [cell(`${id}-r1c1`, 'Name'), cell(`${id}-r1c2`, 'Value')] },
                    { id: `${id}-r2`, cells: [cell(`${id}-r2c1`, 'Item one'), cell(`${id}-r2c2`, 'First value')] },
                    { id: `${id}-r3`, cells: [cell(`${id}-r3c1`, 'Item two'), cell(`${id}-r3c2`, 'Second value')] },
                ],
            },
        },
    };
}

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
