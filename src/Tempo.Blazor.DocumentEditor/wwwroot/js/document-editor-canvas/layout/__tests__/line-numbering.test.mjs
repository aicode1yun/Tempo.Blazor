import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { createLineNumberingState, lineNumbersForFragment } from '../line-numbering.mjs';

// Phase 9: line numbering for legal filings (číslování řádků v podání) — verifies the ordinal
// engine the canvas layout uses: continuous / per-page / per-section restarts, increments and
// the left-margin label placement (distanceFromText left of the text frame).

function section(overrides = {}) {
    return {
        id: 'filing-section',
        properties: {
            lineNumbering: {
                enabled: true,
                startAt: 1,
                increment: 1,
                distanceFromText: 18,
                restart: 'page',
                ...overrides,
            },
        },
    };
}

function fragment(blockId, pageIndex, lineCount) {
    return {
        blockId,
        pageIndex,
        lines: Array.from({ length: lineCount }, (_, index) => ({
            id: `${blockId}-line-${index}`,
            pageIndex,
            rect: { x: 90, y: 100 + index * 20, width: 400, height: 18 },
            baseline: 114 + index * 20,
        })),
    };
}

const page = { body: { x: 90, y: 96, width: 614, height: 900 } };

test('per-page restart numbers every line and starts again on the next page', () => {
    const state = createLineNumberingState();
    const model = {};

    const pageOne = lineNumbersForFragment(fragment('b1', 0, 3), section(), page, state, model, null);
    const pageTwo = lineNumbersForFragment(fragment('b2', 1, 2), section(), page, state, model, null);

    assert.deepEqual(pageOne.map(label => label.text), ['1', '2', '3']);
    assert.deepEqual(pageTwo.map(label => label.text), ['1', '2'], 'restart=page starts over on page 2');
});

test('continuous restart keeps counting across pages and fragments', () => {
    const state = createLineNumberingState();
    const model = {};
    const config = section({ restart: 'continuous' });

    const first = lineNumbersForFragment(fragment('b1', 0, 2), config, page, state, model, null);
    const second = lineNumbersForFragment(fragment('b2', 1, 2), config, page, state, model, null);

    assert.deepEqual([...first, ...second].map(label => label.text), ['1', '2', '3', '4']);
});

test('increment renders only every Nth ordinal (court format: every 5th line)', () => {
    const state = createLineNumberingState();
    const labels = lineNumbersForFragment(
        fragment('b1', 0, 10),
        section({ increment: 5, restart: 'continuous' }),
        page,
        state,
        {},
        null);

    // Ordinals advance by the increment; only multiples aligned to startAt render.
    assert.ok(labels.length >= 2);
    for (const label of labels) {
        assert.equal((Number(label.text) - 1) % 5, 0, `ordinal ${label.text} must align to the increment`);
    }
});

test('labels sit in the left margin, distanceFromText away from the text frame', () => {
    const state = createLineNumberingState();
    const [label] = lineNumbersForFragment(fragment('b1', 0, 1), section(), page, state, {}, null);

    assert.ok(label.x < page.body.x, 'the label must be left of the body frame');
    assert.ok(label.x + label.width <= page.body.x - 18 + 0.001, 'distanceFromText must separate label and text');
    assert.equal(label.pageIndex, 0);
    assert.ok(label.baseline > 0);
});

test('disabled line numbering produces no labels', () => {
    const state = createLineNumberingState();
    const labels = lineNumbersForFragment(
        fragment('b1', 0, 3),
        section({ enabled: false }),
        page,
        state,
        {},
        null);

    assert.deepEqual(labels, []);
});
