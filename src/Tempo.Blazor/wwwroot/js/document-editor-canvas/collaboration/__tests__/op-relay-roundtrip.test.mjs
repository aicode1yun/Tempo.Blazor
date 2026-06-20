// Phase B1.2 — proves the engine's OWN op-log output round-trips through applyRemoteOperationBatch with no
// C# diff in the middle: editor A records a local change -> its op-log batch is applied verbatim to editor
// B's model -> the models converge. If this holds for every edit kind, C# can be a dumb transport pipe
// (B1.3-B1.5) instead of re-diffing _document (the reason the mirror exists today).
import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasOperationLog } from '../op-log.mjs';
import { applyRemoteOperationBatch } from '../transform.mjs';

function paragraph(id, text, extra = {}) {
    return {
        id,
        type: 'paragraph',
        content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
        ...extra,
    };
}

function doc(blocks) {
    return { documentId: 'phaseB-relay', version: 1, body: { blocks } };
}

function blockText(model, blockId) {
    const block = model.body.blocks.find(b => b.id === blockId);
    return (block?.content?.runs || []).map(r => r.text).join('');
}

// Edit on A (before->after), relay A's op-log batch onto B (which starts == before), assert B == after.
function relay(before, after) {
    const log = createCanvasOperationLog({ documentId: 'phaseB-relay', clientId: 'client-a' });
    const batch = log.recordLocalChange({ before, after });
    assert.ok(batch, 'edit must produce a batch');
    // C# would just transport `batch`; B applies it verbatim (wrap in the {batch:{operations}} envelope).
    const result = applyRemoteOperationBatch(before, { sequence: batch.localSequence, batch: { operations: batch.operations } });
    return result;
}

test('B1.2 relay insertText converges', () => {
    const result = relay(doc([paragraph('p1', 'Hello')]), doc([paragraph('p1', 'Hello world')]));
    assert.equal(result.success, true);
    assert.equal(blockText(result.model, 'p1'), 'Hello world');
});

test('B1.2 relay deleteText converges', () => {
    const result = relay(doc([paragraph('p1', 'Hello world')]), doc([paragraph('p1', 'Hello')]));
    assert.equal(result.success, true);
    assert.equal(blockText(result.model, 'p1'), 'Hello');
});

test('B1.2 relay insertBlock (new paragraph) converges', () => {
    const result = relay(doc([paragraph('p1', 'Hello')]), doc([paragraph('p1', 'Hello'), paragraph('p2', 'World')]));
    assert.equal(result.success, true);
    assert.equal(result.model.body.blocks.length, 2);
    assert.equal(blockText(result.model, 'p2'), 'World');
});

test('B1.2 relay deleteBlock converges', () => {
    const result = relay(doc([paragraph('p1', 'Hello'), paragraph('p2', 'World')]), doc([paragraph('p1', 'Hello')]));
    assert.equal(result.success, true);
    assert.equal(result.model.body.blocks.length, 1);
    assert.equal(result.model.body.blocks[0].id, 'p1');
});

test('B1.2 relay updateBlock (paragraph property) converges', () => {
    const result = relay(
        doc([paragraph('p1', 'Hello', { alignment: 'left' })]),
        doc([paragraph('p1', 'Hello', { alignment: 'center' })]));
    assert.equal(result.success, true);
    assert.equal(result.model.body.blocks[0].alignment, 'center');
});

test('B1.2 relay updateBlock (table cell edit, whole-table) converges', () => {
    const table = (cellText) => ({
        id: 'tbl1',
        type: 'table',
        content: { table: { rows: [{ cells: [{ id: 'cell1', blocks: [paragraph('cp1', cellText)] }] }] } },
    });
    const result = relay(doc([table('Hi')]), doc([table('Hi!')]));
    assert.equal(result.success, true);
    const tbl = result.model.body.blocks.find(b => b.id === 'tbl1');
    assert.equal(tbl.content.table.rows[0].cells[0].blocks[0].content.runs[0].text, 'Hi!');
});

test('B1.2 relay updateBlock (image resize) converges', () => {
    const image = (w) => ({ id: 'img1', type: 'image', content: { type: 'image', width: w, height: 50 } });
    const result = relay(doc([image(100)]), doc([image(220)]));
    assert.equal(result.success, true);
    const img = result.model.body.blocks.find(b => b.id === 'img1');
    assert.equal(img.content.width, 220);
});
