// Phase 5 (command-layer plan): the table context menu routes deleteTable and
// toggleTableHeaderRow — the engine must delete the whole table block / toggle
// the header-row layout flag (both were silent no-ops).
import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';

function paragraph(id, text, order) {
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

function tableBlock(id, order) {
    return {
        id,
        sectionId: 'section-1',
        type: 'table',
        order,
        content: {
            type: 'table',
            table: {
                layout: { cellPadding: 8 },
                rows: [1, 2].map(rowNumber => ({
                    id: `${id}-row-${rowNumber}`,
                    cells: [1, 2].map(cellNumber => ({
                        id: `${id}-r${rowNumber}c${cellNumber}`,
                        columnSpan: 1,
                        rowSpan: 1,
                        isHeader: false,
                        merge: { isOrigin: true, originCellId: null },
                        width: null,
                        backgroundColor: null,
                        borders: {},
                        verticalAlignment: 0,
                        padding: 8,
                        blocks: [paragraph(`${id}-r${rowNumber}c${cellNumber}-p`, `cell ${rowNumber}${cellNumber}`, 1)],
                        preserve: {},
                    })),
                })),
            },
        },
    };
}

function createModel({ tableOnly = false } = {}) {
    return {
        documentId: 'phase-5-delete-table',
        version: 0,
        body: {
            blocks: tableOnly
                ? [tableBlock('target-table', 10)]
                : [paragraph('intro', 'Intro', 10), tableBlock('target-table', 20), paragraph('outro', 'Outro', 30)],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function createRuntime(initialModel, initialSelection) {
    const state = { model: initialModel, selection: initialSelection };
    const runtime = createCanvasCommandRuntime({
        getModel: () => state.model,
        getSelection: () => state.selection,
        history: createHistoryStore(),
        commit(change) {
            state.model = change.model;
            state.selection = change.selection ?? state.selection;
        },
    });
    return { runtime, state };
}

const caretInFirstCell = { anchor: { blockId: 'target-table-r1c1-p', offset: 0 }, focus: { blockId: 'target-table-r1c1-p', offset: 0 } };

test('deleteTable removes the whole table via cellId payload and moves the caret to the following block', () => {
    const { runtime, state } = createRuntime(createModel(), caretInFirstCell);

    const result = runtime.execCommand('deleteTable', { cellId: 'target-table-r1c1' });

    assert.equal(result.handled, true, 'deleteTable must be a registered command');
    assert.equal(result.result?.changed, true);
    assert.deepEqual(state.model.body.blocks.map(block => block.id), ['intro', 'outro']);
    assert.equal(state.selection.focus.blockId, 'outro', 'the caret must land on the block that followed the table');
    assert.ok(!state.model.sections[0].blocks.some(block => block.id === 'target-table'), 'sections must be resynchronized');
});

test('deleteTable resolves the table by tableId even without any selection in it', () => {
    const { runtime, state } = createRuntime(createModel(), { anchor: { blockId: 'intro', offset: 0 }, focus: { blockId: 'intro', offset: 0 } });

    const result = runtime.execCommand('deleteTable', { tableId: 'target-table' });

    assert.equal(result.handled, true);
    assert.equal(result.result?.changed, true);
    assert.deepEqual(state.model.body.blocks.map(block => block.id), ['intro', 'outro']);
});

test('deleteTable of the last table keeps the caret on the previous block; an orphaned body gets an empty paragraph', () => {
    const trailing = createModel();
    trailing.body.blocks = trailing.body.blocks.filter(block => block.id !== 'outro');
    const { runtime: trailingRuntime, state: trailingState } = createRuntime(trailing, caretInFirstCell);
    trailingRuntime.execCommand('deleteTable', { cellId: 'target-table-r1c1' });
    assert.equal(trailingState.selection.focus.blockId, 'intro', 'with no following block the caret must land on the previous one');

    const { runtime, state } = createRuntime(createModel({ tableOnly: true }), caretInFirstCell);
    const result = runtime.execCommand('deleteTable', { tableId: 'target-table' });
    assert.equal(result.result?.changed, true);
    assert.equal(state.model.body.blocks.length, 1, 'an orphaned body must receive an empty paragraph');
    assert.equal(state.model.body.blocks[0].type, 'paragraph');
    assert.equal(state.selection.focus.blockId, state.model.body.blocks[0].id, 'the caret must land in the inserted empty paragraph');
});

test('deleteTable undo restores the identical body blocks', () => {
    // The command runtime normalizes the model on every transaction (default style catalog,
    // empty note/header collections) — the undo identity contract is about the CONTENT: the
    // body blocks, including the whole table subtree, must come back byte-identical.
    const { runtime, state } = createRuntime(createModel(), caretInFirstCell);
    const before = JSON.stringify(state.model.body.blocks);

    runtime.execCommand('deleteTable', { cellId: 'target-table-r1c1' });
    assert.equal(state.model.body.blocks.length, 2);

    const undone = runtime.execCommand('undo');
    assert.equal(undone.handled, true);
    assert.equal(JSON.stringify(state.model.body.blocks), before, 'undo must restore the exact pre-delete body blocks');
});

test('toggleTableHeaderRow toggles layout.headerRow and a double toggle restores the identical model', () => {
    // Semantics note (documented limit): the toggle drives table.layout.headerRow ONLY. The
    // renderer styles row 0 + repeats it across page breaks from this flag
    // (resolveTableCellStyle/tableRepeatsHeaderRows); cell-level isHeader/backgroundColor
    // overrides win by design, so seeds with explicitly styled header cells keep their look.
    const { runtime, state } = createRuntime(createModel(), caretInFirstCell);
    const before = JSON.stringify(state.model);

    const on = runtime.execCommand('toggleTableHeaderRow', { cellId: 'target-table-r1c1' });
    assert.equal(on.handled, true, 'toggleTableHeaderRow must be a registered command');
    assert.equal(on.result?.changed, true);
    const table = () => state.model.body.blocks.find(block => block.id === 'target-table');
    assert.equal(table().content.table.layout.headerRow, true, 'first toggle must enable the header row flag');

    const off = runtime.execCommand('toggleTableHeaderRow', { tableId: 'target-table' });
    assert.equal(off.result?.changed, true);
    assert.equal(table().content.table.layout.headerRow, false, 'second toggle must disable the flag');

    // Idempotence: apart from the now-explicit false flag, a double toggle must return the
    // identical body blocks (the runtime normalizes top-level collections on any transaction).
    const normalize = blocks => {
        const copy = JSON.parse(JSON.stringify(blocks));
        const target = copy.find(block => block.id === 'target-table');
        target.content.table.layout.headerRow = false;
        return JSON.stringify(copy);
    };
    assert.equal(
        normalize(state.model.body.blocks),
        normalize(JSON.parse(before).body.blocks),
        'a double toggle must return to the original state');

    const undone = runtime.execCommand('undo');
    assert.equal(undone.handled, true);
    assert.equal(table().content.table.layout.headerRow, true, 'undo must restore the previous flag value');
});
