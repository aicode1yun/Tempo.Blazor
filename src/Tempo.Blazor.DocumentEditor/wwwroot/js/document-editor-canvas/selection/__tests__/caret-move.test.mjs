import assert from 'node:assert/strict';
import test from 'node:test';
import { moveCaretByKey } from '../../../document-editor/core-engine/caret.mjs';
import { blockText, moveWordPosition, normalizeSelectionLayout } from '../selection-controller.mjs';
import { createSelectionTestLayout } from './selection-test-helpers.mjs';

test('keyboard arrows move by grapheme clusters and preserve line-aware vertical movement', () => {
    const { layout, model } = createSelectionTestLayout('Hello e\u0301 family 👨‍👩‍👧 text wraps onto another line for vertical movement.');
    const selectionLayout = normalizeSelectionLayout(layout);
    const text = blockText(model, 'paragraph-1');
    const accentEnd = text.indexOf(' family');
    const accentStart = text.indexOf('e\u0301');
    const left = moveCaretByKey(selectionLayout, { blockId: 'paragraph-1', offset: accentEnd }, 'ArrowLeft', { text });
    const right = moveCaretByKey(selectionLayout, { blockId: 'paragraph-1', offset: accentStart }, 'ArrowRight', { text });
    const down = moveCaretByKey(selectionLayout, { blockId: 'paragraph-1', offset: 2 }, 'ArrowDown', { text });

    assert.equal(left.offset, accentStart);
    assert.equal(right.offset, accentEnd);
    assert.equal(down.blockId, 'paragraph-1');
    assert.ok(down.offset > 2);
});

test('Home End PageUp and PageDown resolve stable caret stops', () => {
    const { layout, model } = createSelectionTestLayout();
    const selectionLayout = normalizeSelectionLayout(layout);
    const text = blockText(model, 'paragraph-1');
    const end = moveCaretByKey(selectionLayout, { blockId: 'paragraph-1', offset: 4 }, 'End', { text });
    const home = moveCaretByKey(selectionLayout, end, 'Home', { text });
    const pageDown = moveCaretByKey(selectionLayout, home, 'PageDown', { text, pageLines: 3 });
    const pageUp = moveCaretByKey(selectionLayout, pageDown, 'PageUp', { text, pageLines: 3 });

    assert.ok(end.offset > home.offset);
    assert.equal(home.offset, 0);
    assert.ok(pageDown.offset >= home.offset);
    assert.equal(pageUp.blockId, 'paragraph-1');
});

test('Ctrl or Alt word movement jumps to word boundaries', () => {
    const { model } = createSelectionTestLayout('Alpha beta gamma');
    const right = moveWordPosition(model, { blockId: 'paragraph-1', offset: 0 }, 'ArrowRight');
    const left = moveWordPosition(model, { blockId: 'paragraph-1', offset: 9 }, 'ArrowLeft');

    assert.equal(right.offset, 5);
    assert.equal(left.offset, 6);
});
