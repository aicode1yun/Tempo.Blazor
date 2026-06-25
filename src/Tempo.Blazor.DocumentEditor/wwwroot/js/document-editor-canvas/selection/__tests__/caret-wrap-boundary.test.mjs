import assert from 'node:assert/strict';
import test from 'node:test';
import { moveCaretByKey } from '../../../document-editor/core-engine/caret.mjs';
import { caretStopAt, collectCaretStops } from '../../../document-editor/core-engine/hit-test.mjs';
import { normalizeSelectionLayout } from '../selection-controller.mjs';
import { createSelectionTestLayout } from './selection-test-helpers.mjs';

// B1 (UX fix 2026-06-11): at a soft-wrap boundary the same {blockId, offset} has TWO caret stops — the END
// of line N and the START of line N+1 (both affinity 'after', so affinity cannot disambiguate). The caret
// position must therefore carry the lineId, and the stop lookup must honour it; otherwise Home (which targets
// the line-start offset) resolves to the previous line's end and the caret jumps up a line.

function findWrapBoundary(selectionLayout, blockId = 'paragraph-1') {
    const byOffset = new Map();
    for (const stop of collectCaretStops(selectionLayout)) {
        if (stop.blockId !== blockId) continue;
        const offset = Number(stop.offset || 0) || 0;
        if (!byOffset.has(offset)) byOffset.set(offset, []);
        byOffset.get(offset).push(stop);
    }
    for (const [offset, stops] of byOffset) {
        if (new Set(stops.map(s => s.lineId)).size < 2) continue;
        const sorted = stops.slice().sort((a, b) => (Number(a.rect.y) || 0) - (Number(b.rect.y) || 0));
        return { offset, lineEnd: sorted[0], lineStart: sorted[sorted.length - 1] };
    }
    return null;
}

test('caretStopAt honours the lineId so a wrap-boundary offset resolves to the requested line', () => {
    const { layout } = createSelectionTestLayout();
    const selectionLayout = normalizeSelectionLayout(layout);
    const boundary = findWrapBoundary(selectionLayout);
    assert.ok(boundary, 'fixture must wrap so a boundary offset has two caret stops');
    assert.ok(boundary.lineStart.rect.y > boundary.lineEnd.rect.y, 'line-start stop sits below the line-end stop');

    const atStartLine = caretStopAt(selectionLayout, {
        blockId: boundary.lineStart.blockId,
        offset: boundary.offset,
        lineId: boundary.lineStart.lineId,
    });
    assert.equal(atStartLine.lineId, boundary.lineStart.lineId);
    assert.equal(Math.round(atStartLine.rect.y), Math.round(boundary.lineStart.rect.y), 'must resolve to the START of the next line');

    const atEndLine = caretStopAt(selectionLayout, {
        blockId: boundary.lineEnd.blockId,
        offset: boundary.offset,
        lineId: boundary.lineEnd.lineId,
    });
    assert.equal(Math.round(atEndLine.rect.y), Math.round(boundary.lineEnd.rect.y), 'the previous-line end is still reachable by lineId');
});

test('Home from a wrapped continuation line keeps the caret on that visual line', () => {
    const { layout } = createSelectionTestLayout();
    const selectionLayout = normalizeSelectionLayout(layout);
    const boundary = findWrapBoundary(selectionLayout);
    assert.ok(boundary, 'fixture must wrap');

    // A caret somewhere in the middle of the continuation line (line N+1).
    const continuationLineId = boundary.lineStart.lineId;
    const midStop = collectCaretStops(selectionLayout)
        .filter(stop => stop.lineId === continuationLineId)
        .sort((a, b) => (Number(a.rect.x) || 0) - (Number(b.rect.x) || 0))
        .find(stop => (Number(stop.offset) || 0) > boundary.offset);
    assert.ok(midStop, 'continuation line must have caret stops beyond its start');

    const home = moveCaretByKey(selectionLayout, {
        blockId: midStop.blockId,
        offset: Number(midStop.offset) || 0,
        lineId: continuationLineId,
    }, 'Home');

    assert.equal(home.offset, boundary.offset, 'Home lands on the line-start offset');
    assert.equal(home.lineId, continuationLineId, 'Home result carries the continuation line id');

    // The rendered caret rect must stay on the continuation line, NOT jump up to the previous line end.
    const rendered = caretStopAt(selectionLayout, home);
    assert.equal(Math.round(rendered.rect.y), Math.round(boundary.lineStart.rect.y), 'caret must stay on the wrapped line after Home');
    assert.ok(rendered.rect.x <= boundary.lineEnd.rect.x, 'caret moved to the start (left) of the line, not the previous line end (right)');
});

test('End from the first wrapped line stays on that line (regression guard)', () => {
    const { layout } = createSelectionTestLayout();
    const selectionLayout = normalizeSelectionLayout(layout);
    const boundary = findWrapBoundary(selectionLayout);
    assert.ok(boundary, 'fixture must wrap');

    const firstLineId = boundary.lineEnd.lineId;
    const end = moveCaretByKey(selectionLayout, { blockId: boundary.lineEnd.blockId, offset: 1, lineId: firstLineId }, 'End');
    const rendered = caretStopAt(selectionLayout, end);
    assert.equal(Math.round(rendered.rect.y), Math.round(boundary.lineEnd.rect.y), 'End stays on the first wrapped line');
});
