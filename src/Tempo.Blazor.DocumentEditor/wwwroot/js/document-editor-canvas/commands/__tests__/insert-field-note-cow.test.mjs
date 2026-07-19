// Command-layer plan phase 10 (remaining-task fix): run-inserting field commands must follow the
// copy-on-write layout contract like insertToken — layout memoizes block signatures by object
// reference and the paginator reads sections[].blocks, so an in-place splice updates the model
// but the canvas never repaints (proven live for insertField before this fix).
import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';

function createModel() {
    return {
        documentId: 'phase-10-field-cow',
        version: 0,
        body: {
            blocks: [{
                id: 'intro',
                sectionId: 'section-1',
                type: 'paragraph',
                order: 10,
                paragraphProperties: {},
                content: {
                    type: 'paragraph',
                    runs: [{ id: 'intro-run', type: 'text', text: 'Hello field world', marks: [] }],
                },
            }],
        },
        notes: [],
        sections: [{ id: 'section-1', order: 0, blocks: [] }],
    };
}

function withSectionCopies(model) {
    // Mirror normalized seed documents: section lists hold SEPARATE objects with the same ids.
    model.sections[0].blocks = [structuredClone(model.body.blocks[0])];
    return model;
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

const caret = offset => ({ anchor: { blockId: 'intro', offset }, focus: { blockId: 'intro', offset } });

test('insertField swaps in a cloned block and updates section lists (copy-on-write)', () => {
    const model = withSectionCopies(createModel());
    const blockBefore = model.body.blocks[0];
    const { runtime, state } = createRuntime(model, caret(6));

    const result = runtime.execCommand('insertDateField', { displayText: '2026-07-19' });

    assert.equal(result.handled, true);
    assert.equal(result.result?.changed, true);
    assert.notStrictEqual(state.model, model, 'the model must be a new object reference');
    const blockAfter = state.model.body.blocks[0];
    assert.notStrictEqual(blockAfter, blockBefore, 'the mutated block must be a new object reference');
    assert.ok(blockAfter.content.runs.some(run => String(run?.type || '') === 'field'));
    assert.ok(!blockBefore.content.runs.some(run => String(run?.type || '') === 'field'),
        'the original block object must stay untouched');
    const sectionBlock = state.model.sections[0].blocks[0];
    assert.strictEqual(sectionBlock, blockAfter, 'section and body must converge on the same block reference');
    assert.equal(result.result?.insertedRunIds?.length, 1, 'inserted run id must be reported for structural rendering');
});

test('insertFootnote swaps in a cloned block and updates section lists (copy-on-write)', () => {
    const model = withSectionCopies(createModel());
    const blockBefore = model.body.blocks[0];
    const { runtime, state } = createRuntime(model, caret(6));

    const result = runtime.execCommand('insertFootnote', { text: 'A note body.' });

    assert.equal(result.handled, true);
    assert.equal(result.result?.changed, true);
    assert.notStrictEqual(state.model, model, 'the model must be a new object reference');
    const blockAfter = state.model.body.blocks[0];
    assert.notStrictEqual(blockAfter, blockBefore, 'the mutated block must be a new object reference');
    assert.ok(blockAfter.content.runs.some(run => String(run?.type || '') === 'noteReference'));
    assert.strictEqual(state.model.sections[0].blocks[0], blockAfter,
        'section and body must converge on the same block reference');
    assert.equal(state.model.notes.length, 1, 'the note body must be appended');
    assert.equal(result.result?.insertedRunIds?.length, 1, 'inserted run id must be reported for structural rendering');
});
