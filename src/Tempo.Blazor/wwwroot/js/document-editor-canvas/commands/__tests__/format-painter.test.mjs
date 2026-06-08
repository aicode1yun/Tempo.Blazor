import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../dispatcher.mjs';
import { createFormatPainterState } from '../format-painter.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';

test('format painter state tolerates missing history snapshots', () => {
    assert.deepEqual(createFormatPainterState(null), {
        active: false,
        sticky: false,
        payload: null,
    });
    assert.deepEqual(createFormatPainterState(undefined), {
        active: false,
        sticky: false,
        payload: null,
    });
});

test('format painter copies character and paragraph formatting and remains undoable', () => {
    let model = createPainterModel();
    let selection = range('source', 0, 6);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const copied = runtime.execCommand('copyFormatting');
    assert.equal(copied.handled, true);
    assert.equal(copied.result.stateChanged, true);
    assert.equal(runtime.queryCommandState().formatPainter.active, true);

    selection = range('target', 0, 6);
    const pasted = runtime.execCommand('pasteFormatting');
    assert.equal(pasted.handled, true);
    assert.equal(pasted.result.changed, true);
    assert.equal(runtime.queryCommandState().formatPainter.active, false);

    const target = model.body.blocks.find(block => block.id === 'target');
    assert.equal(target.paragraphProperties.alignment, 2);
    assert.equal(target.paragraphProperties.spacingAfter, 14);
    const styledRun = target.content.runs.find(run => run.text === 'Target');
    assert.ok(styledRun.marks.some(mark => mark.type === 'bold'));
    assert.ok(styledRun.marks.some(mark => mark.type === 'textColor' && mark.value === '#1155cc'));

    runtime.execCommand('undo');
    const undone = model.body.blocks.find(block => block.id === 'target');
    assert.deepEqual(undone.paragraphProperties, {});
    assert.equal(undone.content.runs.some(run => run.marks?.length > 0), false);

    runtime.execCommand('redo');
    const redone = model.body.blocks.find(block => block.id === 'target');
    assert.equal(redone.paragraphProperties.alignment, 2);
    assert.ok(redone.content.runs.find(run => run.text === 'Target').marks.some(mark => mark.type === 'bold'));
});

test('locked format painter keeps payload after multiple paste operations', () => {
    let model = createPainterModel();
    let selection = range('source', 0, 6);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    runtime.execCommand('lockFormatPainter');
    assert.equal(runtime.queryCommandState().formatPainter.sticky, true);

    selection = range('target', 0, 6);
    runtime.execCommand('pasteFormatting');
    assert.equal(runtime.queryCommandState().formatPainter.active, true);

    selection = range('second-target', 0, 6);
    runtime.execCommand('pasteFormatting');
    assert.equal(runtime.queryCommandState().formatPainter.active, true);

    const second = model.body.blocks.find(block => block.id === 'second-target');
    assert.equal(second.paragraphProperties.alignment, 2);
    assert.ok(second.content.runs.find(run => run.text === 'Second').marks.some(mark => mark.type === 'bold'));

    runtime.execCommand('cancelFormatPainter');
    assert.equal(runtime.queryCommandState().formatPainter.active, false);
});

function createPainterModel() {
    return {
        documentId: 'phase-e10-format-painter',
        version: 0,
        body: {
            blocks: [
                block('source', 'Source style', 10, {
                    alignment: 2,
                    spacingAfter: 14,
                    leftIndent: 18,
                }, [
                    { id: 'source-run-1', type: 'text', text: 'Source', marks: [{ type: 'bold' }, { type: 'textColor', value: '#1155cc' }] },
                    { id: 'source-run-2', type: 'text', text: ' style', marks: [] },
                ]),
                block('target', 'Target plain', 20),
                block('second-target', 'Second plain', 30),
            ],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function block(id, text, order, paragraphProperties = {}, runs = null) {
    return {
        id,
        sectionId: 'section-1',
        type: 'paragraph',
        order,
        paragraphProperties,
        content: {
            type: 'paragraph',
            runs: runs || [{ id: `${id}-run`, type: 'text', text, marks: [] }],
        },
    };
}

function range(blockId, start, end) {
    return {
        anchor: { blockId, offset: start },
        focus: { blockId, offset: end },
    };
}
