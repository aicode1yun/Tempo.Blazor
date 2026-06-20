import test from 'node:test';
import assert from 'node:assert/strict';
import { isDeliberateSelectionNotification } from './selection-cadence.mjs';

test('a placed (visible) range selection is deliberate -> notify .NET promptly', () => {
    const payload = { isVisible: true, reason: 'canvas-selection', selection: { isCollapsed: false } };
    assert.equal(isDeliberateSelectionNotification(payload), true);
});

test('an object selection (visible) is deliberate even when the text selection is collapsed', () => {
    const payload = { isVisible: true, reason: 'canvas-object-selection', selection: { isCollapsed: true } };
    assert.equal(isDeliberateSelectionNotification(payload), true);
});

test('an unplaced range selection is still deliberate (toolbar must reflect it)', () => {
    const payload = { isVisible: false, reason: 'canvas-selection-unplaced', selection: { isCollapsed: false } };
    assert.equal(isDeliberateSelectionNotification(payload), true);
});

test('a collapsed caret (typing / arrow navigation) is NOT deliberate -> debounce it', () => {
    const payload = { isVisible: false, reason: 'canvas-selection-collapsed', selection: { isCollapsed: true } };
    assert.equal(isDeliberateSelectionNotification(payload), false);
});

test('missing or malformed payloads default to the debounced (non-deliberate) path', () => {
    assert.equal(isDeliberateSelectionNotification(null), false);
    assert.equal(isDeliberateSelectionNotification(undefined), false);
    assert.equal(isDeliberateSelectionNotification('not-an-object'), false);
    assert.equal(isDeliberateSelectionNotification({}), false);
    assert.equal(isDeliberateSelectionNotification({ selection: null }), false);
});
