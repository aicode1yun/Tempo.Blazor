import assert from 'node:assert/strict';
import test from 'node:test';
import { applyAutocorrectAfterTextInput } from '../autocorrect.mjs';
import { createCanvasInputController } from '../input-controller.mjs';
import { canvasBlockText } from '../text-editing.mjs';

test('replace-as-you-type rules cover dash, symbols, fractions, ordinals, quotes and capitalization', () => {
    assertAutocorrect('--', 2, '-', '—', 'emDash');
    assertAutocorrect('(c) ', 4, ' ', '© ', 'replaceAsYouType');
    assertAutocorrect('1/2 ', 4, ' ', '½ ', 'replaceAsYouType');
    assertAutocorrect('21st ', 5, ' ', '21ˢᵗ ', 'replaceAsYouType');
    assertAutocorrect('"', 1, '"', '“', 'smartQuote');
    assertAutocorrect('hello. w', 8, 'w', 'hello. W', 'autoCapitalize');
});

test('autoformat creates lists, hyperlinks and horizontal rules as one model mutation', () => {
    const numbered = applyAutocorrectAfterTextInput({
        model: createModel('1. '),
        selection: collapsed(3),
        edit: { type: 'insertText', text: ' ' },
    });
    assert.equal(numbered.changed, true);
    assert.equal(numbered.operation, 'autoNumberList');
    assert.equal(numbered.model.body.blocks[0].type, 'list');
    assert.equal(canvasBlockText(numbered.model, 'autocorrect-body'), '');
    assert.equal(numbered.model.body.blocks[0].content.list.ordered, true);

    const linked = applyAutocorrectAfterTextInput({
        model: createModel('https://example.test '),
        selection: collapsed('https://example.test '.length),
        edit: { type: 'insertText', text: ' ' },
    });
    assert.equal(linked.changed, true);
    assert.equal(linked.operation, 'autoHyperlink');
    assert.equal(linked.selection.focus.offset, 'https://example.test '.length);
    const linkRun = linked.model.body.blocks[0].content.runs.find(run => run.marks?.some(mark => mark.type === 'link'));
    assert.equal(linkRun.marks[0].link.href, 'https://example.test');

    const rule = applyAutocorrectAfterTextInput({
        model: createModel('--- '),
        selection: collapsed(4),
        edit: { type: 'insertText', text: ' ' },
    });
    assert.equal(rule.changed, true);
    assert.equal(rule.operation, 'autoHorizontalRule');
    assert.equal(canvasBlockText(rule.model, 'autocorrect-body'), '');
    assert.equal(rule.model.body.blocks[0].paragraphProperties.horizontalRule, true);
});

test('input controller records autocorrect with undo-before raw typed text', () => {
    const input = createFakeInput();
    let model = createModel('');
    let selection = collapsed(0);
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
        },
        getModel: () => model,
        commit(change) {
            model = change.model;
            selection = change.selection;
            commits.push(change);
        },
    }).mount();

    input.beforeInputListener({ inputType: 'insertText', data: '-' }, createEvent());
    input.beforeInputListener({ inputType: 'insertText', data: '-' }, createEvent());

    assert.equal(canvasBlockText(model, 'autocorrect-body'), '—');
    assert.equal(commits.at(-1).result.autoCorrect, true);
    assert.equal(canvasBlockText(commits.at(-1).before.model, 'autocorrect-body'), '--');
    assert.equal(commits.at(-1).result.operation, 'emDash');

    controller.destroy();
});

function assertAutocorrect(text, offset, typedText, expectedText, expectedOperation) {
    const result = applyAutocorrectAfterTextInput({
        model: createModel(text),
        selection: collapsed(offset),
        edit: { type: 'insertText', text: typedText },
    });

    assert.equal(result.changed, true, `${text} should autocorrect`);
    assert.equal(result.operation, expectedOperation);
    assert.equal(canvasBlockText(result.model, 'autocorrect-body'), expectedText);
    assert.equal(canvasBlockText(result.undoBeforeModel, 'autocorrect-body'), text);
}

function createModel(text) {
    return {
        documentId: 'phase-e10-autocorrect',
        version: 0,
        body: { blocks: [textBlock('autocorrect-body', text, 10)] },
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

function collapsed(offset) {
    return {
        anchor: { blockId: 'autocorrect-body', offset },
        focus: { blockId: 'autocorrect-body', offset },
    };
}

function createFakeInput() {
    return {
        value: '',
        addEventListener() { },
        removeEventListener() { },
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

test('plain mid-word typing takes the no-clone fast path (returns the input model reference)', () => {
    // Typing a letter that cannot trigger any rule must not deep-clone the model (the clones dominated
    // per-keystroke profiles); unchanged() returns the original references, which pins the fast path.
    const model = createModel('hella');
    const selection = collapsed(5);
    const result = applyAutocorrectAfterTextInput({
        model,
        selection,
        edit: { type: 'insertText', text: 'a' },
    });
    assert.equal(result.changed, false);
    assert.equal(result.model, model, 'fast path must return the original model reference (no clone)');
    assert.equal(result.selection, selection);
});

test('precheck still routes capitalize/em-dash candidates to the slow path', () => {
    // 'w' after a sentence boundary — capitalize must still fire (the precheck reads block text).
    assertAutocorrect('done. w', 7, 'w', 'done. W', 'autoCapitalize');
    // second '-' (not a word boundary char) — em-dash must still fire.
    assertAutocorrect('--', 2, '-', '—', 'emDash');
    // a letter NOT after a boundary, with '--' NOT before the caret -> fast path, nothing fires.
    const noFire = applyAutocorrectAfterTextInput({
        model: createModel('a--bc'),
        selection: collapsed(5),
        edit: { type: 'insertText', text: 'c' },
    });
    assert.equal(noFire.changed, false);
});
