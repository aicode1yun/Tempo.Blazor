import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { layoutCanvasDocument } from '../../layout/pagination.mjs';

// Phase 4 — updateImageLayout must round-trip a layout faithfully: changing one facet (rotation, or size)
// must NOT silently rewrite the object's reference frame (horizontal/vertical RelativeTo), its alignment, or
// materialise an explicit x/y on an alignment-positioned object. The previous createLayoutPayload hardcoded
// horizontalRelativeTo=2 / verticalRelativeTo=3 / horizontalAlignment=Left and forced x/y to 0 when absent,
// so e.g. rotating a right-aligned, page-relative image quietly moved it to the left.

test('updateImageLayout with only a rotation preserves the reference frame, alignment and absent x/y', () => {
    const { runtime, layoutOf } = mountImage();

    const result = runtime.execCommand('updateImageLayout', { objectId: 'img', rotation: 30 }).result;
    assert.equal(result.changed, true);

    const layout = layoutOf('img');
    assert.equal(layout.transform.rotation, 30, 'rotation applied');
    assert.equal(layout.position.horizontalRelativeTo, 0, 'horizontal frame (page) preserved, not forced to column');
    assert.equal(layout.position.verticalRelativeTo, 3, 'vertical frame (paragraph) preserved');
    assert.equal(layout.position.horizontalAlignment, 2, 'right alignment preserved, not forced to Left');
    assert.equal(layout.position.x, undefined, 'alignment-positioned image keeps no explicit x');
    assert.equal(layout.position.y, undefined, 'alignment-positioned image keeps no explicit y');
});

test('updateImageLayout with only a size change preserves rotation, frame and alignment', () => {
    const { runtime, layoutOf } = mountImage();
    runtime.execCommand('updateImageLayout', { objectId: 'img', rotation: 30 });

    const result = runtime.execCommand('updateImageLayout', { objectId: 'img', width: 200, height: 120 }).result;
    assert.equal(result.changed, true);

    const layout = layoutOf('img');
    assert.equal(layout.transform.width, 200, 'width applied');
    assert.equal(layout.transform.height, 120, 'height applied');
    assert.equal(layout.transform.rotation, 30, 'rotation preserved across a resize');
    assert.equal(layout.position.horizontalRelativeTo, 0, 'horizontal frame preserved across a resize');
    assert.equal(layout.position.horizontalAlignment, 2, 'alignment preserved across a resize');
    assert.equal(layout.position.x, undefined, 'resize does not materialise an explicit x');
});

test('updateImageLayout with an explicit move sets x/y but keeps the reference frame', () => {
    const { runtime, layoutOf } = mountImage();

    runtime.execCommand('updateImageLayout', { objectId: 'img', x: 50, y: 60 });

    const layout = layoutOf('img');
    assert.equal(layout.position.x, 50, 'explicit move sets x');
    assert.equal(layout.position.y, 60, 'explicit move sets y');
    assert.equal(layout.position.horizontalRelativeTo, 0, 'move keeps the horizontal frame');
    assert.equal(layout.position.verticalRelativeTo, 3, 'move keeps the vertical frame');
});

test('updateImageLayout with a keyboard delta nudges from the current offset, preserving the frame', () => {
    const { runtime, layoutOf } = mountImage({ x: 40, y: 70 });

    runtime.execCommand('updateImageLayout', { objectId: 'img', dx: 10, dy: 12 });

    const layout = layoutOf('img');
    assert.equal(layout.position.x, 50, 'dx nudges from the current x');
    assert.equal(layout.position.y, 82, 'dy nudges from the current y');
    assert.equal(layout.position.horizontalRelativeTo, 0, 'nudge keeps the horizontal frame');
    assert.equal(layout.position.horizontalAlignment, 2, 'nudge keeps the alignment');
});

test('P6: a drag delta moves the RESOLVED position of a paragraph-anchored image by exactly that delta', () => {
    // The image is Square-wrapped + paragraph-relative and anchored to a paragraph that flows well below the
    // page body top, so resolved = paragraphFlowY + storedY with paragraphFlowY ≫ bodyY. A drag must land it
    // under the pointer (resolved += delta), which only holds when the stored offset is NUDGED by the delta —
    // the old body-relative absolute offset would have displaced it by (paragraphFlowY − bodyY).
    let model = createWrapImageModel();
    let selection = textSelection();
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) { model = change.model; selection = change.selection; },
        history: createHistory(),
    });

    const before = resolvedImageRect(model, 'wrap-img');
    // Comfortably below the page body top (72) so resolved is paragraph-relative — the regime where the old
    // body-relative offset displaced the object instead of landing it under the pointer.
    assert.ok(before.y > 100, `image should flow below the body top (was ${before.y})`);

    const result = runtime.execCommand('updateImageLayout', { objectId: 'wrap-img', dx: 40, dy: 60 }).result;
    assert.equal(result.changed, true);

    const after = resolvedImageRect(model, 'wrap-img');
    assert.ok(Math.abs(after.y - (before.y + 60)) <= 0.5, `expected resolved y ${before.y + 60}, got ${after.y}`);
    assert.ok(Math.abs(after.x - (before.x + 40)) <= 0.5, `expected resolved x ${before.x + 40}, got ${after.x}`);
});

test('P4: layoutCanvasDocument carries an image rotation into the resolved object layout', () => {
    const model = createWrapImageModel();
    // Rotate the seed image directly in the model.
    model.body.blocks[1].content.runs[0].drawing.layout.transform.rotation = 30;

    const layout = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const block = (layout.blocks || []).find(item =>
        item.type === 'image' && String(item.objectId || item.object?.objectId || '') === 'wrap-img');
    assert.ok(block, 'resolved image block exists');
    assert.equal(Number(block.object?.rotation || 0), 30, 'the resolved object layout must carry the rotation');
});

function resolvedImageRect(model, objectId) {
    const layout = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const block = (layout.blocks || []).find(item =>
        item.type === 'image' && String(item.objectId || item.object?.objectId || '') === objectId);
    assert.ok(block?.rect, `resolved image rect for ${objectId} should exist`);
    return { x: Number(block.rect.x) || 0, y: Number(block.rect.y) || 0 };
}

function createWrapImageModel() {
    const longText = 'This intro paragraph carries enough descriptive contract language to push the wrapped '
        + 'image well below the page body top so the resolved position is paragraph-relative, not body-relative.';
    return {
        documentId: 'phase-5-wrap-image',
        body: {
            blocks: [
                { id: 'intro', type: 'paragraph', order: 1, content: { type: 'paragraph', runs: [{ id: 'intro-run', type: 'text', text: longText }] } },
                {
                    id: 'wrap-block',
                    type: 'paragraph',
                    order: 2,
                    content: {
                        type: 'paragraph',
                        runs: [{
                            id: 'wrap-run',
                            type: 'drawing',
                            drawing: {
                                objectId: 'wrap-img',
                                kind: 0,
                                source: 1,
                                assetId: 'asset-1',
                                size: { width: 150, height: 90, lockAspectRatio: true },
                                naturalSize: { width: 150, height: 90 },
                                layout: {
                                    kind: 1,
                                    anchor: { blockId: 'wrap-block', offset: 0, moveWithText: true },
                                    position: { horizontalRelativeTo: 0, verticalRelativeTo: 3, horizontalAlignment: 0, verticalAlignment: 1, x: 0, y: 0 },
                                    wrap: { mode: 1, distanceLeft: 12, distanceRight: 12, distanceTop: 8, distanceBottom: 8 },
                                    transform: { width: 150, height: 90, rotation: 0, lockAspectRatio: true },
                                    stacking: { zIndex: 0 },
                                },
                                metadata: {},
                            },
                        }],
                    },
                },
                { id: 'wrap-text', type: 'paragraph', order: 3, content: { type: 'paragraph', runs: [{ id: 'wrap-text-run', type: 'text', text: longText }] } },
            ],
        },
    };
}

function metrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, text.length * fontSize * 0.5),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}

function mountImage(positionOverrides = null) {
    let model = createImageModel(positionOverrides);
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
    return {
        runtime,
        layoutOf(objectId) {
            return drawingById(model, objectId).layout;
        },
    };
}

function createImageModel(positionOverrides) {
    const position = positionOverrides
        ? { horizontalRelativeTo: 0, verticalRelativeTo: 3, horizontalAlignment: 2, verticalAlignment: 1, ...positionOverrides }
        : { horizontalRelativeTo: 0, verticalRelativeTo: 3, horizontalAlignment: 2, verticalAlignment: 1 };
    return {
        documentId: 'phase-4-image-layout',
        body: {
            blocks: [
                {
                    id: 'anchor',
                    type: 'paragraph',
                    order: 1,
                    content: { type: 'paragraph', runs: [{ id: 'anchor-run', type: 'text', text: 'Anchor paragraph' }] },
                },
                {
                    id: 'img-block',
                    type: 'paragraph',
                    order: 2,
                    content: {
                        type: 'paragraph',
                        runs: [{
                            id: 'img-run',
                            type: 'drawing',
                            drawing: {
                                objectId: 'img',
                                kind: 0,
                                source: 1,
                                assetId: 'asset-1',
                                size: { width: 150, height: 90, lockAspectRatio: true },
                                naturalSize: { width: 150, height: 90 },
                                layout: {
                                    kind: 1,
                                    anchor: { blockId: 'anchor', offset: 0, moveWithText: true },
                                    position,
                                    wrap: { mode: 1, distanceLeft: 12, distanceRight: 12, distanceTop: 8, distanceBottom: 8 },
                                    transform: { width: 150, height: 90, rotation: 0, lockAspectRatio: true },
                                    stacking: { zIndex: 0 },
                                },
                                metadata: {},
                            },
                        }],
                    },
                },
            ],
        },
    };
}

function drawingById(model, objectId) {
    const drawing = (model.body.blocks || [])
        .flatMap(block => block.content?.runs || [])
        .find(run => run.drawing?.objectId === objectId)
        ?.drawing || null;
    assert.ok(drawing, `Drawing ${objectId} should exist.`);
    return drawing;
}

function textSelection() {
    return { anchor: { blockId: 'anchor', offset: 0 }, focus: { blockId: 'anchor', offset: 0 } };
}

function createHistory() {
    const undoStack = [];
    const redoStack = [];
    return {
        push(transaction) { undoStack.push(transaction); redoStack.length = 0; return this.snapshot(); },
        undo() { const t = undoStack.pop(); if (t) redoStack.push(t); return t || null; },
        redo() { const t = redoStack.pop(); if (t) undoStack.push(t); return t || null; },
        snapshot() { return { canUndo: undoStack.length > 0, canRedo: redoStack.length > 0, undoDepth: undoStack.length, redoDepth: redoStack.length }; },
    };
}
