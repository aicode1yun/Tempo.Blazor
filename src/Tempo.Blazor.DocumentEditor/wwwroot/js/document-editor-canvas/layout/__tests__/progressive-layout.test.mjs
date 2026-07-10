import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../pagination.mjs';

// Perf plan N11.1: layoutCanvasDocument accepts options.budget = { maxPages?, deadlineMs? } and
// returns { partial: true, resume } when the budget is exhausted; passing the resume token back
// continues the layout exactly where it stopped. The chunked concatenation must be byte-identical
// to a single full layout — pagination, numbering, line numbers, floats and sections included.

function layoutInChunks(model, { maxPagesStep = 2, layoutCache = undefined } = {}) {
    let result = layoutCanvasDocument(model, {
        fontMetrics: metrics(),
        layoutCache,
        budget: { maxPages: maxPagesStep },
    });
    let calls = 1;
    while (result.partial) {
        assert.ok(result.resume, 'a partial result must carry a resume token');
        assert.equal(result.layoutComplete, false);
        result = layoutCanvasDocument(model, {
            fontMetrics: metrics(),
            layoutCache,
            resume: result.resume,
            budget: { maxPages: (result.pages?.length || 0) + maxPagesStep },
        });
        calls += 1;
        assert.ok(calls < 500, 'chunked layout must terminate');
    }

    assert.equal(result.layoutComplete, true);
    return { result, calls };
}

function assertLayoutsIdentical(chunked, full, label) {
    assert.equal(JSON.stringify(chunked.blocks), JSON.stringify(full.blocks), `${label}: blocks identical`);
    assert.equal(JSON.stringify(chunked.pages), JSON.stringify(full.pages), `${label}: pages identical`);
    assert.equal(JSON.stringify(chunked.textRects), JSON.stringify(full.textRects), `${label}: text rects identical`);
    assert.equal(JSON.stringify(chunked.listLabels), JSON.stringify(full.listLabels), `${label}: list labels identical`);
    assert.equal(JSON.stringify(chunked.lineNumbers), JSON.stringify(full.lineNumbers), `${label}: line numbers identical`);
    assert.equal(JSON.stringify(chunked.objectLayouts), JSON.stringify(full.objectLayouts), `${label}: object layouts identical`);
}

test('a budgeted layout stops after the page budget and resumes to a byte-identical result', () => {
    const model = buildModel(120);
    const full = layoutCanvasDocument(model, { fontMetrics: metrics() });
    assert.ok(full.pages.length > 4, 'fixture must span several pages');
    assert.equal(full.layoutComplete, true, 'an unbudgeted layout reports completion');
    assert.equal(full.partial, false);

    const first = layoutCanvasDocument(model, { fontMetrics: metrics(), budget: { maxPages: 2 } });
    assert.equal(first.partial, true, 'exceeding the page budget must return a partial result');
    assert.ok(first.pages.length < full.pages.length, 'partial layout must not lay out every page');
    assert.ok(first.blocks.length > 0, 'partial layout must lay out the first pages');

    const { result: chunked, calls } = layoutInChunks(model, { maxPagesStep: 2 });
    assert.ok(calls > 2, 'the fixture must require several continuation calls');
    assertLayoutsIdentical(chunked, full, 'chunked vs full');
});

test('deadlineMs budget still makes progress (at least one block per call)', () => {
    const model = buildModel(30);
    const full = layoutCanvasDocument(model, { fontMetrics: metrics() });

    let result = layoutCanvasDocument(model, { fontMetrics: metrics(), budget: { deadlineMs: 0 } });
    let calls = 1;
    while (result.partial) {
        result = layoutCanvasDocument(model, {
            fontMetrics: metrics(),
            resume: result.resume,
            budget: { deadlineMs: 0 },
        });
        calls += 1;
        assert.ok(calls <= 40, 'zero deadline must still advance at least one block per call');
    }

    assertLayoutsIdentical(result, full, 'deadline-chunked vs full');
});

test('resume interacts with the layout cache: chunked warm result equals fresh full layout (N11.4)', () => {
    const model = buildModel(60);
    const cache = new Map();
    const { result: chunked } = layoutInChunks(model, { maxPagesStep: 2, layoutCache: cache });
    const fresh = layoutCanvasDocument(model, { fontMetrics: metrics() });
    assertLayoutsIdentical(chunked, fresh, 'chunked+cache vs full');

    // Cache pruning must only run on completion — every block laid out must still be cached.
    const warm = layoutCanvasDocument(model, { fontMetrics: metrics(), layoutCache: cache });
    assert.equal(warm.cacheStats.misses, 0, 'a warm full pass after chunked completion recomputes nothing');
});

test('a resume token from another model reference is ignored (edit invalidates resume)', () => {
    const model = buildModel(40);
    const first = layoutCanvasDocument(model, { fontMetrics: metrics(), budget: { maxPages: 1 } });
    assert.equal(first.partial, true);

    const edited = structuredClone(model);
    edited.body.blocks[0].content.runs[0].text = 'Edited lead paragraph text.';

    // Passing a stale resume with an edited model must fall back to a fresh (budgeted) layout
    // rather than continuing from state computed for a different model.
    const restarted = layoutCanvasDocument(edited, { fontMetrics: metrics(), resume: first.resume });
    const fresh = layoutCanvasDocument(edited, { fontMetrics: metrics() });
    assertLayoutsIdentical(restarted, fresh, 'stale-resume restart vs fresh');
});

test('list numbering continues across chunk boundaries', () => {
    const model = buildModel(80);
    // Turn every 4th paragraph into an ordered list item spanning the whole document.
    for (let index = 0; index < model.body.blocks.length; index += 4) {
        const block = model.body.blocks[index];
        block.type = 'list';
        block.content = {
            type: 'list',
            ordered: true,
            indentLevel: 0,
            runs: block.content.runs,
        };
    }

    const full = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const { result: chunked } = layoutInChunks(model, { maxPagesStep: 1 });
    assertLayoutsIdentical(chunked, full, 'list numbering chunked vs full');
});

test('section break edge case: chunk boundary at a next-page section break', () => {
    const model = buildModel(40);
    model.body.blocks.splice(10, 0, {
        id: 'sec-break',
        type: 'pagebreak',
        order: 10.5,
        content: { type: 'pageBreak', breakType: 'nextPage' },
    });

    const full = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const { result: chunked } = layoutInChunks(model, { maxPagesStep: 1 });
    assertLayoutsIdentical(chunked, full, 'section break chunked vs full');
});

test('table spanning a chunk boundary lays out identically', () => {
    const model = buildModel(50);
    model.body.blocks.splice(8, 0, buildTableBlock('tbl-progressive', 30));

    const full = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const { result: chunked } = layoutInChunks(model, { maxPagesStep: 1 });
    assertLayoutsIdentical(chunked, full, 'table chunked vs full');
});

test('floating object affecting later paragraphs survives the chunk boundary', () => {
    const model = buildModel(50);
    model.body.blocks.splice(2, 0, {
        id: 'float-img',
        type: 'image',
        order: 2.5,
        content: {
            type: 'image',
            image: {
                url: 'https://example.test/a.png',
                width: 180,
                height: 3000,
                layout: { mode: 'anchored', wrapMode: 'Square', offsetX: 12, offsetY: 6 },
            },
        },
    });

    const full = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const { result: chunked } = layoutInChunks(model, { maxPagesStep: 1 });
    assertLayoutsIdentical(chunked, full, 'floating object chunked vs full');
});

test('progress metadata reports laid vs total blocks', () => {
    const model = buildModel(60);
    const first = layoutCanvasDocument(model, { fontMetrics: metrics(), budget: { maxPages: 1 } });
    assert.equal(first.partial, true);
    assert.ok(first.progress, 'partial results must report progress');
    assert.ok(first.progress.laidBlockCount > 0);
    assert.ok(first.progress.laidBlockCount < first.progress.totalBlockCount);
    assert.equal(first.progress.totalBlockCount, 60);

    const full = layoutCanvasDocument(model, { fontMetrics: metrics() });
    assert.equal(full.progress.laidBlockCount, full.progress.totalBlockCount);
});

function buildTableBlock(id, rowCount) {
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
                rows: Array.from({ length: rowCount }, (_, row) => ({
                    id: `${id}-r${row}`,
                    cells: [
                        cell(`${id}-r${row}c1`, `Row ${row + 1} name with some descriptive text`),
                        cell(`${id}-r${row}c2`, `Row ${row + 1} value that wraps across lines in the cell`),
                    ],
                })),
            },
        },
    };
}

function buildModel(count) {
    return {
        documentId: 'n11-progressive',
        pageSettings: { width: 600, height: 900, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 8 },
        body: {
            blocks: Array.from({ length: count }, (_, index) => ({
                id: `p${index}`,
                type: 'paragraph',
                order: index + 1,
                paragraphProperties: { alignment: 'left', lineSpacing: 1.1 },
                content: {
                    type: 'paragraph',
                    runs: [{
                        id: `p${index}-run`,
                        type: 'text',
                        text: `Paragraph ${index + 1} carries deliberately long deterministic descriptive contract text that wraps across several visual lines to fill pages quickly for the progressive layout tests.`,
                        marks: [],
                    }],
                },
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
