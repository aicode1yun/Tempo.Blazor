import assert from 'node:assert/strict';
import test from 'node:test';
import {
    IMAGE_SNAP_GRID,
    snapObjectMoveRect,
    snapObjectResizeRect,
} from '../image-move-snap.mjs';

test('object move snaps image edges to the canvas grid', () => {
    const result = snapObjectMoveRect(
        { x: 143, y: 151, width: 168, height: 96 },
        { layout: snapLayout(), pageIndex: 0, objectId: 'main-image' });

    assert.equal(IMAGE_SNAP_GRID, 8);
    assert.equal(result.snapped, true);
    assert.equal(result.rect.x, 144);
    assert.equal(result.rect.y, 152);
    assert.equal(result.x.guideType, 'grid');
    assert.equal(result.y.guideType, 'grid');
});

test('object move prefers nearby page and object guides before equal-distance grid guides', () => {
    const objectGuide = snapObjectMoveRect(
        { x: 300, y: 219, width: 160, height: 40 },
        { layout: snapLayout(), pageIndex: 0, objectId: 'main-image' });
    const bodyGuide = snapObjectMoveRect(
        { x: 97, y: 97, width: 160, height: 40 },
        { layout: snapLayout(), pageIndex: 0, objectId: 'main-image' });

    assert.equal(objectGuide.snapped, true);
    assert.equal(objectGuide.rect.x, 304);
    assert.equal(objectGuide.x.guideType, 'object-left');
    assert.equal(bodyGuide.rect.x, 96);
    assert.equal(bodyGuide.rect.y, 96);
    assert.equal(bodyGuide.x.guideType, 'body-left');
    assert.equal(bodyGuide.y.guideType, 'body-top');
});

test('object resize snaps southeast handle while preserving aspect ratio', () => {
    const result = snapObjectResizeRect(
        { x: 144, y: 152, width: 168, height: 96 },
        'se',
        56,
        34,
        true,
        { layout: snapLayout(), pageIndex: 0, objectId: 'main-image' });

    assert.equal(result.snapped, true);
    assert.equal(result.rect.x, 144);
    assert.equal(result.rect.y, 152);
    assert.equal(result.rect.width, 224);
    assert.equal(result.rect.height, 128);
    assert.equal(result.x.guideType, 'grid');
    assert.equal(result.y, null);
});

test('object resize with a vertical guide drives the paired horizontal size when aspect ratio is locked', () => {
    const result = snapObjectResizeRect(
        { x: 144, y: 152, width: 168, height: 96 },
        'se',
        55,
        34,
        true,
        { layout: snapLayout(), pageIndex: 0, objectId: 'main-image' });

    assert.equal(result.snapped, true);
    assert.equal(result.y.guide, 280);
    assert.equal(result.rect.height, 128);
    assert.equal(result.rect.width, 224);
    assert.equal(result.rect.x + result.rect.width, 368);
});

test('object snap can be disabled for precision pointer gestures', () => {
    const result = snapObjectMoveRect(
        { x: 143, y: 151, width: 168, height: 96 },
        { layout: snapLayout(), pageIndex: 0, objectId: 'main-image', enabled: false });

    assert.equal(result.snapped, false);
    assert.equal(result.rect.x, 143);
    assert.equal(result.rect.y, 151);
});

function snapLayout() {
    return {
        pages: [{
            index: 0,
            body: { x: 96, y: 96, width: 600, height: 760 },
        }],
        blocks: [{
            type: 'image',
            pageIndex: 0,
            blockId: 'peer-image-block',
            objectId: 'peer-image',
            rect: { x: 304, y: 208, width: 80, height: 48 },
        }],
    };
}
