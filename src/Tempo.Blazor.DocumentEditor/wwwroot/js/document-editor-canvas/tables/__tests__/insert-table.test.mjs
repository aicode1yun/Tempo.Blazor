// Phase 4 (command-layer plan): the toolbar "Insert table" grid routes
// execCommand('insertTable', { rows, columns, appendToBodyEnd }) — the engine
// must create a brand-new table block (it previously only operated on existing
// tables, so the toolbar grid was a silent no-op).
import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';

function createModel() {
    return {
        documentId: 'phase-4-insert-table',
        version: 0,
        pageSettings: { width: 800, marginLeft: 50, marginRight: 50 },
        body: {
            blocks: [
                paragraph('intro', 'Intro paragraph'),
                paragraph('outro', 'Outro paragraph'),
            ],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function paragraph(id, text) {
    return {
        id,
        sectionId: 'section-1',
        type: 'paragraph',
        order: id === 'intro' ? 10 : 20,
        paragraphProperties: {},
        content: {
            type: 'paragraph',
            runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }],
        },
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

function caretIn(blockId, offset = 0) {
    return { anchor: { blockId, offset }, focus: { blockId, offset } };
}

function tableBlocks(model) {
    return model.body.blocks.filter(block => String(block?.type || '').toLowerCase() === 'table');
}

test('insertTable creates a table after the caret block with empty cell paragraphs and caret in the first cell', () => {
    const { runtime, state } = createRuntime(createModel(), caretIn('intro', 5));

    const result = runtime.execCommand('insertTable', { rows: 2, columns: 2 });

    assert.equal(result.handled, true, 'insertTable must be a registered command');
    assert.equal(result.result?.changed, true, 'insertTable must report a model change');

    const tables = tableBlocks(state.model);
    assert.equal(tables.length, 1, 'one table block must be inserted');
    const table = tables[0];
    const blockIds = state.model.body.blocks.map(block => block.id);
    assert.equal(blockIds.indexOf(table.id), blockIds.indexOf('intro') + 1, 'the table must land right after the caret block');

    const rows = table.content.table.rows;
    assert.equal(rows.length, 2);
    assert.ok(rows.every(row => row.cells.length === 2), 'every row must have the requested column count');
    for (const row of rows) {
        for (const cell of row.cells) {
            assert.equal(cell.blocks.length, 1, 'each cell starts with one paragraph');
            assert.equal(cell.blocks[0].type, 'paragraph');
            assert.equal(String(cell.blocks[0].content.runs.map(run => run.text).join('')), '', 'cell paragraphs start empty');
            // Default column width = content width / columns → (800 - 50 - 50) / 2.
            assert.equal(cell.width, 350, 'default column widths must split the content width evenly');
        }
    }

    const firstCellParagraphId = rows[0].cells[0].blocks[0].id;
    assert.equal(state.selection.focus.blockId, firstCellParagraphId, 'the caret must move into the first cell');
    assert.equal(state.selection.focus.offset, 0);

    // The new table participates in section sync (save/reload path reads section.blocks).
    assert.ok(state.model.sections[0].blocks.some(block => block.id === table.id), 'sections must be resynchronized');
});

test('insertTable appendToBodyEnd places the table after the last body block', () => {
    const { runtime, state } = createRuntime(createModel(), caretIn('intro', 0));

    const result = runtime.execCommand('insertTable', { rows: 1, columns: 3, appendToBodyEnd: true });

    assert.equal(result.handled, true);
    const blocks = state.model.body.blocks;
    assert.equal(String(blocks.at(-1)?.type || '').toLowerCase(), 'table', 'the table must be the last body block');
    assert.equal(blocks.at(-1).content.table.rows[0].cells.length, 3);
});

test('insertTable with the caret inside an existing table inserts after that table, never nested', () => {
    const { runtime, state } = createRuntime(createModel(), caretIn('intro', 0));
    runtime.execCommand('insertTable', { rows: 1, columns: 1 });
    const firstTable = tableBlocks(state.model)[0];
    const innerParagraphId = firstTable.content.table.rows[0].cells[0].blocks[0].id;

    runtime.execCommand('insertTable', { rows: 1, columns: 2 });

    const tables = tableBlocks(state.model);
    assert.equal(tables.length, 2, 'a second top-level table must be inserted');
    const blockIds = state.model.body.blocks.map(block => block.id);
    assert.equal(
        blockIds.indexOf(tables[1].id),
        blockIds.indexOf(firstTable.id) + 1,
        'the new table must land after the table containing the caret');
    assert.equal(firstTable.content.table.rows[0].cells[0].blocks[0].id, innerParagraphId, 'the existing table must stay untouched');
});

test('insertTable is one atomic undo/redo transaction', () => {
    const { runtime, state } = createRuntime(createModel(), caretIn('intro', 5));

    runtime.execCommand('insertTable', { rows: 2, columns: 2 });
    assert.equal(tableBlocks(state.model).length, 1);

    const undone = runtime.execCommand('undo');
    assert.equal(undone.handled, true);
    assert.equal(tableBlocks(state.model).length, 0, 'undo must remove the whole table in one step');
    assert.deepEqual(state.model.body.blocks.map(block => block.id), ['intro', 'outro']);

    const redone = runtime.execCommand('redo');
    assert.equal(redone.handled, true);
    assert.equal(tableBlocks(state.model).length, 1, 'redo must restore the whole table in one step');
});

test('insertTable clamps rows/columns to at least 1 and at most the toolbar picker cap (10)', () => {
    const { runtime, state } = createRuntime(createModel(), caretIn('intro', 0));

    runtime.execCommand('insertTable', { rows: 0, columns: -4 });
    let table = tableBlocks(state.model).at(-1);
    assert.equal(table.content.table.rows.length, 1, 'rows must clamp up to 1');
    assert.equal(table.content.table.rows[0].cells.length, 1, 'columns must clamp up to 1');

    runtime.execCommand('insertTable', { rows: 99, columns: 42, appendToBodyEnd: true });
    table = tableBlocks(state.model).at(-1);
    assert.equal(table.content.table.rows.length, 10, 'rows must clamp down to the 10×10 picker cap');
    assert.equal(table.content.table.rows[0].cells.length, 10, 'columns must clamp down to the 10×10 picker cap');
});

test('insertTable without a resolvable caret block appends to the body end', () => {
    const { runtime, state } = createRuntime(createModel(), caretIn('missing-block', 0));

    const result = runtime.execCommand('insertTable', { rows: 2, columns: 2 });

    assert.equal(result.handled, true);
    assert.equal(result.result?.changed, true, 'a missing caret block must not turn the insert into a no-op');
    assert.equal(String(state.model.body.blocks.at(-1)?.type || '').toLowerCase(), 'table');
});
