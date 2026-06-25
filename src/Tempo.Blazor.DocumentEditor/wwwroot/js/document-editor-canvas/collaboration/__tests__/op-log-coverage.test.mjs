// Phase B0.4 — characterizes the operations the canvas op-log derives from a model change. This is the
// foundation for Phase B1 (collaboration via operation-relay): it pins exactly which operation each edit
// type produces, so when C# stops diffing _document and instead relays the engine's op-log batches, we can
// prove the engine already covers every edit kind (text fine-grained; everything else as whole `updateBlock`).
import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasOperationLog, diffModels } from '../op-log.mjs';

// --- model builders (match the canvas model shape diffModels walks) ---
function paragraph(id, text, extra = {}) {
    return {
        id,
        type: 'paragraph',
        content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
        ...extra,
    };
}

function doc(blocks) {
    return { documentId: 'phaseB-coverage', version: 1, body: { blocks } };
}

function diff(before, after) {
    return diffModels(before, after, { clientId: 'client-a', sequence: 1, source: 'test' });
}

test('B0.4 insert text -> single insertText op', () => {
    const ops = diff(doc([paragraph('p1', 'Hello')]), doc([paragraph('p1', 'Hello world')]));
    assert.equal(ops.length, 1);
    assert.equal(ops[0].type, 'insertText');
    assert.equal(ops[0].target.blockId, 'p1');
    assert.equal(ops[0].target.offset, 5);
    assert.equal(ops[0].text, ' world');
});

test('B0.4 delete text -> single deleteText op', () => {
    const ops = diff(doc([paragraph('p1', 'Hello world')]), doc([paragraph('p1', 'Hello')]));
    assert.equal(ops.length, 1);
    assert.equal(ops[0].type, 'deleteText');
    assert.equal(ops[0].target.offset, 5);
    assert.equal(ops[0].text, ' world');
});

test('B0.4 mixed replace in one block -> updateBlock (no clean text diff)', () => {
    const ops = diff(doc([paragraph('p1', 'Hello world')]), doc([paragraph('p1', 'Howdy world')]));
    assert.equal(ops.length, 1);
    assert.equal(ops[0].type, 'updateBlock');
    assert.equal(ops[0].block.content.runs[0].text, 'Howdy world');
});

test('B0.4 new paragraph (split) -> insertBlock', () => {
    const ops = diff(doc([paragraph('p1', 'Hello')]), doc([paragraph('p1', 'Hello'), paragraph('p2', 'World')]));
    assert.equal(ops.length, 1);
    assert.equal(ops[0].type, 'insertBlock');
    assert.equal(ops[0].target.blockId, 'p2');
    assert.equal(ops[0].block.content.runs[0].text, 'World');
});

test('B0.4 removed paragraph -> deleteBlock', () => {
    const ops = diff(doc([paragraph('p1', 'Hello'), paragraph('p2', 'World')]), doc([paragraph('p1', 'Hello')]));
    assert.equal(ops.length, 1);
    assert.equal(ops[0].type, 'deleteBlock');
    assert.equal(ops[0].target.blockId, 'p2');
});

test('B0.4 paragraph property change (alignment, same text) -> updateBlock', () => {
    const before = doc([paragraph('p1', 'Hello', { alignment: 'left' })]);
    const after = doc([paragraph('p1', 'Hello', { alignment: 'center' })]);
    const ops = diff(before, after);
    assert.equal(ops.length, 1);
    assert.equal(ops[0].type, 'updateBlock');
    assert.equal(ops[0].block.alignment, 'center');
});

test('B0.4 image resize (non-text block) -> updateBlock', () => {
    const image = (w) => ({ id: 'img1', type: 'image', content: { type: 'image', width: w, height: 50 } });
    const ops = diff(doc([image(100)]), doc([image(200)]));
    assert.equal(ops.length, 1);
    assert.equal(ops[0].type, 'updateBlock');
    assert.equal(ops[0].block.content.width, 200);
});

// B1 FIX: a table edit emits EXACTLY ONE updateBlock(table) carrying the whole table — no recursion into
// cells, so no redundant cell op and no spurious cell move. (Was the B0.4 double-apply / spurious-move bug.)
test('B1 table cell text edit -> single updateBlock(table), no cell/move ops', () => {
    const table = (cellText) => ({
        id: 'tbl1',
        type: 'table',
        content: { table: { rows: [{ cells: [{ id: 'cell1', blocks: [paragraph('cp1', cellText)] }] }] } },
    });
    const ops = diff(doc([table('Hi')]), doc([table('Hi!')]));
    assert.equal(ops.length, 1);
    assert.equal(ops[0].type, 'updateBlock');
    assert.equal(ops[0].target.blockId, 'tbl1');
    // The whole new table (with the edited cell text) travels in the updateBlock.
    assert.equal(ops[0].block.content.table.rows[0].cells[0].blocks[0].content.runs[0].text, 'Hi!');
});

test('B1 table property change -> single updateBlock(table), no spurious cell move', () => {
    const table = (border) => ({
        id: 'tbl1',
        type: 'table',
        content: { table: { border, rows: [{ cells: [{ id: 'cell1', blocks: [paragraph('cp1', 'Hi')] }] }] } },
    });
    const ops = diff(doc([table('none')]), doc([table('single')]));
    assert.equal(ops.length, 1);
    assert.equal(ops[0].type, 'updateBlock');
    assert.equal(ops[0].target.blockId, 'tbl1');
    assert.equal(ops[0].block.content.table.border, 'single');
});

test('B0.4 recordLocalChange wraps ops into a relayable batch and tracks pending sequence', () => {
    const log = createCanvasOperationLog({ documentId: 'phaseB-coverage', clientId: 'client-a' });
    const batch = log.recordLocalChange({ before: doc([paragraph('p1', 'Hi')]), after: doc([paragraph('p1', 'Hip')]) });

    assert.ok(batch, 'a change must yield a batch');
    assert.equal(batch.documentId, 'phaseB-coverage');
    assert.equal(batch.clientId, 'client-a');
    assert.equal(batch.localSequence, 1);
    assert.equal(batch.operations.length, 1);
    assert.equal(batch.operations[0].type, 'insertText');

    // The batch is queued as pending until acknowledged (this is what B1 will relay then ack).
    assert.equal(log.snapshot().pendingLocalBatches.length, 1);
    log.acknowledgeThrough(1);
    assert.equal(log.snapshot().pendingLocalBatches.length, 0);
});

test('B0.4 no-op change yields no batch', () => {
    const log = createCanvasOperationLog({ documentId: 'phaseB-coverage', clientId: 'client-a' });
    const same = doc([paragraph('p1', 'Hi')]);
    assert.equal(log.recordLocalChange({ before: same, after: doc([paragraph('p1', 'Hi')]) }), null);
});

test('B1.1 takeLocalBatches returns pending batches and clears them (host relay handoff)', () => {
    const log = createCanvasOperationLog({ documentId: 'phaseB-coverage', clientId: 'client-a' });
    log.recordLocalChange({ before: doc([paragraph('p1', 'Hi')]), after: doc([paragraph('p1', 'Hip')]) });
    log.recordLocalChange({ before: doc([paragraph('p1', 'Hip')]), after: doc([paragraph('p1', 'Hippo')]) });

    const taken = log.takeLocalBatches();
    assert.equal(taken.length, 2, 'both pending batches handed to the host');
    assert.equal(taken[0].localSequence, 1);
    assert.equal(taken[1].localSequence, 2);
    // Ownership transferred: nothing left pending, and a second take returns nothing.
    assert.equal(log.snapshot().pendingLocalBatches.length, 0);
    assert.equal(log.takeLocalBatches().length, 0);
});
