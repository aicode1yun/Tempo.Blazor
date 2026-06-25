import assert from 'node:assert/strict';
import test from 'node:test';
import { selectionRectsForRange } from '../../../document-editor/core-engine/selection-overlay.mjs';
import { normalizeSelectionLayout } from '../selection-controller.mjs';
import { classifyPointerGesture, shouldBeginDrag } from '../pointer-gestures.mjs';
import { createSelectionTestLayout } from './selection-test-helpers.mjs';

test('selection range creates one-line and multi-line highlight rectangles', () => {
    const { layout } = createSelectionTestLayout();
    const selectionLayout = normalizeSelectionLayout(layout);
    const oneLine = selectionRectsForRange(
        selectionLayout,
        { blockId: 'paragraph-1', offset: 0 },
        { blockId: 'paragraph-1', offset: 8 });
    const multiLine = selectionRectsForRange(
        selectionLayout,
        { blockId: 'paragraph-1', offset: 0 },
        { blockId: 'paragraph-1', offset: 96 });

    assert.equal(oneLine.length, 1);
    assert.ok(oneLine[0].rect.width > 0);
    assert.ok(multiLine.length >= 2);
    assert.ok(multiLine.every(item => item.rect.height > 0));
});

test('pointer gesture classifier covers caret, drag, word, paragraph, and shift extension', () => {
    assert.equal(classifyPointerGesture({ detail: 1 }, {}), 'caret');
    assert.equal(classifyPointerGesture({ detail: 2 }, {}), 'word');
    assert.equal(classifyPointerGesture({ detail: 3 }, {}), 'paragraph');
    assert.equal(classifyPointerGesture({ detail: 1, shiftKey: true }, { hasAnchor: true }), 'extend');
    assert.equal(shouldBeginDrag({ x: 10, y: 10 }, { x: 14, y: 12 }), true);
});
