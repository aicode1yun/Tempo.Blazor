import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasInputController } from '../input-controller.mjs';
import { applyCanvasTextEdit, canvasBlockText } from '../text-editing.mjs';

test('insert text at collapsed caret and replace selection by typing', () => {
    const model = createInputModel('Hello world');
    const inserted = applyCanvasTextEdit(model, collapsed('typing-body', 5), { type: 'insertText', text: ' canvas' });

    assert.equal(inserted.changed, true);
    assert.equal(canvasBlockText(inserted.model, 'typing-body'), 'Hello canvas world');
    assert.deepEqual(inserted.selection.focus, { blockId: 'typing-body', offset: 12 });

    const replaced = applyCanvasTextEdit(
        inserted.model,
        { anchor: { blockId: 'typing-body', offset: 6 }, focus: { blockId: 'typing-body', offset: 12 } },
        { type: 'insertText', text: 'model' });

    assert.equal(canvasBlockText(replaced.model, 'typing-body'), 'Hello model world');
    assert.deepEqual(replaced.selection.focus, { blockId: 'typing-body', offset: 11 });
});

test('Enter splits paragraphs and Shift Enter inserts a soft line break', () => {
    const model = createInputModel('HelloWorld');
    const split = applyCanvasTextEdit(model, collapsed('typing-body', 5), { type: 'insertParagraph' });

    assert.equal(split.changed, true);
    assert.equal(split.model.body.blocks.length, 2);
    assert.equal(canvasBlockText(split.model, 'typing-body'), 'Hello');
    assert.equal(canvasBlockText(split.model, split.insertedBlockId), 'World');
    assert.deepEqual(split.selection.focus, { blockId: split.insertedBlockId, offset: 0 });

    const softBreak = applyCanvasTextEdit(split.model, split.selection, { type: 'insertLineBreak' });
    assert.equal(canvasBlockText(softBreak.model, split.insertedBlockId), '\nWorld');
    assert.equal(softBreak.model.body.blocks.length, 2);
});

test('Backspace and Delete are grapheme safe and merge block boundaries', () => {
    const model = {
        documentId: 'phase-8-delete',
        version: 0,
        body: {
            blocks: [
                textBlock('delete-first', 'A👨‍👩‍👧B', 10),
                textBlock('delete-second', 'Next', 20),
            ],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };

    const emojiEnd = 'A👨‍👩‍👧'.length;
    const deletedEmoji = applyCanvasTextEdit(model, collapsed('delete-first', emojiEnd), { type: 'deleteBackward' });
    assert.equal(canvasBlockText(deletedEmoji.model, 'delete-first'), 'AB');
    assert.deepEqual(deletedEmoji.selection.focus, { blockId: 'delete-first', offset: 1 });

    const forward = applyCanvasTextEdit(deletedEmoji.model, collapsed('delete-first', 1), { type: 'deleteForward' });
    assert.equal(canvasBlockText(forward.model, 'delete-first'), 'A');

    const merged = applyCanvasTextEdit(forward.model, collapsed('delete-second', 0), { type: 'deleteBackward' });
    assert.equal(merged.model.body.blocks.length, 1);
    assert.equal(canvasBlockText(merged.model, 'delete-first'), 'ANext');
    assert.deepEqual(merged.selection.focus, { blockId: 'delete-first', offset: 1 });
});

test('input controller routes beforeinput and keydown without contenteditable authority', () => {
    const input = createFakeInput();
    let model = createInputModel('Hi');
    let selection = collapsed('typing-body', 2);
    const commits = [];
    const controller = createCanvasInputController({
        inputBridge: {
            input,
            subscribe(listener) {
                input.beforeInputListener = listener;
                return () => { input.beforeInputListener = null; };
            },
        },
        selectionController: {
            getSelection: () => selection,
            setSelection: next => { selection = next; },
            setCompositionRange() { },
        },
        getModel: () => model,
        commit(change) {
            model = change.model;
            selection = change.selection;
            commits.push(change);
            return { ok: true };
        },
        now: createClock(),
    }).mount();

    const beforeInputEvent = createEvent();
    input.beforeInputListener({ inputType: 'insertText', data: '!' }, beforeInputEvent);
    assert.equal(beforeInputEvent.defaultPrevented, true);
    assert.equal(canvasBlockText(model, 'typing-body'), 'Hi!');

    controller.handleKeyDown({ key: 'Enter', preventDefault() { this.defaultPrevented = true; } });
    assert.equal(model.body.blocks.length, 2);
    assert.equal(commits.at(-1).result.operation, 'insertParagraph');

    controller.destroy();
});

test('Tab and Shift Tab route to list nesting commands when form navigation is unavailable', () => {
    const input = createFakeInput();
    const executed = [];
    const controller = createCanvasInputController({
        inputBridge: {
            input,
            subscribe() {
                return () => { };
            },
        },
        selectionController: {
            getSelection: () => collapsed('typing-body', 0),
        },
        getModel: () => createInputModel('List item'),
        commit() {
            throw new Error('Tab list nesting must be command-routed instead of text editing.');
        },
        executeCommand(commandId) {
            executed.push(commandId);
            return commandId === 'increaseListLevel' || commandId === 'decreaseListLevel';
        },
    }).mount();

    const tab = { ...createEvent(), key: 'Tab', shiftKey: false };
    assert.equal(controller.handleKeyDown(tab), true);
    assert.equal(tab.defaultPrevented, true);

    const shiftTab = { ...createEvent(), key: 'Tab', shiftKey: true };
    assert.equal(controller.handleKeyDown(shiftTab), true);
    assert.equal(shiftTab.defaultPrevented, true);
    assert.deepEqual(executed, ['nextContentControl', 'increaseListLevel', 'previousContentControl', 'decreaseListLevel']);

    controller.destroy();
});

test('Tab and Shift Tab route to content control navigation before list nesting', () => {
    const input = createFakeInput();
    const executed = [];
    const controller = createCanvasInputController({
        inputBridge: {
            input,
            subscribe() {
                return () => { };
            },
        },
        selectionController: {
            getSelection: () => collapsed('typing-body', 0),
        },
        getModel: () => createInputModel('Form field'),
        commit() {
            throw new Error('Content-control keyboard navigation must not commit text edits.');
        },
        executeCommand(commandId, payload) {
            executed.push({ commandId, direction: payload?.direction || '' });
            return commandId === 'nextContentControl' || commandId === 'previousContentControl';
        },
    }).mount();

    const tab = { ...createEvent(), key: 'Tab', shiftKey: false };
    assert.equal(controller.handleKeyDown(tab), true);
    assert.equal(tab.defaultPrevented, true);

    const shiftTab = { ...createEvent(), key: 'Tab', shiftKey: true };
    assert.equal(controller.handleKeyDown(shiftTab), true);
    assert.equal(shiftTab.defaultPrevented, true);
    assert.deepEqual(executed, [
        { commandId: 'nextContentControl', direction: 'next' },
        { commandId: 'previousContentControl', direction: 'previous' },
    ]);

    controller.destroy();
});

test('input controller routes keyboard and beforeinput into the active math slot', () => {
    const input = createFakeInput();
    const executed = [];
    const selection = {
        ...collapsed('typing-body', 1),
        math: {
            mathId: 'math-input-equation',
            runId: 'math-input-run',
            slotPath: ['elements', 0, 'rows', 0, 'cells', 0],
            slotName: 'cell 1, 1',
            offset: 0,
        },
    };
    const controller = createCanvasInputController({
        inputBridge: {
            input,
            subscribe(listener) {
                input.beforeInputListener = listener;
                return () => { input.beforeInputListener = null; };
            },
        },
        selectionController: {
            getSelection: () => selection,
        },
        getModel: () => createInputModel('Equation'),
        commit() {
            throw new Error('Active math slot input must route through math commands.');
        },
        executeCommand(commandId, payload) {
            executed.push({ commandId, payload });
            return true;
        },
    }).mount();

    const text = createEvent();
    assert.equal(input.beforeInputListener({ inputType: 'insertText', data: 'x' }, text), true);
    assert.equal(text.defaultPrevented, true);

    const backspace = { ...createEvent(), key: 'Backspace' };
    assert.equal(controller.handleKeyDown(backspace), true);
    assert.equal(backspace.defaultPrevented, true);

    const tab = { ...createEvent(), key: 'Tab', shiftKey: false };
    assert.equal(controller.handleKeyDown(tab), true);

    const enter = { ...createEvent(), key: 'Enter' };
    assert.equal(controller.handleKeyDown(enter), true);

    assert.deepEqual(executed.map(item => item.commandId), [
        'insertMathSlotText',
        'deleteMathSlotBackward',
        'moveMathSlot',
        'addMathMatrixRow',
    ]);
    assert.deepEqual(executed.at(-1).payload.matrixPath, ['elements', 0]);

    controller.destroy();
});

test('typing in a heading invalidates outline and table of contents revisions', () => {
    const input = createFakeInput();
    let model = createInputModel('Heading');
    model.outlineRevision = 0;
    model.tableOfContentsRevision = 0;
    model.body.blocks[0].type = 'heading';
    model.body.blocks[0].content.type = 'heading';
    model.body.blocks[0].content.headingLevel = 1;
    let selection = collapsed('typing-body', 7);
    const controller = createCanvasInputController({
        inputBridge: {
            input,
            subscribe(listener) {
                input.beforeInputListener = listener;
                return () => { input.beforeInputListener = null; };
            },
        },
        selectionController: {
            getSelection: () => selection,
            setSelection: next => { selection = next; },
        },
        getModel: () => model,
        commit(change) {
            model = change.model;
            selection = change.selection;
            return { ok: true };
        },
    }).mount();

    input.beforeInputListener({ inputType: 'insertText', data: ' updated' }, createEvent());
    assert.equal(model.outlineRevision, 1);
    assert.equal(model.tableOfContentsRevision, 1);
    assert.equal(canvasBlockText(model, 'typing-body'), 'Heading updated');

    controller.destroy();
});

function createInputModel(text) {
    return {
        documentId: 'phase-8-input',
        version: 0,
        body: { blocks: [textBlock('typing-body', text, 10)] },
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

function createEvent() {
    return {
        defaultPrevented: false,
        preventDefault() {
            this.defaultPrevented = true;
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
