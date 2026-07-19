// Phase 8 (command-layer plan): setProtectionMode enforcement. The C# ribbon toggled
// its own state and routed setProtectionMode, but the engine never registered the
// command, so model.isProtected/restrictedMarkers never changed and the (existing)
// text-edit veto plus overlay never engaged. Formatting commands additionally had no
// protection gate at all.
import assert from 'node:assert/strict';
import test from 'node:test';
import { applyProtectionMode, canEditRestrictedSelection } from '../restricted-editing.mjs';
import { applyCanvasTextEdit } from '../../input/text-editing.mjs';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';

function paragraph(id, text) {
    return {
        id,
        sectionId: 'section-1',
        type: 'paragraph',
        order: 10,
        paragraphProperties: {},
        content: {
            type: 'paragraph',
            runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }],
        },
    };
}

function createModel() {
    return {
        documentId: 'phase-8-protection',
        version: 0,
        body: {
            blocks: [
                paragraph('locked', 'Locked paragraph content'),
                paragraph('editable', 'Editable paragraph content'),
            ],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

const editableMarker = { startBlockId: 'editable', startOffset: 0, endBlockId: 'editable', endOffset: 26 };
const caret = (blockId, offset) => ({ anchor: { blockId, offset }, focus: { blockId, offset } });
const range = (blockId, start, end) => ({ anchor: { blockId, offset: start }, focus: { blockId, offset: end } });

test('applyProtectionMode stores isProtected and normalized markers on the model', () => {
    const result = applyProtectionMode(createModel(), { isProtected: true, markers: [editableMarker] });

    assert.equal(result.changed, true);
    assert.equal(result.model.isProtected, true);
    assert.equal(result.model.restrictedMarkers.length, 1);
    assert.equal(result.model.restrictedMarkers[0].startBlockId, 'editable');
    assert.ok(Number(result.model.version) > 0, 'the model version must bump so relayout and sync run');

    // Toggle off clears the protection state again.
    const off = applyProtectionMode(result.model, { isProtected: false, markers: [] });
    assert.equal(off.changed, true);
    assert.equal(off.model.isProtected, false);
    assert.equal(off.model.restrictedMarkers.length, 0);

    // Same state twice is a no-op.
    const again = applyProtectionMode(off.model, { isProtected: false, markers: [] });
    assert.equal(again.changed, false);
});

test('typing is vetoed outside the editable marker and allowed inside while protected', () => {
    const { model } = applyProtectionMode(createModel(), { isProtected: true, markers: [editableMarker] });

    const blocked = applyCanvasTextEdit(model, caret('locked', 5), { type: 'insertText', text: 'X' });
    assert.equal(blocked.changed, false, 'typing in the locked paragraph must be blocked');
    assert.equal(blocked.protected, true);

    const allowed = applyCanvasTextEdit(model, caret('editable', 5), { type: 'insertText', text: 'X' });
    assert.equal(allowed.changed, true, 'typing inside the editable marker must pass');

    const deletion = applyCanvasTextEdit(model, caret('locked', 5), { type: 'deleteBackward' });
    assert.equal(deletion.changed, false, 'deletion in the locked paragraph must be blocked');
});

test('disabling protection allows everything again', () => {
    const protectedModel = applyProtectionMode(createModel(), { isProtected: true, markers: [editableMarker] }).model;
    const unprotectedModel = applyProtectionMode(protectedModel, { isProtected: false, markers: [] }).model;

    const edit = applyCanvasTextEdit(unprotectedModel, caret('locked', 5), { type: 'insertText', text: 'X' });
    assert.equal(edit.changed, true);
});

test('markers spanning multiple blocks admit edits at the marker boundaries', () => {
    const model = createModel();
    model.body.blocks.push(paragraph('third', 'Third paragraph'));
    const spanning = { startBlockId: 'locked', startOffset: 3, endBlockId: 'editable', endOffset: 10 };
    const { model: protectedModel } = applyProtectionMode(model, { isProtected: true, markers: [spanning] });

    // Whole-range selection matching the marker is editable; a block outside it is not.
    const across = canEditRestrictedSelection(protectedModel, {
        anchor: { blockId: 'locked', offset: 3 },
        focus: { blockId: 'editable', offset: 10 },
    });
    assert.equal(across.allowed, true, 'the exact multi-block marker range must be editable');

    const outside = canEditRestrictedSelection(protectedModel, caret('third', 2));
    assert.equal(outside.allowed, false, 'a block outside the marker must stay locked');
});

test('formatting commands are vetoed outside the editable marker while protected', () => {
    const state = { model: applyProtectionMode(createModel(), { isProtected: true, markers: [editableMarker] }).model, selection: range('locked', 0, 6) };
    const runtime = createCanvasCommandRuntime({
        getModel: () => state.model,
        getSelection: () => state.selection,
        history: createHistoryStore(),
        commit(change) {
            state.model = change.model;
            state.selection = change.selection ?? state.selection;
        },
    });

    const blocked = runtime.execCommand('bold');
    assert.equal(blocked.handled, true, 'bold stays a registered command');
    assert.equal(runtime.queryCommand('bold').state, 'inactive', 'bold must NOT apply in the locked paragraph');
    assert.ok(!state.model.body.blocks[0].content.runs.some(run => run.marks?.some(mark => mark.type === 'bold')),
        'no bold mark may be written into the locked paragraph');

    const alignBlocked = runtime.execCommand('align', 'center');
    assert.equal(alignBlocked.handled, true);
    assert.notEqual(state.model.body.blocks[0].paragraphProperties?.alignment, 1,
        'paragraph formatting must NOT apply in the locked paragraph');

    // Inside the editable marker both commands work.
    state.selection = range('editable', 0, 6);
    runtime.execCommand('bold');
    assert.ok(state.model.body.blocks[1].content.runs.some(run => run.marks?.some(mark => mark.type === 'bold')),
        'bold must apply inside the editable marker');
    void blocked;
});
