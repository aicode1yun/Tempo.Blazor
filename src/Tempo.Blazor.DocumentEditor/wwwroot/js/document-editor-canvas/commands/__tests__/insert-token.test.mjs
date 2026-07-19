// Phase 9 (command-layer plan): the Blazor-side token menu inserts the selected token
// through the engine — insertToken creates a first-class token RUN at the caret (the
// model/renderer already support type 'token'; plain text would lose the pill semantics).
import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';
import { createCanvasRunText } from '../../layout/canvas-text-style.mjs';

function createModel() {
    return {
        documentId: 'phase-9-insert-token',
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
                    runs: [{ id: 'intro-run', type: 'text', text: 'Hello token world', marks: [] }],
                },
            }],
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

const caret = offset => ({ anchor: { blockId: 'intro', offset }, focus: { blockId: 'intro', offset } });
const tokenRuns = state => state.model.body.blocks[0].content.runs.filter(run => String(run?.type || '') === 'token');

test('insertToken inserts a token run at the caret with the catalog metadata', () => {
    const { runtime, state } = createRuntime(createModel(), caret(6));

    const result = runtime.execCommand('insertToken', {
        key: 'user.email',
        displayName: 'User e-mail',
        description: 'Recipient e-mail address',
        colorClass: 'tm-token--user',
        typeLabel: 'User',
    });

    assert.equal(result.handled, true, 'insertToken must be a registered command');
    assert.equal(result.result?.changed, true);
    const tokens = tokenRuns(state);
    assert.equal(tokens.length, 1, 'one token run must be inserted');
    assert.equal(tokens[0].token.key, 'user.email');
    assert.equal(tokens[0].token.displayName, 'User e-mail');
    assert.equal(tokens[0].token.typeLabel, 'User');
    assert.equal(tokens[0].token.colorClass, 'tm-token--user');
    assert.equal(createCanvasRunText(tokens[0]), 'User e-mail', 'the token renders by its display name');

    // The run splits the original text at the caret: 'Hello ' + [token] + 'token world'.
    const texts = state.model.body.blocks[0].content.runs.map(run => createCanvasRunText(run));
    assert.deepEqual(texts, ['Hello ', 'User e-mail', 'token world']);

    // Caret ends right after the inserted token.
    assert.equal(state.selection.focus.blockId, 'intro');
    assert.equal(state.selection.focus.offset, 6 + 'User e-mail'.length);
});

test('insertToken honours an explicit blockId/offset payload target', () => {
    const { runtime, state } = createRuntime(createModel(), caret(0));

    const result = runtime.execCommand('insertToken', { key: 'k', displayName: 'T', blockId: 'intro', offset: 17 });

    assert.equal(result.result?.changed, true);
    const texts = state.model.body.blocks[0].content.runs.map(run => createCanvasRunText(run));
    assert.deepEqual(texts, ['Hello token world', 'T'], 'the token must land at the requested end offset');
});

test('insertToken without a key is a handled no-op', () => {
    const { runtime, state } = createRuntime(createModel(), caret(3));

    const result = runtime.execCommand('insertToken', { displayName: 'nameless' });

    assert.equal(result.handled, true);
    assert.equal(result.result?.changed ?? false, false, 'a token without a key must not be inserted');
    assert.equal(tokenRuns(state).length, 0);
});

test('insertToken swaps in a cloned block so reference-memoized layout re-lays it out', () => {
    // Layout signatures are memoized BY BLOCK REFERENCE (immutable-model-contract tests):
    // an in-place run splice would keep the old reference and the canvas would never repaint.
    const model = createModel();
    const blockBefore = model.body.blocks[0];
    const { runtime, state } = createRuntime(model, caret(6));

    const result = runtime.execCommand('insertToken', { key: 'user.email', displayName: 'User e-mail' });

    assert.equal(result.result?.changed, true);
    assert.notStrictEqual(state.model, model, 'the model must be a new object reference (layout memoizes by model)');
    const blockAfter = state.model.body.blocks[0];
    assert.notStrictEqual(blockAfter, blockBefore, 'the mutated block must be a new object reference');
    assert.ok(blockAfter.content.runs.some(run => String(run?.type || '') === 'token'));
    assert.ok(!blockBefore.content.runs.some(run => String(run?.type || '') === 'token'),
        'the original block object must stay untouched');
    assert.deepEqual(result.result?.insertedRunIds?.length, 1, 'the inserted run id must be reported for structural rendering');
});

test('insertToken updates section block lists too (layout reads sections, not body)', () => {
    // buildSectionFlows lays out sections[].blocks when populated. After model normalization those
    // are SEPARATE objects from body.blocks (same ids) — the real seed documents hit this. An
    // insertToken that only touches body.blocks leaves the painted layout without the token.
    const model = createModel();
    model.sections = [{
        id: 'section-1',
        order: 0,
        blocks: [structuredClone(model.body.blocks[0])],
    }];
    const { runtime, state } = createRuntime(model, caret(6));

    const result = runtime.execCommand('insertToken', { key: 'user.email', displayName: 'User e-mail' });

    assert.equal(result.result?.changed, true);
    const sectionBlock = state.model.sections[0].blocks[0];
    assert.ok(sectionBlock.content.runs.some(run => String(run?.type || '') === 'token'),
        'the section block list must carry the token run for the paginator');
    assert.strictEqual(sectionBlock, state.model.body.blocks[0],
        'section and body must converge on the same block reference');
});

test('insertToken is undoable and redoable', () => {
    const { runtime, state } = createRuntime(createModel(), caret(6));
    runtime.execCommand('insertToken', { key: 'user.email', displayName: 'User e-mail' });
    assert.equal(tokenRuns(state).length, 1);

    const undone = runtime.execCommand('undo');
    assert.equal(undone.handled, true);
    assert.equal(tokenRuns(state).length, 0, 'undo must remove the token run');

    const redone = runtime.execCommand('redo');
    assert.equal(redone.handled, true);
    assert.equal(tokenRuns(state).length, 1, 'redo must restore the token run');
});
