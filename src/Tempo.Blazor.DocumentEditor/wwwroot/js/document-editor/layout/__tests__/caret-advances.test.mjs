import assert from 'node:assert/strict';
import test from 'node:test';
import { createFontMetricsService } from '../font-metrics.mjs';

// Phase N4.1-N4.3 (canvas perf 2026-07-10): caret x-positions need the width of EVERY character
// prefix of a segment (shaped advance). Measuring each prefix ad hoc was O(n²) per keystroke and
// flooded the shared measurement LRU with one-shot prefix keys. `getCaretAdvances(text, style)`
// returns the whole prefix-width array at once, cached in its own LRU, byte-identical (in the
// default 'exact' mode) to the per-prefix `measureText(...).width` values the line-breaker used.

const STYLE = { fontFamily: 'Arial', fontSize: 16 };

function service(options) {
    return createFontMetricsService({ createMeasureContext: () => null, ...options });
}

test('exact advances are byte-identical to per-prefix measureText widths', () => {
    const metrics = service();
    const text = 'Hello wide world';
    const advances = metrics.getCaretAdvances(text, STYLE);

    assert.equal(advances.length, text.length + 1);
    assert.equal(advances[0], 0, 'offset 0 sits at the segment start');
    for (let k = 1; k <= text.length; k += 1) {
        const expected = metrics.measureText(text.slice(0, k), STYLE).width;
        assert.strictEqual(advances[k], expected, `prefix ${k} must match measureText exactly`);
    }
});

test('advances are monotonic and the last equals the full segment width', () => {
    const metrics = service();
    for (const text of ['performance', 'a b  c   d', 'kočka žere ěščř', 'مرحبا بالعالم']) {
        const advances = metrics.getCaretAdvances(text, STYLE);
        for (let k = 1; k < advances.length; k += 1) {
            assert.ok(advances[k] >= advances[k - 1],
                `advances must be monotonic (${text} @ ${k}: ${advances[k]} < ${advances[k - 1]})`);
        }
        assert.strictEqual(advances[text.length], metrics.measureText(text, STYLE).width,
            'last advance equals the measured segment width');
    }
});

test('repeated calls are served from the caret-advance cache (same frozen instance)', () => {
    const metrics = service();
    const first = metrics.getCaretAdvances('cached segment', STYLE);
    const second = metrics.getCaretAdvances('cached segment', STYLE);
    assert.strictEqual(second, first, 'second call must return the cached array');
    assert.ok(Object.isFrozen(first), 'cached array is frozen against caller mutation');
});

test('caret-advance cache is style-sensitive and LRU-capped', () => {
    const metrics = service({ caretAdvanceCacheLimit: 4 });
    const bold = metrics.getCaretAdvances('style', { ...STYLE, bold: true });
    const regular = metrics.getCaretAdvances('style', STYLE);
    assert.notStrictEqual(bold, regular, 'different styles have distinct entries');
    assert.ok(bold[5] > regular[5], 'bold synthetic widths are wider');

    // Overflow the tiny cache; the oldest entry must be evicted and rebuilt on next request.
    for (let index = 0; index < 4; index += 1) {
        metrics.getCaretAdvances(`filler-${index}`, STYLE);
    }
    assert.notStrictEqual(metrics.getCaretAdvances('style', { ...STYLE, bold: true }), bold,
        'evicted entry is rebuilt (new instance)');
});

test('prefix measurements do not flood the shared measurement LRU', () => {
    const metrics = service();
    const before = metrics.getStats().MeasureCacheSize;
    metrics.getCaretAdvances('a fairly long caret segment text', STYLE);
    const after = metrics.getStats().MeasureCacheSize;
    assert.ok(after - before <= 1,
        `caret prefixes must not populate the main measure cache (grew by ${after - before})`);
});

test('empty and single-character segments are handled', () => {
    const metrics = service();
    assert.deepEqual(Array.from(metrics.getCaretAdvances('', STYLE)), [0]);
    const single = metrics.getCaretAdvances('x', STYLE);
    assert.equal(single.length, 2);
    assert.strictEqual(single[1], metrics.measureText('x', STYLE).width);
});

// Fáze 23 (code review N4): chunked interpolační režim (caretAdvanceMode: 'chunked') byl smazán
// jako mrtvý kód — jediný odkaz mimo definici byl tento test. getCaretAdvances měří vždy exact
// (byte-identicky s historickou per-prefix cestou); ignorace neznámé option je kontrakt:
test('unknown caretAdvanceMode option is ignored — advances are always exact', () => {
    const exact = service();
    const withStaleOption = service({ caretAdvanceMode: 'chunked' });
    const text = 'The quick brown fox jumps over the lazy dog, twice around';

    assert.deepEqual(
        Array.from(withStaleOption.getCaretAdvances(text, STYLE)),
        Array.from(exact.getCaretAdvances(text, STYLE)),
        'stale option must not change the exact measurement');
});
