import assert from 'node:assert/strict';
import test from 'node:test';
import {
    addCommentToCanvasModel,
    buildCommentMarkers,
    buildCommentRailItems,
    createCanvasCommentOverlay,
    deleteCommentFromCanvasModel,
    selectionForComment,
} from '../comment-overlay.mjs';

test('comment overlay builds readable markers from inline comment marks and selects a thread', () => {
    const render = createRender();
    const model = {
        comments: [{
            id: 'comment-a',
            status: 'Open',
            anchor: { type: 'TextRange', blockId: 'p1', startOffset: 2, endOffset: 7 },
        }],
    };

    const markers = buildCommentMarkers(model, render, { selectedCommentId: 'comment-a' });

    assert.equal(markers.length, 1);
    assert.equal(markers[0].commentId, 'comment-a');
    assert.equal(markers[0].selected, true);
    assert.equal(markers[0].rect.width, 40);
    assert.equal(markers[0].status, 'open');
});

test('comment overlay renders rail-synchronized marker elements', () => {
    const document = createFakeDocument();
    const parent = document.createElement('div');
    const overlay = createCanvasCommentOverlay({ document }).mount(parent);

    overlay.update({
        comments: [{
            id: 'comment-b',
            status: 'Resolved',
            anchor: { type: 'TextRange', blockId: 'p1', startOffset: 0, endOffset: 4 },
        }],
    }, createAnchorRender());
    const selected = overlay.select('comment-b');

    assert.equal(overlay.snapshot().markerCount, 1);
    assert.equal(parent.children[0], overlay.root);
    assert.equal(overlay.root.children[0].getAttribute('data-testid'), 'document-canvas-comment-marker');
    assert.equal(overlay.root.children[0].getAttribute('data-canvas-comment-status'), 'resolved');
    assert.equal(selected.commentId, 'comment-b');
});

test('add comment from selection creates an anchor mark, rail item, highlight, and caret selection', () => {
    const model = createEditableCommentModel();
    const selection = {
        anchor: { blockId: 'p1', offset: 7 },
        focus: { blockId: 'p1', offset: 15 },
    };

    const result = addCommentToCanvasModel(model, selection, {
        comment: {
            id: 'comment-created',
            status: 'Open',
            entries: [{ id: 'entry-created', text: 'Review this clause' }],
        },
    });

    assert.equal(result.changed, true);
    assert.equal(result.commentId, 'comment-created');
    assert.deepEqual(result.selection, {
        anchor: { blockId: 'p1', offset: 7 },
        focus: { blockId: 'p1', offset: 7 },
    });
    assert.equal(result.model.comments.length, 1);
    assert.equal(result.model.body.blocks[0].content.runs.some(run =>
        run.marks?.some(mark => mark.type === 'commentAnchor' && mark.commentAnchor.commentId === 'comment-created')), true);

    const render = createCommentRenderFromModel(result.model);
    const markers = buildCommentMarkers(result.model, render, { selectedCommentId: 'comment-created' });
    const rail = buildCommentRailItems(result.model, { selectedCommentId: 'comment-created' });
    assert.equal(markers.length, 1);
    assert.equal(markers[0].commentId, 'comment-created');
    assert.equal(markers[0].startOffset, 7);
    assert.equal(rail.length, 1);
    assert.equal(rail[0].selected, true);
    assert.equal(rail[0].previewText, 'Review this clause');
    assert.deepEqual(selectionForComment(result.model.comments[0]), result.selection);
});

test('delete comment removes thread and anchor marks from the canvas model', () => {
    const added = addCommentToCanvasModel(createEditableCommentModel(), {
        anchor: { blockId: 'p1', offset: 0 },
        focus: { blockId: 'p1', offset: 6 },
    }, {
        comment: {
            id: 'comment-delete',
            entries: [{ id: 'entry-delete', text: 'Remove me' }],
        },
    });

    const deleted = deleteCommentFromCanvasModel(added.model, 'comment-delete');

    assert.equal(deleted.changed, true);
    assert.equal(deleted.model.comments.length, 0);
    assert.equal(deleted.model.body.blocks[0].content.runs.some(run =>
        run.marks?.some(mark => mark.type === 'commentAnchor')), false);
});

function createRender() {
    return {
        displayList: {
            commands: [
                { id: 'p1-box', type: 'paragraphBox', blockId: 'p1', pageIndex: 0, x: 96, y: 96, width: 400, height: 24 },
                { id: 'run-a', type: 'textRun', blockId: 'p1', runId: 'run-a', pageIndex: 0, x: 100, y: 100, width: 40, height: 18, start: 0, end: 4 },
                { id: 'run-a-comment', type: 'commentAnchor', blockId: 'p1', runId: 'run-a', pageIndex: 0, x: 100, y: 100, width: 40, height: 18, commentId: 'comment-a' },
            ],
        },
    };
}

function createAnchorRender() {
    return {
        displayList: {
            commands: [
                { id: 'p1-box', type: 'paragraphBox', blockId: 'p1', pageIndex: 0, x: 96, y: 96, width: 400, height: 24 },
                { id: 'run-b', type: 'textRun', blockId: 'p1', runId: 'run-b', pageIndex: 0, x: 100, y: 100, width: 40, height: 18, start: 0, end: 4 },
            ],
        },
    };
}

function createEditableCommentModel() {
    return {
        documentId: 'comment-command-test',
        version: 1,
        body: {
            blocks: [{
                id: 'p1',
                sectionId: 's1',
                type: 'paragraph',
                order: 10,
                content: {
                    type: 'paragraph',
                    runs: [{ id: 'run-1', type: 'text', text: 'Please review this clause carefully.', marks: [] }],
                },
            }],
        },
        sections: [{
            id: 's1',
            order: 0,
            blocks: [{
                id: 'p1',
                sectionId: 's1',
                type: 'paragraph',
                order: 10,
                content: {
                    type: 'paragraph',
                    runs: [{ id: 'run-1', type: 'text', text: 'Please review this clause carefully.', marks: [] }],
                },
            }],
        }],
        comments: [],
    };
}

function createCommentRenderFromModel(model) {
    const commands = [];
    let x = 100;
    let offset = 0;
    for (const run of model.body.blocks[0].content.runs) {
        const width = Math.max(8, String(run.text || '').length * 6);
        const command = {
            id: run.id,
            type: 'textRun',
            blockId: 'p1',
            runId: run.id,
            pageIndex: 0,
            x,
            y: 100,
            width,
            height: 18,
            start: offset,
            end: offset + String(run.text || '').length,
        };
        commands.push(command);
        for (const mark of run.marks || []) {
            if (mark.type === 'commentAnchor') {
                commands.push({
                    ...command,
                    id: `${run.id}-comment`,
                    type: 'commentAnchor',
                    commentId: mark.commentAnchor.commentId,
                });
            }
        }
        x += width;
        offset = command.end;
    }

    return { displayList: { commands } };
}

function createFakeDocument() {
    return {
        createElement(tagName) {
            return new FakeElement(String(tagName).toUpperCase());
        },
    };
}

class FakeElement {
    constructor(tagName) {
        this.tagName = tagName;
        this.children = [];
        this.attributes = new Map();
        this.style = {};
        this.parentNode = null;
        this.className = '';
    }

    appendChild(child) {
        child.parentNode = this;
        this.children.push(child);
        return child;
    }

    replaceChildren(...children) {
        this.children = [];
        for (const child of children) {
            this.appendChild(child);
        }
    }

    setAttribute(name, value) {
        this.attributes.set(String(name), String(value));
    }

    getAttribute(name) {
        return this.attributes.get(String(name)) ?? null;
    }
}

test('comment overlay prefers the canvas-stack shared page placements (no per-marker offset reads)', () => {
    const document = createFakeDocument();
    const stack = {
        root: document.createElement('div'),
        // No `pages` map on purpose: the overlay must use the shared snapshot, which exists precisely so
        // markers do not read pageElement.offsetLeft/offsetTop (forced reflow) per marker per render.
        getPagePlacements: () => new Map([['0', { offsetX: 100, offsetY: 50, scale: 2 }]]),
    };
    const overlay = createCanvasCommentOverlay({ document }).mount(stack);

    overlay.update({
        comments: [{
            id: 'comment-place',
            status: 'Open',
            anchor: { type: 'TextRange', blockId: 'p1', startOffset: 0, endOffset: 4 },
        }],
    }, createAnchorRender());

    const marker = overlay.root.children[0];
    const rect = overlay.snapshot().markers[0].rect;
    assert.equal(marker.style.left, `${rect.x * 2 + 100}px`);
    assert.equal(marker.style.top, `${rect.y * 2 + 50}px`);
});
