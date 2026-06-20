import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasHistoryController } from '../history-controller.mjs';
import { createModelStore } from '../../model/model-store.mjs';

test('text input undo and redo restore exact model and selection snapshots', () => {
    const history = createCanvasHistoryController({ now: createClock([0, 80, 160]) });
    let model = createModel('Hello', 0);
    let selection = collapsed(5);

    for (const text of [' ', 'w', 'o']) {
        const before = { model, selection };
        const after = insertText(model, selection, text);
        history.recordTextInput({
            before,
            after,
            model: after.model,
            selection: after.selection,
            edit: { type: 'insertText', text, source: 'insertText' },
            result: { changed: true, operation: 'insertText', dirtyBlockIds: ['history-body'] },
            input: { revision: after.model.version, dirtyBlockIds: ['history-body'] },
        });
        model = after.model;
        selection = after.selection;
    }

    assert.equal(history.snapshot().undoDepth, 2, 'space closes the first word group and the next letters coalesce');

    const secondWord = history.undo();
    assert.equal(text(secondWord.before.model), 'Hello ');
    assert.deepEqual(secondWord.before.selection, collapsed(6));

    const redone = history.redo();
    assert.equal(text(redone.after.model), 'Hello wo');
    assert.deepEqual(redone.after.selection, collapsed(8));
});

test('formatting, object-like, comment, and revision commands share the same transaction gate', () => {
    const history = createCanvasHistoryController();
    const baseModel = createModel('Review image table', 0);
    const selection = collapsed(6);
    const categories = [
        ['inline-format', 'bold'],
        ['table', 'insertTable'],
        ['image', 'insertImage'],
        ['comment', 'addComment'],
        ['revision', 'acceptRevision'],
    ];

    for (const [kind, commandId] of categories) {
        const before = { model: baseModel, selection };
        const after = {
            model: {
                ...baseModel,
                version: baseModel.version + history.snapshot().undoDepth + 1,
                [`${kind}Revision`]: history.snapshot().undoDepth + 1,
            },
            selection,
        };
        history.push({
            id: `phase12-${kind}`,
            kind,
            commandId,
            before,
            after,
            dirtyBlockIds: ['history-body'],
        });
    }

    assert.equal(history.snapshot().undoDepth, categories.length);

    for (const [expectedKind] of [...categories].reverse()) {
        const transaction = history.undo();
        assert.equal(transaction.kind, expectedKind);
        assert.equal(text(transaction.before.model), 'Review image table');
    }

    assert.equal(history.snapshot().redoDepth, categories.length);
    for (const [expectedKind] of categories) {
        const transaction = history.redo();
        assert.equal(transaction.kind, expectedKind);
        assert.equal(text(transaction.after.model), 'Review image table');
    }
});

test('model store preserves version zero so undo can clear dirty state at the saved baseline', () => {
    const store = createModelStore(createModel('Saved', 0));
    const savedVersion = store.getVersion();
    store.setModel(createModel('Saved dirty', 1));
    assert.notEqual(store.getVersion(), savedVersion);

    store.setModel(createModel('Saved', 0));
    assert.equal(store.getVersion(), savedVersion);
});

function createModel(value, version) {
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
                    runs: [{ id: 'history-body-run', type: 'text', text: value, marks: [] }],
                },
            }],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function insertText(model, selection, value) {
    const offset = Number(selection.focus.offset || 0);
    const existing = text(model);
    const nextText = `${existing.slice(0, offset)}${value}${existing.slice(offset)}`;
    const nextSelection = collapsed(offset + value.length);
    return {
        model: createModel(nextText, model.version + 1),
        selection: nextSelection,
    };
}

function text(model) {
    return model.body.blocks[0].content.runs.map(run => run.text).join('');
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
