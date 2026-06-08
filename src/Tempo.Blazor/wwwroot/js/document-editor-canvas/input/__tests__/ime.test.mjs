import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasInputController } from '../input-controller.mjs';
import { canvasBlockText } from '../text-editing.mjs';

test('IME composition preview lives in the canvas model and commit replaces the preview', () => {
    const input = createFakeInput();
    let model = createInputModel('Hi');
    let selection = collapsed('ime-body', 2);
    let compositionRange = null;
    const controller = createCanvasInputController({
        inputBridge: { input, subscribe: () => () => { } },
        selectionController: {
            getSelection: () => selection,
            setSelection: next => { selection = next; },
            setCompositionRange: range => { compositionRange = range; },
        },
        getModel: () => model,
        commit(change) {
            model = change.model;
            selection = change.selection;
            return { ok: true };
        },
        now: createClock(),
    }).mount();

    controller.handleCompositionStart();
    controller.handleCompositionUpdate('か');
    assert.equal(canvasBlockText(model, 'ime-body'), 'Hiか');
    assert.equal(compositionRange.anchor.offset, 2);
    assert.equal(compositionRange.focus.offset, 3);
    assert.equal(controller.getState().isComposing, true);

    controller.handleCompositionUpdate('かん');
    assert.equal(canvasBlockText(model, 'ime-body'), 'Hiかん');
    assert.equal(compositionRange.focus.offset, 4);

    controller.handleCompositionUpdate('感');
    assert.equal(canvasBlockText(model, 'ime-body'), 'Hi感');
    assert.equal(compositionRange.focus.offset, 3);

    controller.handleCompositionEnd('感じ');
    assert.equal(canvasBlockText(model, 'ime-body'), 'Hi感じ');
    assert.equal(compositionRange, null);
    assert.deepEqual(selection.focus, { blockId: 'ime-body', offset: 4 });
    assert.equal(controller.getState().isComposing, false);
});

test('empty IME composition end cancels the preview text', () => {
    const input = createFakeInput();
    let model = createInputModel('Ready');
    let selection = collapsed('ime-body', 5);
    let compositionRange = null;
    const controller = createCanvasInputController({
        inputBridge: { input, subscribe: () => () => { } },
        selectionController: {
            getSelection: () => selection,
            setSelection: next => { selection = next; },
            setCompositionRange: range => { compositionRange = range; },
        },
        getModel: () => model,
        commit(change) {
            model = change.model;
            selection = change.selection;
            return { ok: true };
        },
        now: createClock(),
    }).mount();

    controller.handleCompositionStart();
    controller.handleCompositionUpdate('仮');
    assert.equal(canvasBlockText(model, 'ime-body'), 'Ready仮');

    controller.handleCompositionEnd('');
    assert.equal(canvasBlockText(model, 'ime-body'), 'Ready');
    assert.equal(compositionRange, null);
    assert.deepEqual(selection.focus, { blockId: 'ime-body', offset: 5 });
});

function createInputModel(text) {
    return {
        documentId: 'phase-8-ime',
        version: 0,
        body: { blocks: [textBlock('ime-body', text, 10)] },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function textBlock(id, text, order) {
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

function collapsed(blockId, offset) {
    return {
        anchor: { blockId, offset },
        focus: { blockId, offset },
    };
}

function createFakeInput() {
    return {
        value: '',
        listeners: new Map(),
        addEventListener(type, listener) {
            this.listeners.set(type, listener);
        },
        removeEventListener(type) {
            this.listeners.delete(type);
        },
    };
}

function createClock() {
    let value = 0;
    return () => {
        value += 1;
        return value;
    };
}
