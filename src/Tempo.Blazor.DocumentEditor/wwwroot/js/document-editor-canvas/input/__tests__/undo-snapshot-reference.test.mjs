import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasInputController } from '../input-controller.mjs';
import { canvasBlockText } from '../text-editing.mjs';
import { createCanvasHistoryController } from '../../history/history-controller.mjs';

// Phase N5 (canvas perf 2026-07-10): the model is copy-on-write — the pre-edit model OBJECT already
// IS the undo snapshot, so neither the input hot path (applyEdit) nor the history record may deep
// clone the whole document per keystroke. These tests cement the reference semantics end to end:
// input commit -> history record -> undo -> edit-after-undo without corrupting older entries.

function createInputModel(text) {
    return {
        documentId: 'n5-undo-reference',
        version: 0,
        body: {
            blocks: [{
                id: 'typing-body',
                sectionId: 'section-1',
                type: 'paragraph',
                order: 10,
                paragraphProperties: {},
                content: { type: 'paragraph', runs: [{ id: 'typing-body-run', type: 'text', text, marks: [] }] },
            }],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function collapsed(blockId, offset) {
    return { anchor: { blockId, offset }, focus: { blockId, offset } };
}

function createHarness(initialText) {
    const state = {
        model: createInputModel(initialText),
        selection: collapsed('typing-body', initialText.length),
        commits: [],
    };
    const history = createCanvasHistoryController();
    const bridge = {
        input: { value: '', addEventListener() {}, removeEventListener() {} },
        beforeInputListener: null,
        subscribe(listener) {
            bridge.beforeInputListener = listener;
            return () => { bridge.beforeInputListener = null; };
        },
    };
    const controller = createCanvasInputController({
        inputBridge: bridge,
        selectionController: {
            getSelection: () => state.selection,
            setSelection: next => { state.selection = next; },
            setCompositionRange() {},
        },
        getModel: () => state.model,
        commit(change) {
            state.model = change.model;
            state.selection = change.selection;
            state.commits.push(change);
            // Mirrors flushPendingTextInputSideEffects: the change (incl. `before`) feeds the
            // history record on the input path.
            history.recordTextInput({
                ...change,
                after: { model: change.model, selection: change.selection },
            });
            return { ok: true };
        },
        now: (() => { let tick = 0; return () => ++tick; })(),
    });
    controller.mount();
    const type = (character) => bridge.beforeInputListener(
        { inputType: 'insertText', data: character },
        { preventDefault() {} });
    return { state, history, controller, type };
}

test('input commit hands the pre-edit model to history BY REFERENCE (no per-keystroke deep clone)', () => {
    const { state, history, controller, type } = createHarness('Hello');
    const preEditModel = state.model;

    type('!');
    assert.equal(canvasBlockText(state.model, 'typing-body'), 'Hello!');

    assert.strictEqual(state.commits[0].before.model, preEditModel,
        'applyEdit must pass the copy-on-write snapshot by reference, not a structuredClone');

    const transaction = history.undo();
    assert.strictEqual(transaction.before.model, preEditModel,
        'history.recordTextInput on the input path must not re-clone the immutable snapshot');
    controller.destroy();
});

test('edit -> undo restores a byte-identical model', () => {
    const { state, history, controller, type } = createHarness('Hello');
    const original = JSON.stringify(state.model);

    type('x');
    const transaction = history.undo();
    assert.equal(JSON.stringify(transaction.before.model), original,
        'undo snapshot must be byte-identical to the pre-edit model');
    controller.destroy();
});

test('a second edit after undo does not corrupt older history entries', () => {
    const { state, history, controller, type } = createHarness('base');

    type('A');
    const afterFirst = JSON.stringify(state.model);

    // Undo (as the dispatcher does: restore the transaction's before model by reference).
    const transaction = history.undo();
    state.model = transaction.before.model;
    state.selection = transaction.before.selection || collapsed('typing-body', 4);
    assert.equal(canvasBlockText(state.model, 'typing-body'), 'base');

    // Type again from the restored state — copy-on-write must leave the redo snapshot intact.
    type('B');
    assert.equal(canvasBlockText(state.model, 'typing-body'), 'baseB');
    assert.equal(JSON.stringify(transaction.after.model), afterFirst,
        'the earlier transaction snapshot must not be corroded by later edits');
    assert.equal(canvasBlockText(transaction.before.model, 'typing-body'), 'base',
        'the restored snapshot itself must stay untouched');
    controller.destroy();
});

// Fáze 20 (code review N5): cloneSnapshots:false dropped the deep clone for the whole change, and
// the clone was re-added only for before.selection. after.selection stayed the LIVE result
// selection that the selection controller normalizes/mutates in place — redo then restored a
// mutated caret position. The history record must clone after.selection symmetrically.
test('history after.selection is immune to later in-place mutation of the live selection (Fáze 20)', () => {
    const { state, history, controller, type } = createHarness('Hi');

    type('!');
    const committedFocusOffset = state.selection.focus.offset;

    // The selection controller may normalize/mutate the live selection object in place after commit.
    state.selection.focus.offset = 999;
    state.selection.anchor.offset = 999;

    const transaction = history.undo();
    assert.equal(transaction.after.selection.focus.offset, committedFocusOffset,
        'redo selection must reflect the committed caret, not later in-place mutations');
    assert.notStrictEqual(transaction.after.selection, state.selection,
        'history must hold its own after.selection snapshot, not the live object');
    controller.destroy();
});

test('typing coalescing keeps reference snapshots intact across merged transactions', () => {
    const { state, history, controller, type } = createHarness('t');
    const preEditModel = state.model;

    type('a');
    type('b');
    type('c');
    assert.equal(canvasBlockText(state.model, 'typing-body'), 'tabc');

    const undone = history.undo();
    assert.equal(canvasBlockText(undone.after.model, 'typing-body'), 'tabc',
        'coalesced transaction ends at the final typed state');
    assert.strictEqual(undone.before.model, preEditModel,
        'coalesced transaction keeps the FIRST pre-edit snapshot by reference');
    controller.destroy();
});
