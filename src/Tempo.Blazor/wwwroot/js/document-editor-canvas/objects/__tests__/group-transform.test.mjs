import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';

test('group and ungroup commands preserve child ids and move children through group transform', () => {
    let model = createGroupModel();
    let selection = textSelection();
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    const grouped = runtime.execCommand('groupObjects', {
        objectId: 'e7-group',
        objectIds: ['shape-a', 'shape-b'],
    }).result;
    assert.equal(grouped.changed, true);

    const group = drawingById(model, 'e7-group');
    assert.equal(group.kind, 6);
    assert.deepEqual(group.group.childObjectIds, ['shape-a', 'shape-b']);
    assert.equal(drawingById(model, 'shape-a').metadata.groupId, 'e7-group');
    assert.equal(group.layout.position.x, 40);
    assert.equal(group.layout.position.y, 80);
    assert.equal(group.layout.transform.width, 260);
    assert.equal(group.layout.transform.height, 110);

    const beforeA = drawingRect('shape-a', model);
    const beforeB = drawingRect('shape-b', model);
    const moved = runtime.execCommand('updateImageLayout', {
        objectId: 'e7-group',
        x: 70,
        y: 120,
        width: 390,
        height: 165,
    }).result;
    assert.equal(moved.changed, true);
    const afterA = drawingRect('shape-a', model);
    const afterB = drawingRect('shape-b', model);
    assert.equal(afterA.x, 70);
    assert.equal(afterA.y, 120);
    assert.equal(afterA.width, 120);
    assert.equal(afterA.height, 90);
    assert.equal(afterB.x, 340);
    assert.equal(afterB.y, 202.5);
    assert.equal(afterB.width, 120);
    assert.equal(afterB.height, 82.5);

    assert.equal(runtime.execCommand('undo').result.changed, true);
    assert.deepEqual(drawingRect('shape-a', model), beforeA);
    assert.deepEqual(drawingRect('shape-b', model), beforeB);

    assert.equal(runtime.execCommand('redo').result.changed, true);
    assert.deepEqual(drawingRect('shape-a', model), afterA);
    assert.deepEqual(drawingRect('shape-b', model), afterB);

    const ungrouped = runtime.execCommand('ungroupObjects', { objectId: 'e7-group' }).result;
    assert.equal(ungrouped.changed, true);
    assert.equal(drawingByIdOrNull(model, 'e7-group'), null);
    assert.equal(drawingById(model, 'shape-a').metadata.groupId, undefined);
});

test('align and distribute drawing commands update explicit object sets deterministically', () => {
    let model = createGroupModel();
    let selection = textSelection();
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    assert.equal(runtime.execCommand('alignObjects', {
        objectIds: ['shape-a', 'shape-b', 'shape-c'],
        alignment: 'top',
    }).result.changed, true);
    assert.deepEqual(['shape-a', 'shape-b', 'shape-c'].map(id => drawingRect(id, model).y), [80, 80, 80]);

    assert.equal(runtime.execCommand('distributeObjects', {
        objectIds: ['shape-a', 'shape-b', 'shape-c'],
        axis: 'horizontal',
    }).result.changed, true);
    const centers = ['shape-a', 'shape-b', 'shape-c'].map(id => {
        const rect = drawingRect(id, model);
        return rect.x + rect.width / 2;
    });
    assert.equal(Math.round((centers[1] - centers[0]) * 1000) / 1000, Math.round((centers[2] - centers[1]) * 1000) / 1000);
});

test('group z-order commands propagate the wrapper delta to child drawings and undo exactly', () => {
    let model = createGroupModel();
    let selection = textSelection();
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    assert.equal(runtime.execCommand('groupObjects', {
        objectId: 'e7-group',
        objectIds: ['shape-a', 'shape-b'],
    }).result.changed, true);

    const before = {
        group: drawingRect('e7-group', model).zIndex,
        a: drawingRect('shape-a', model).zIndex,
        b: drawingRect('shape-b', model).zIndex,
    };

    const front = runtime.execCommand('setImageZOrder', {
        objectId: 'e7-group',
        direction: 'front',
    }).result;
    assert.equal(front.changed, true);

    const after = {
        group: drawingRect('e7-group', model).zIndex,
        a: drawingRect('shape-a', model).zIndex,
        b: drawingRect('shape-b', model).zIndex,
    };
    const delta = after.group - before.group;
    assert.ok(delta > 0);
    assert.equal(after.a, before.a + delta);
    assert.equal(after.b, before.b + delta);
    assert.ok(after.group > after.a);
    assert.ok(after.group > after.b);

    assert.equal(runtime.execCommand('undo').result.changed, true);
    assert.equal(drawingRect('e7-group', model).zIndex, before.group);
    assert.equal(drawingRect('shape-a', model).zIndex, before.a);
    assert.equal(drawingRect('shape-b', model).zIndex, before.b);

    assert.equal(runtime.execCommand('redo').result.changed, true);
    assert.equal(drawingRect('e7-group', model).zIndex, after.group);
    assert.equal(drawingRect('shape-a', model).zIndex, after.a);
    assert.equal(drawingRect('shape-b', model).zIndex, after.b);
});

test('nested group hierarchy propagates transform and z-order into descendant drawings', () => {
    let model = createGroupModel();
    let selection = textSelection();
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    assert.equal(runtime.execCommand('groupObjects', {
        objectId: 'e7-child-group',
        objectIds: ['shape-a', 'shape-b'],
    }).result.changed, true);
    assert.equal(runtime.execCommand('groupObjects', {
        objectId: 'e7-parent-group',
        objectIds: ['e7-child-group', 'shape-c'],
    }).result.changed, true);

    const beforeA = drawingRect('shape-a', model);
    const beforeChildGroup = drawingRect('e7-child-group', model);
    const beforeParentGroup = drawingRect('e7-parent-group', model);
    const moved = runtime.execCommand('updateImageLayout', {
        objectId: 'e7-parent-group',
        x: beforeParentGroup.x + 24,
        y: beforeParentGroup.y + 18,
        width: beforeParentGroup.width * 1.2,
        height: beforeParentGroup.height * 1.1,
    }).result;
    assert.equal(moved.changed, true);

    const afterA = drawingRect('shape-a', model);
    const afterChildGroup = drawingRect('e7-child-group', model);
    assert.ok(afterChildGroup.x > beforeChildGroup.x);
    assert.ok(afterChildGroup.y > beforeChildGroup.y);
    assert.ok(afterChildGroup.width > beforeChildGroup.width);
    assert.ok(afterA.x > beforeA.x);
    assert.ok(afterA.y > beforeA.y);
    assert.ok(afterA.width > beforeA.width);

    const zBefore = {
        parent: drawingRect('e7-parent-group', model).zIndex,
        child: drawingRect('e7-child-group', model).zIndex,
        a: drawingRect('shape-a', model).zIndex,
        b: drawingRect('shape-b', model).zIndex,
    };
    assert.equal(runtime.execCommand('setImageZOrder', {
        objectId: 'e7-parent-group',
        direction: 'front',
    }).result.changed, true);

    const zAfter = {
        parent: drawingRect('e7-parent-group', model).zIndex,
        child: drawingRect('e7-child-group', model).zIndex,
        a: drawingRect('shape-a', model).zIndex,
        b: drawingRect('shape-b', model).zIndex,
    };
    const delta = zAfter.parent - zBefore.parent;
    assert.ok(delta > 0);
    assert.equal(zAfter.child, zBefore.child + delta);
    assert.equal(zAfter.a, zBefore.a + delta);
    assert.equal(zAfter.b, zBefore.b + delta);
});

function createGroupModel() {
    return {
        documentId: 'e7-group-transform-test',
        body: {
            blocks: [
                paragraph('anchor', 'Drawing anchor'),
                drawingBlock('shape-a-block', 'shape-a', 40, 80, 80, 60, 1),
                drawingBlock('shape-b-block', 'shape-b', 220, 135, 80, 55, 2),
                drawingBlock('shape-c-block', 'shape-c', 420, 180, 90, 60, 3),
            ],
        },
    };
}

function paragraph(id, text) {
    return {
        id,
        type: 'paragraph',
        order: 1,
        content: {
            type: 'paragraph',
            runs: [{ id: `${id}-run`, type: 'text', text }],
        },
    };
}

function drawingBlock(blockId, objectId, x, y, width, height, order) {
    return {
        id: blockId,
        type: 'paragraph',
        order,
        content: {
            type: 'paragraph',
            runs: [{
                id: `${objectId}-run`,
                type: 'drawing',
                drawing: {
                    objectId,
                    kind: 1,
                    size: { width, height },
                    naturalSize: { width, height },
                    layout: {
                        kind: 1,
                        anchor: { blockId: 'anchor', offset: 0 },
                        position: { x, y },
                        wrap: { mode: 6 },
                        transform: { width, height, lockAspectRatio: false },
                        stacking: { zIndex: order },
                    },
                    shape: {
                        preset: 'rectangle',
                        fill: { color: '#dbeafe' },
                        stroke: { color: '#2563eb', width: 1.5 },
                    },
                    metadata: {},
                },
            }],
        },
    };
}

function drawingById(model, objectId) {
    const drawing = drawingByIdOrNull(model, objectId);
    assert.ok(drawing, `Drawing ${objectId} should exist.`);
    return drawing;
}

function drawingByIdOrNull(model, objectId) {
    return (model.body.blocks || [])
        .flatMap(block => block.content?.runs || [])
        .find(run => run.drawing?.objectId === objectId)
        ?.drawing || null;
}

function drawingRect(objectId, model) {
    const drawing = drawingById(model, objectId);
    const layout = drawing.layout || {};
    const transform = layout.transform || {};
    const position = layout.position || {};
    const stacking = layout.stacking || {};
    return {
        x: Number(position.x || 0) || 0,
        y: Number(position.y || 0) || 0,
        width: Number(transform.width || drawing.size?.width || 0) || 0,
        height: Number(transform.height || drawing.size?.height || 0) || 0,
        zIndex: Number(stacking.zIndex || 0) || 0,
    };
}

function textSelection() {
    return { anchor: { blockId: 'anchor', offset: 0 }, focus: { blockId: 'anchor', offset: 0 } };
}

function createHistory() {
    const undoStack = [];
    const redoStack = [];
    return {
        push(entry) {
            undoStack.push(entry);
            redoStack.length = 0;
        },
        undo() {
            const entry = undoStack.pop();
            if (entry) redoStack.push(entry);
            return entry || null;
        },
        redo() {
            const entry = redoStack.pop();
            if (entry) undoStack.push(entry);
            return entry || null;
        },
        snapshot() {
            return { undo: undoStack.length, redo: redoStack.length };
        },
    };
}
