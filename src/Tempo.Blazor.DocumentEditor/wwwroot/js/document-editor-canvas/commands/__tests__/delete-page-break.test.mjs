// Phase 6 (command-layer plan): the page-break context menu routes
// deletePageBreak {blockId} — the engine must remove the page-break block so the
// content flows back (insert worked, delete was a silent no-op).
import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';

function paragraph(id, text, order) {
    return {
        id,
        sectionId: 'section-1',
        type: 'paragraph',
        order,
        paragraphProperties: {},
        content: {
            type: 'paragraph',
            runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }],
        },
    };
}

function createModel() {
    return {
        documentId: 'phase-6-delete-page-break',
        version: 0,
        body: {
            blocks: [paragraph('intro', 'Intro', 10), paragraph('outro', 'Outro', 20)],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function createRuntime(initialModel, initialSelection) {
    const state = { model: initialModel, selection: initialSelection };
    const runtime = createCanvasCommandRuntime({
        getModel: () => state.model,
        getSelection: () => state.selection,
        history: createHistoryStore(),
        commit(change) {
            state.model = change.model;
            state.selection = change.selection ?? state.selection;
        },
    });
    return { runtime, state };
}

const caretInIntro = { anchor: { blockId: 'intro', offset: 5 }, focus: { blockId: 'intro', offset: 5 } };

function pageBreakBlocks(model) {
    return model.body.blocks.filter(block => String(block?.type || '').toLowerCase() === 'pagebreak');
}

test('insertPageBreak followed by deletePageBreak restores the original pagination blocks', () => {
    const { runtime, state } = createRuntime(createModel(), caretInIntro);
    const originalIds = state.model.body.blocks.map(block => block.id);

    const inserted = runtime.execCommand('insertPageBreak', { id: 'break-1', blockId: 'intro' });
    assert.equal(inserted.handled, true);
    assert.equal(pageBreakBlocks(state.model).length, 1, 'insertPageBreak must add a pageBreak block');

    const deleted = runtime.execCommand('deletePageBreak', { blockId: 'break-1' });
    assert.equal(deleted.handled, true, 'deletePageBreak must be a registered command');
    assert.equal(deleted.result?.changed, true);
    assert.equal(pageBreakBlocks(state.model).length, 0, 'the page break must be gone');
    assert.deepEqual(state.model.body.blocks.map(block => block.id), originalIds, 'body must match the pre-insert order');
});

test('deletePageBreak supports undo and redo', () => {
    const { runtime, state } = createRuntime(createModel(), caretInIntro);
    runtime.execCommand('insertPageBreak', { id: 'break-1', blockId: 'intro' });

    runtime.execCommand('deletePageBreak', { blockId: 'break-1' });
    assert.equal(pageBreakBlocks(state.model).length, 0);

    const undone = runtime.execCommand('undo');
    assert.equal(undone.handled, true);
    assert.equal(pageBreakBlocks(state.model).length, 1, 'undo must restore the page break');

    const redone = runtime.execCommand('redo');
    assert.equal(redone.handled, true);
    assert.equal(pageBreakBlocks(state.model).length, 0, 'redo must delete it again');
});

test('deletePageBreak without blockId falls back to the page break at or next to the caret', () => {
    const { runtime, state } = createRuntime(createModel(), caretInIntro);
    runtime.execCommand('insertPageBreak', { id: 'break-1', blockId: 'intro' });

    // Caret ON the page break block itself.
    state.selection = { anchor: { blockId: 'break-1', offset: 0 }, focus: { blockId: 'break-1', offset: 0 } };
    const deleted = runtime.execCommand('deletePageBreak');
    assert.equal(deleted.result?.changed, true, 'caret on the break must resolve it');
    assert.equal(pageBreakBlocks(state.model).length, 0);

    // Caret on the block ADJACENT to a page break.
    runtime.execCommand('insertPageBreak', { id: 'break-2', blockId: 'intro' });
    state.selection = caretInIntro;
    const adjacent = runtime.execCommand('deletePageBreak');
    assert.equal(adjacent.result?.changed, true, 'caret adjacent to the break must resolve it');
    assert.equal(pageBreakBlocks(state.model).length, 0);
});

test('deletePageBreak with no resolvable page break is a no-op', () => {
    const { runtime, state } = createRuntime(createModel(), caretInIntro);

    const result = runtime.execCommand('deletePageBreak', { blockId: 'missing' });
    assert.equal(result.handled, true, 'the command stays handled (registered)');
    assert.equal(result.result?.changed ?? false, false, 'nothing changes without a page break');
    assert.deepEqual(state.model.body.blocks.map(block => block.id), ['intro', 'outro']);
});
