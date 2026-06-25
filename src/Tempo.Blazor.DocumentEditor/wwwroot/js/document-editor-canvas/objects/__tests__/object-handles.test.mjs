import assert from 'node:assert/strict';
import test from 'node:test';
import {
    cursorForObjectHandle,
    imageObjectAtPoint,
    objectResizeHandleAt,
    resizeRectFromHandle,
} from '../object-handles.mjs';
import { aabbOfRotatedRect } from '../image-render.mjs';

test('cursorForObjectHandle maps each handle to its drag affordance (P5)', () => {
    assert.equal(cursorForObjectHandle('nw'), 'nwse-resize');
    assert.equal(cursorForObjectHandle('se'), 'nwse-resize');
    assert.equal(cursorForObjectHandle('ne'), 'nesw-resize');
    assert.equal(cursorForObjectHandle('sw'), 'nesw-resize');
    assert.equal(cursorForObjectHandle('n'), 'ns-resize');
    assert.equal(cursorForObjectHandle('s'), 'ns-resize');
    assert.equal(cursorForObjectHandle('e'), 'ew-resize');
    assert.equal(cursorForObjectHandle('w'), 'ew-resize');
    assert.equal(cursorForObjectHandle('rotate'), 'grab');
    assert.equal(cursorForObjectHandle('connector-start'), 'crosshair');
    assert.equal(cursorForObjectHandle('connector-end'), 'crosshair');
    assert.equal(cursorForObjectHandle(''), 'move');
});

test('resizeRectFromHandle keeps the aspect ratio when locked and frees it on Shift (P5)', () => {
    const start = { x: 100, y: 100, width: 100, height: 50 }; // ratio 2:1

    const locked = resizeRectFromHandle(start, 'se', 40, 0, true);
    assert.equal(locked.width, 140);
    assert.equal(locked.height, 70, 'locked corner resize preserves the 2:1 ratio');

    const free = resizeRectFromHandle(start, 'se', 40, 0, false);
    assert.equal(free.width, 140);
    assert.equal(free.height, 50, 'unlocked (Shift) resize changes only the dragged dimension');
});

test('cursorForObjectHandle rotates the resize cursor with the object (P4)', () => {
    // No rotation reproduces the axis-aligned mapping.
    assert.equal(cursorForObjectHandle('e', 0), 'ew-resize');
    assert.equal(cursorForObjectHandle('n', 0), 'ns-resize');
    // A 90° rotation turns the east edge vertical and the north edge horizontal.
    assert.equal(cursorForObjectHandle('e', 90), 'ns-resize');
    assert.equal(cursorForObjectHandle('n', 90), 'ew-resize');
    // A 45° rotation lands a corner on a diagonal axis.
    assert.equal(cursorForObjectHandle('se', 45), 'ew-resize');
    // Rotate handle stays grab regardless of angle.
    assert.equal(cursorForObjectHandle('rotate', 90), 'grab');
});

test('objectResizeHandleAt hit-tests a rotated object in its local frame (P4)', () => {
    const layout = {
        blocks: [{
            type: 'image',
            objectId: 'img',
            blockId: 'img-block',
            pageIndex: 0,
            rect: { x: 100, y: 100, width: 200, height: 120 },
            rotation: 90,
            object: { objectId: 'img', rotation: 90 },
        }],
    };
    const selection = { objectId: 'img' };

    // The east handle (local centre (300,160)) appears at (200,260) after a 90° rotation about the centre
    // (200,160). Clicking that VISUAL position must grab the 'e' handle.
    const hit = objectResizeHandleAt(layout, selection, 0, 200, 260);
    assert.ok(hit, 'rotated handle is grabbable at its visual position');
    assert.equal(hit.handle, 'e');

    // The un-rotated 'e' position (300,160) now points off the rotated frame and must NOT grab the east handle.
    const stale = objectResizeHandleAt(layout, selection, 0, 300, 160);
    assert.notEqual(stale?.handle, 'e');
});

test('imageObjectAtPoint hit-tests a rotated image in its local frame (P4)', () => {
    const layout = {
        blocks: [{
            type: 'image',
            objectId: 'img',
            blockId: 'img-block',
            pageIndex: 0,
            sequence: 0,
            rect: { x: 100, y: 100, width: 200, height: 60 }, // wide, short
            rotation: 90,
            object: { objectId: 'img', rotation: 90, zIndex: 0 },
        }],
    };

    // Rotated 90°, the wide-short image occupies a tall-narrow visual footprint. A point above the centre
    // (200,180) — outside the axis-aligned rect (100..160 in y) but inside the rotated footprint — must hit.
    assert.equal(imageObjectAtPoint(layout, 0, 200, 180)?.objectId, 'img');
    // A point far to the right (outside the rotated footprint) must miss.
    assert.equal(imageObjectAtPoint(layout, 0, 295, 130), null);
});

test('aabbOfRotatedRect grows the bounding box for a rotated rect (P4)', () => {
    const square = aabbOfRotatedRect({ x: 100, y: 100, width: 100, height: 100 }, 0);
    assert.deepEqual(square, { x: 100, y: 100, width: 100, height: 100 });

    const rotated = aabbOfRotatedRect({ x: 100, y: 100, width: 100, height: 100 }, 45);
    const diagonal = Math.sqrt(2) * 100;
    assert.ok(Math.abs(rotated.width - diagonal) < 0.01, `45° square AABB width ≈ ${diagonal}`);
    assert.ok(Math.abs(rotated.height - diagonal) < 0.01);
    // Centre is preserved.
    assert.ok(Math.abs((rotated.x + rotated.width / 2) - 150) < 0.01);
});

test('objectResizeHandleAt gives the 8px handles a padded ~12px hit target (P5)', () => {
    const layout = {
        blocks: [{
            type: 'image',
            objectId: 'img',
            blockId: 'img-block',
            pageIndex: 0,
            sequence: 0,
            rect: { x: 100, y: 100, width: 200, height: 120 },
            object: { objectId: 'img' },
        }],
    };
    const selection = { objectId: 'img' };

    // The SE corner handle paints as an 8px square centred on (300,220) → visual rect 296..304. A point at
    // (295,215) is just OUTSIDE the 8px square but inside the padded hit target, so it must still grab 'se'.
    const padded = objectResizeHandleAt(layout, selection, 0, 295, 215);
    assert.ok(padded, 'point within the padded hit area grabs a handle');
    assert.equal(padded.handle, 'se');

    // A point well outside the padded target hits nothing.
    assert.equal(objectResizeHandleAt(layout, selection, 0, 285, 205), null);
});
