import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../dispatcher.mjs';
import { applyInlineFormatCommand, createInlineFormatState } from '../inline-format.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';

test('advanced character toggles apply mutually exclusive script and strike marks', () => {
    let result = applyInlineFormatCommand(createModel('H2O x2'), range(1, 2), 'subscript', null, createInlineFormatState());
    assert.equal(result.changed, true);
    assert.deepEqual(markTypesForText(result.model, 'format-body', '2'), ['subscript']);

    result = applyInlineFormatCommand(result.model, range(1, 2), 'superscript', null, result.state);
    assert.deepEqual(markTypesForText(result.model, 'format-body', '2'), ['superscript']);
    assert.equal(result.formattingState.commands.superscript.state, 'active');

    result = applyInlineFormatCommand(result.model, range(4, 6), 'doubleStrikethrough', null, result.state);
    assert.ok(result.model.body.blocks[0].content.runs.some(run => (run.marks || []).some(mark => mark.type === 'doubleStrikethrough')));
});

test('character spacing scale and kerning commands write value marks and clear formatting removes them', () => {
    let result = applyInlineFormatCommand(createModel('Spacing'), range(0, 7), 'setCharacterSpacing', { value: 2.5 }, createInlineFormatState());
    result = applyInlineFormatCommand(result.model, range(0, 7), 'setCharacterScale', { percent: 125 }, result.state);
    result = applyInlineFormatCommand(result.model, range(0, 7), 'toggleKerning', null, result.state);

    const marks = result.model.body.blocks[0].content.runs[0].marks;
    assert.equal(marks.find(mark => mark.type === 'characterSpacing').value, '2.5');
    assert.equal(marks.find(mark => mark.type === 'characterScale').value, '125');
    assert.equal(marks.find(mark => mark.type === 'kerning').value, 'false');

    const cleared = applyInlineFormatCommand(result.model, range(0, 7), 'clearCharacterFormatting', null, result.state);
    assert.deepEqual(cleared.model.body.blocks[0].content.runs[0].marks, []);
});

test('change case and font size step commands mutate selection through undoable runtime', () => {
    let model = createModel('phase e6 proof');
    let selection = range(0, 14);
    const history = createHistoryStore();
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history,
        commit(change) {
            model = change.model;
            selection = change.selection;
            return { ok: true };
        },
    });

    let result = runtime.execCommand('changeCase', { variant: 'titleCase' });
    assert.equal(result.handled, true);
    assert.equal(model.body.blocks[0].content.runs.map(run => run.text).join(''), 'Phase E6 Proof');

    result = runtime.execCommand('increaseFontSize');
    assert.equal(result.handled, true);
    assert.equal(runtime.queryCommand('fontSize').value, '12');

    runtime.execCommand('undo');
    assert.equal(runtime.queryCommand('fontSize').state, 'inactive');
    runtime.execCommand('undo');
    assert.equal(model.body.blocks[0].content.runs.map(run => run.text).join(''), 'phase e6 proof');
});

function markTypes(model, blockId) {
    return model.body.blocks
        .find(block => block.id === blockId)
        .content.runs
        .map(run => (run.marks || []).map(mark => mark.type));
}

function markTypesForText(model, blockId, text) {
    const run = model.body.blocks
        .find(block => block.id === blockId)
        .content.runs
        .find(candidate => candidate.text === text);
    return (run?.marks || []).map(mark => mark.type);
}

function createModel(text) {
    return {
        documentId: 'phase-e6-advanced-char',
        version: 0,
        body: {
            blocks: [{
                id: 'format-body',
                sectionId: 'section-1',
                type: 'paragraph',
                order: 10,
                paragraphProperties: {},
                content: {
                    type: 'paragraph',
                    runs: [{ id: 'format-body-run', type: 'text', text, marks: [] }],
                },
            }],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function range(start, end) {
    return {
        anchor: { blockId: 'format-body', offset: start },
        focus: { blockId: 'format-body', offset: end },
    };
}
