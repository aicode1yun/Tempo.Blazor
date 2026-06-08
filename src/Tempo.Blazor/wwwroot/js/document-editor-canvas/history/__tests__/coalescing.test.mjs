import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasHistoryController } from '../history-controller.mjs';

test('typing coalesces within one word and closes at whitespace boundary', () => {
    const history = createCanvasHistoryController({ now: createClock([0, 120, 240, 360, 480, 600]) });
    let model = createModel('');
    let selection = collapsed(0);

    for (const letter of ['H', 'e', 'l', 'l', 'o', ' ']) {
        const before = { model, selection };
        const after = appendText(model, selection, letter);
        history.recordTextInput({
            before,
            after,
            model: after.model,
            selection: after.selection,
            edit: { type: 'insertText', text: letter, source: 'insertText' },
            result: { changed: true, operation: 'insertText', dirtyBlockIds: ['history-body'] },
            input: { revision: after.model.version, dirtyBlockIds: ['history-body'] },
        });
        model = after.model;
        selection = after.selection;
    }

    assert.equal(history.snapshot().undoDepth, 1);
    assert.equal(history.snapshot().lastTransaction.coalesced, true);

    const world = appendText(model, selection, 'w');
    history.recordTextInput({
        before: { model, selection },
        after: world,
        model: world.model,
        selection: world.selection,
        edit: { type: 'insertText', text: 'w', source: 'insertText' },
        result: { changed: true, operation: 'insertText', dirtyBlockIds: ['history-body'] },
        input: { revision: world.model.version, dirtyBlockIds: ['history-body'] },
    });

    assert.equal(history.snapshot().undoDepth, 2);
});

test('typing coalescing respects the configured time window and non-typing commands', () => {
    const history = createCanvasHistoryController({ now: createClock([0, 2000, 2100]) });
    let model = createModel('');
    let selection = collapsed(0);

    for (const letter of ['A', 'B']) {
        const before = { model, selection };
        const after = appendText(model, selection, letter);
        history.recordTextInput({
            before,
            after,
            model: after.model,
            selection: after.selection,
            edit: { type: 'insertText', text: letter, source: 'insertText' },
            result: { changed: true, operation: 'insertText', dirtyBlockIds: ['history-body'] },
            input: { revision: after.model.version, dirtyBlockIds: ['history-body'] },
        });
        model = after.model;
        selection = after.selection;
    }

    assert.equal(history.snapshot().undoDepth, 2);

    history.push({
        id: 'format-1',
        kind: 'inline-format',
        commandId: 'bold',
        before: { model, selection },
        after: { model: { ...model, version: model.version + 1 }, selection },
        dirtyBlockIds: ['history-body'],
    });

    const afterCommand = appendText(model, selection, 'C');
    history.recordTextInput({
        before: { model, selection },
        after: afterCommand,
        model: afterCommand.model,
        selection: afterCommand.selection,
        edit: { type: 'insertText', text: 'C', source: 'insertText' },
        result: { changed: true, operation: 'insertText', dirtyBlockIds: ['history-body'] },
        input: { revision: afterCommand.model.version, dirtyBlockIds: ['history-body'] },
    });

    assert.equal(history.snapshot().undoDepth, 4);
});

function createModel(text, version = 0) {
    return {
        documentId: 'phase-12-history',
        version,
        body: {
            blocks: [{
                id: 'history-body',
                sectionId: 'section-1',
                type: 'paragraph',
                order: 10,
                paragraphProperties: {},
                content: {
                    type: 'paragraph',
                    runs: [{ id: 'history-body-run', type: 'text', text, marks: [] }],
                },
            }],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function appendText(model, selection, text) {
    const beforeText = model.body.blocks[0].content.runs[0].text;
    const offset = Number(selection.focus.offset || 0);
    const nextText = `${beforeText.slice(0, offset)}${text}${beforeText.slice(offset)}`;
    const nextModel = createModel(nextText, model.version + 1);
    const nextSelection = collapsed(offset + text.length);
    return { model: nextModel, selection: nextSelection };
}

function collapsed(offset) {
    return {
        anchor: { blockId: 'history-body', offset },
        focus: { blockId: 'history-body', offset },
    };
}

function createClock(values) {
    let index = 0;
    return () => values[Math.min(index++, values.length - 1)];
}
