import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../display-list.mjs';

// Phase 4 (perf+rendering fix 2026-06-08): the per-block display-command cache must reuse the
// commands of every unchanged paragraph (so a keystroke re-assembles only the edited block) while
// always producing output byte-identical to a fresh, uncached build.

test('warm rebuild reuses every block command set and is byte-identical', () => {
    const layoutCache = new Map();
    const commandCache = new WeakMap();
    const model = buildModel(20);
    const layout = { pageSettings: model.pageSettings };
    const options = () => ({ fontMetrics: metrics(), layoutCache, commandCache });

    const cold = buildDisplayList(model, layout, options());
    assert.equal(cold.commandCacheStats.hits, 0, 'cold build has no command-cache hits');
    assert.ok(cold.commandCacheStats.misses >= 20, 'cold build assembles every paragraph');

    const warm = buildDisplayList(model, layout, options());
    assert.equal(warm.commandCacheStats.misses, 0, 'warm build re-assembles nothing');
    assert.equal(warm.commandCacheStats.hits, cold.commandCacheStats.misses, 'warm build reuses every paragraph');

    assert.equal(commandsJson(warm), commandsJson(cold), 'cached command output is identical');
});

test('editing one block re-assembles only that block and stays byte-identical to a fresh build', () => {
    const layoutCache = new Map();
    const commandCache = new WeakMap();
    const model = buildModel(20);
    const layout = { pageSettings: model.pageSettings };
    buildDisplayList(model, layout, { fontMetrics: metrics(), layoutCache, commandCache });

    const edited = structuredClone(model);
    // 'keeps' -> 'keepX': one glyph swap, identical width/length under the deterministic metrics.
    edited.body.blocks[12].content.runs[0].text =
        edited.body.blocks[12].content.runs[0].text.replace('keeps', 'keepX');

    const incremental = buildDisplayList(edited, layout, { fontMetrics: metrics(), layoutCache, commandCache });
    assert.equal(incremental.commandCacheStats.misses, 1, 'only the edited block re-assembles commands');
    assert.ok(incremental.commandCacheStats.hits >= 19, 'every other block reuses its commands');

    const fresh = buildDisplayList(edited, layout, { fontMetrics: metrics() });
    assert.equal(commandsJson(incremental), commandsJson(fresh), 'incremental command output equals a fresh build');
});

test('the command cache is a no-op (and still correct) when no cache is supplied', () => {
    const model = buildModel(8);
    const layout = { pageSettings: model.pageSettings };
    const a = buildDisplayList(model, layout, { fontMetrics: metrics() });
    const b = buildDisplayList(model, layout, { fontMetrics: metrics() });
    assert.equal(a.commandCacheStats.hits, 0);
    assert.equal(commandsJson(a), commandsJson(b));
});

function buildModel(count) {
    return {
        documentId: 'phase4-command-cache',
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

function commandsJson(displayList) {
    return JSON.stringify(displayList.commands);
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
