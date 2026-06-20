import assert from 'node:assert/strict';
import test from 'node:test';
import {
    applySigningFieldCommand,
    canonicalSigningFieldCommandId,
    isSigningFieldCommand,
} from '../signing-field-commands.mjs';
import { createCanvasCommandRuntime } from '../dispatcher.mjs';

// Signing field commands (plan S2.9/S2.9b): insert/update/remove a signing field run at the caret —
// in the body OR in a header/footer — and round-trip through history (undo/redo).

function baseModel() {
    return {
        documentId: 'signing-commands',
        body: { blocks: [{ id: 'p1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'r1', type: 'text', text: 'Signed: ', marks: [] }] } }] },
        sections: [{ id: 's1', blocks: [] }],
        headersFooters: [
            { id: 'footer-1', type: 1, scope: 0, blocks: [{ id: 'f1', type: 'paragraph', content: { type: 'paragraph', runs: [{ id: 'fr1', type: 'text', text: 'Footer ', marks: [] }] } }] },
        ],
    };
}

function caret(blockId, offset) {
    return { anchor: { blockId, offset }, focus: { blockId, offset } };
}

test('command id matching is alias-tolerant', () => {
    assert.equal(isSigningFieldCommand('insertSigningField'), true);
    assert.equal(isSigningFieldCommand('removesigningfield'), true);
    assert.equal(isSigningFieldCommand('bold'), false);
    assert.equal(canonicalSigningFieldCommandId('deleteSigningField'), 'removeSigningField');
});

test('insertSigningField inserts a signing field run at the body caret', () => {
    const result = applySigningFieldCommand(baseModel(), caret('p1', 8), 'insertSigningField', {
        fieldType: 'signature', submitterUuid: 'signer', required: true, label: 'Signature',
    });

    assert.equal(result.changed, true);
    const runs = result.model.body.blocks[0].content.runs;
    const field = runs.find(run => run.type === 'signingField');
    assert.ok(field, 'a signing field run is inserted');
    assert.equal(field.signingField.fieldType, 'signature');
    assert.equal(field.signingField.submitterUuid, 'signer');
    assert.equal(result.fieldUuid, field.signingField.uuid);
});

test('insertSigningField inserts into a footer block when the caret is in the footer', () => {
    const result = applySigningFieldCommand(baseModel(), caret('f1', 7), 'insertSigningField', {
        fieldType: 'initials', submitterUuid: 'signer',
    });

    assert.equal(result.changed, true);
    const bodyRuns = result.model.body.blocks[0].content.runs;
    assert.equal(bodyRuns.some(run => run.type === 'signingField'), false, 'the body is untouched');
    const footerRuns = result.model.headersFooters[0].blocks[0].content.runs;
    assert.ok(footerRuns.some(run => run.type === 'signingField'), 'the field lands in the footer');
});

test('updateSigningField merges properties by uuid', () => {
    const inserted = applySigningFieldCommand(baseModel(), caret('p1', 8), 'insertSigningField', { fieldType: 'text', submitterUuid: 'signer', label: 'Old' });
    const uuid = inserted.fieldUuid;

    const updated = applySigningFieldCommand(inserted.model, caret('p1', 8), 'updateSigningField', { uuid, label: 'New label', required: true });

    assert.equal(updated.changed, true);
    const field = updated.model.body.blocks[0].content.runs.find(run => run.signingField?.uuid === uuid);
    assert.equal(field.signingField.label, 'New label');
    assert.equal(field.signingField.required, true);
});

test('removeSigningField deletes the run by uuid', () => {
    const inserted = applySigningFieldCommand(baseModel(), caret('p1', 8), 'insertSigningField', { fieldType: 'text', submitterUuid: 'signer' });
    const uuid = inserted.fieldUuid;

    const removed = applySigningFieldCommand(inserted.model, caret('p1', 8), 'removeSigningField', { uuid });

    assert.equal(removed.changed, true);
    assert.equal(removed.model.body.blocks[0].content.runs.some(run => run.type === 'signingField'), false);
});

test('insert is undoable and redoable through the command runtime', () => {
    let current = baseModel();
    let selection = caret('p1', 8);
    const history = createHistory();
    const runtime = createCanvasCommandRuntime({
        getModel: () => current,
        getSelection: () => selection,
        history,
        commit(change) { current = change.model; selection = change.selection || selection; },
    });

    const inserted = runtime.execCommand('insertSigningField', { fieldType: 'signature', submitterUuid: 'signer' });
    assert.equal(inserted.handled, true);
    assert.equal(current.body.blocks[0].content.runs.some(run => run.type === 'signingField'), true);
    assert.equal(history.snapshot().canUndo, true);

    runtime.execCommand('undo');
    assert.equal(current.body.blocks[0].content.runs.some(run => run.type === 'signingField'), false, 'undo removes the field');

    runtime.execCommand('redo');
    assert.equal(current.body.blocks[0].content.runs.some(run => run.type === 'signingField'), true, 'redo restores the field');
});

function createHistory() {
    const undo = [];
    const redo = [];
    return {
        push(transaction) { undo.push(transaction); redo.length = 0; },
        undo() { const t = undo.pop(); if (t) redo.push(t); return t; },
        redo() { const t = redo.pop(); if (t) undo.push(t); return t; },
        snapshot() { return { canUndo: undo.length > 0, canRedo: redo.length > 0 }; },
    };
}
