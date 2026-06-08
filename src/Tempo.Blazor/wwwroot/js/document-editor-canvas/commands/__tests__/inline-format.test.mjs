import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../dispatcher.mjs';
import { applyInlineFormatCommand, createInlineFormatState, queryInlineFormattingState } from '../inline-format.mjs';
import { applyCanvasTextEdit, canvasBlockText } from '../../input/text-editing.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';

test('range inline commands toggle marks and expose active mixed readback', () => {
    const selection = range(0, 5);
    const result = applyInlineFormatCommand(createModel('Hello world'), selection, 'bold', null, createInlineFormatState());
    assert.equal(result.changed, true);
    assert.deepEqual(result.model.body.blocks[0].content.runs[0].marks.map(mark => mark.type), ['bold']);
    assert.equal(result.formattingState.commands.bold.state, 'active');

    const mixed = queryInlineFormattingState(result.model, range(0, 11), result.state);
    assert.equal(mixed.commands.bold.state, 'mixed');

    const removed = applyInlineFormatCommand(result.model, selection, 'bold', null, result.state);
    assert.equal(removed.formattingState.commands.bold.state, 'inactive');
});

test('font size color highlight clear formatting and link mutate selected runs', () => {
    let model = createModel('Format me');
    const selection = range(0, 6);
    let result = applyInlineFormatCommand(model, selection, 'fontfamily', 'Aptos', createInlineFormatState());
    result = applyInlineFormatCommand(result.model, selection, 'fontsize', '18pt', result.state);
    result = applyInlineFormatCommand(result.model, selection, 'textcolor', '#dc2626', result.state);
    result = applyInlineFormatCommand(result.model, selection, 'highlight', '#fde68a', result.state);
    result = applyInlineFormatCommand(result.model, selection, 'link', 'https://example.com', result.state);

    const marks = result.model.body.blocks[0].content.runs[0].marks;
    assert.deepEqual(marks.map(mark => mark.type).sort(), ['fontFamily', 'fontSize', 'highlight', 'link', 'textColor'].sort());
    assert.equal(marks.find(mark => mark.type === 'fontSize').value, '18');
    assert.equal(result.formattingState.commands.fontsize.value, '18');
    assert.equal(result.formattingState.commands.textcolor.value, '#dc2626');
    assert.equal(result.formattingState.commands.highlight.value, '#fde68a');
    assert.equal(result.formattingState.commands.link.value, 'https://example.com');

    const noLink = applyInlineFormatCommand(result.model, selection, 'removelink', null, result.state);
    assert.equal(noLink.formattingState.commands.link.state, 'inactive');

    const cleared = applyInlineFormatCommand(noLink.model, selection, 'clearFormatting', null, noLink.state);
    assert.equal(cleared.model.body.blocks[0].content.runs[0].marks.length, 0);
});

test('collapsed inline command stores pending formatting for the next inserted text', () => {
    const model = createModel('Hello ');
    const state = createInlineFormatState();
    const pending = applyInlineFormatCommand(model, collapsed(6), 'bold', null, state);
    assert.equal(pending.changed, false);
    assert.equal(pending.formattingState.commands.bold.state, 'active');

    const inserted = applyCanvasTextEdit(model, collapsed(6), {
        type: 'insertText',
        text: 'canvas',
        marks: pending.state.pendingMarks,
    });

    assert.equal(canvasBlockText(inserted.model, 'format-body'), 'Hello canvas');
    const newRun = inserted.model.body.blocks[0].content.runs.find(run => run.text === 'canvas');
    assert.ok(newRun.marks.some(mark => mark.type === 'bold'));
});

test('inline format state tolerates missing history snapshots', () => {
    assert.deepEqual(createInlineFormatState(null), { pendingMarks: [] });
    assert.deepEqual(createInlineFormatState(undefined), { pendingMarks: [] });
});

test('command runtime preserves selection token and provides undo redo snapshots', () => {
    let model = createModel('Hello world');
    let selection = range(0, 5);
    const commits = [];
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
            commits.push(change);
            return { ok: true };
        },
    });

    const tokenBefore = JSON.stringify(selection);
    const result = runtime.execCommand('bold');
    assert.equal(result.handled, true);
    assert.equal(JSON.stringify(selection), tokenBefore);
    assert.equal(runtime.queryCommand('bold').state, 'active');
    assert.equal(commits.length, 1);

    runtime.execCommand('undo');
    assert.equal(runtime.queryCommand('bold').state, 'inactive');
    runtime.execCommand('redo');
    assert.equal(runtime.queryCommand('bold').state, 'active');
});

function createModel(text) {
    return {
        documentId: 'phase-9-inline-format',
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

function collapsed(offset) {
    return {
        anchor: { blockId: 'format-body', offset },
        focus: { blockId: 'format-body', offset },
    };
}

function range(start, end) {
    return {
        anchor: { blockId: 'format-body', offset: start },
        focus: { blockId: 'format-body', offset: end },
    };
}
