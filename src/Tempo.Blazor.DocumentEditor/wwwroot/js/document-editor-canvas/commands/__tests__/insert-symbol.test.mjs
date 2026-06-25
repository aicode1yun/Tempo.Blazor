import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';
import { canvasBlockText } from '../../input/text-editing.mjs';

test('insert symbol commands insert special characters through undoable dispatcher transactions', () => {
    let model = createModel('');
    let selection = collapsed(0);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    assert.equal(runtime.execCommand('insertEmDash').result.insertedText, '—');
    assert.equal(runtime.execCommand('insertEnDash').result.insertedText, '–');
    assert.equal(runtime.execCommand('insertNonBreakingSpace').result.insertedText, '\u00A0');
    assert.equal(runtime.execCommand('insertOptionalHyphen').result.insertedText, '\u00AD');
    assert.equal(runtime.execCommand('insertEmoji', { emoji: '✓' }).result.insertedText, '✓');
    assert.equal(canvasBlockText(model, 'symbol-body'), '—–\u00A0\u00AD✓');
    assert.equal(runtime.queryCommand('insertEmDash').disabled, false);

    runtime.execCommand('undo');
    assert.equal(canvasBlockText(model, 'symbol-body'), '—–\u00A0\u00AD');

    runtime.execCommand('redo');
    assert.equal(canvasBlockText(model, 'symbol-body'), '—–\u00A0\u00AD✓');
});

function createModel(text) {
    return {
        documentId: 'phase-e10-symbols',
        version: 0,
        body: {
            blocks: [{
                id: 'symbol-body',
                sectionId: 'section-1',
                type: 'paragraph',
                order: 10,
                paragraphProperties: {},
                content: {
                    type: 'paragraph',
                    runs: [{ id: 'symbol-body-run', type: 'text', text, marks: [] }],
                },
            }],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function collapsed(offset) {
    return {
        anchor: { blockId: 'symbol-body', offset },
        focus: { blockId: 'symbol-body', offset },
    };
}
