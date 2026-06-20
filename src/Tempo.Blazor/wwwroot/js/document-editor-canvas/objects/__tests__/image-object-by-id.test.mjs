import assert from 'node:assert/strict';
import test from 'node:test';
import { imageObjectById } from '../object-handles.mjs';

// B3 (UX fix 2026-06-11): the engine resolves an object's selection info by id so it can be selected
// programmatically (the test/automation seam behind interop.selectObject), since a synthetic pointer click
// is unreliable on the full editor.

const layout = {
    blocks: [
        { type: 'paragraph', blockId: 'p1', rect: { x: 0, y: 0, width: 100, height: 20 } },
        {
            type: 'image',
            blockId: 'img-block',
            objectId: 'img-1',
            runId: 'run-1',
            pageIndex: 0,
            rect: { x: 40, y: 60, width: 120, height: 80 },
            object: { objectId: 'img-1', wrapMode: 'Square', rotation: 0, altText: 'Logo' },
        },
    ],
};

test('imageObjectById resolves the object selection info by objectId', () => {
    const info = imageObjectById(layout, 'img-1');
    assert.ok(info, 'must find the image by objectId');
    assert.equal(info.objectId, 'img-1');
    assert.equal(info.blockId, 'img-block');
    assert.equal(info.wrapMode, 'Square');
    assert.deepEqual(info.rect, { x: 40, y: 60, width: 120, height: 80 });
});

test('imageObjectById also resolves by the block id', () => {
    const info = imageObjectById(layout, 'img-block');
    assert.ok(info);
    assert.equal(info.objectId, 'img-1');
});

test('imageObjectById returns null for an unknown id or empty input', () => {
    assert.equal(imageObjectById(layout, 'missing'), null);
    assert.equal(imageObjectById(layout, ''), null);
    assert.equal(imageObjectById(null, 'img-1'), null);
});
