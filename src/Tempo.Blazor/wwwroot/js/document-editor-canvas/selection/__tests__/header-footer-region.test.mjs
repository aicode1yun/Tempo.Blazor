import assert from 'node:assert/strict';
import test from 'node:test';
import { editableRegionForBlock } from '../selection-controller.mjs';

// B6 (UX fix 2026-06-11): the editable region of the caret's block drives the Header & Footer contextual tab
// + field-command enablement. A caret in a header/footer block reports 'Header'/'Footer'; body reports 'body'.

const model = {
    body: { blocks: [{ id: 'body-1', type: 'paragraph' }] },
    headersFooters: [
        { id: 'hf-header', type: 'Header', scope: 'Primary', blocks: [{ id: 'header-block' }] },
        { id: 'hf-footer', type: 'Footer', scope: 'Primary', blocks: [{ id: 'footer-block' }] },
    ],
    notes: [{ id: 'note-1', blocks: [{ id: 'note-block' }] }],
};

test('a header block reports the Header region', () => {
    const region = editableRegionForBlock(model, 'header-block');
    assert.equal(region.kind, 'headerFooter');
    assert.equal(region.region, 'Header');
});

test('a footer block reports the Footer region', () => {
    const region = editableRegionForBlock(model, 'footer-block');
    assert.equal(region.kind, 'headerFooter');
    assert.equal(region.region, 'Footer');
});

test('a body block reports the body region', () => {
    assert.equal(editableRegionForBlock(model, 'body-1').kind, 'body');
});

test('a note block reports the note region (not header/footer)', () => {
    assert.equal(editableRegionForBlock(model, 'note-block').kind, 'note');
});

test('an unknown block falls back to body', () => {
    assert.equal(editableRegionForBlock(model, 'missing').kind, 'body');
});
