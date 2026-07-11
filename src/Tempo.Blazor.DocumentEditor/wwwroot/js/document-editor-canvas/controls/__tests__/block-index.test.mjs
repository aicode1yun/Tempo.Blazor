import assert from 'node:assert/strict';
import test from 'node:test';
import { getBlockIndex } from '../block-index.mjs';
import { findContentControlAtSelection } from '../content-control-selection.mjs';
import { findSigningFieldAtSelection } from '../signing-field-selection.mjs';

// Fáze 23 (code review N2): selection-state čtečky dělaly plný DFS dokumentu po každém settled
// editu — nově sdílí memoizovaný per-model index. Testy drží (a) memoizaci per model reference,
// (b) paritu pokrytí s původními průchody obou modulů.

function buildModel() {
    return {
        documentId: 'block-index-test',
        body: {
            blocks: [
                paragraph('p1', 'Hello world'),
                {
                    id: 'table-1',
                    type: 'table',
                    content: {
                        type: 'table',
                        table: {
                            rows: [{
                                cells: [{ blocks: [paragraph('cell-block', 'In cell')] }],
                            }],
                        },
                    },
                },
                {
                    id: 'control-host',
                    type: 'contentControl',
                    content: {
                        type: 'contentControl',
                        contentControl: {
                            control: { controlId: 'cc-scope', kind: 'richText' },
                            blocks: [paragraph('nested-in-control', 'Nested')],
                        },
                    },
                },
            ],
        },
        headersFooters: [{
            id: 'hf-1',
            type: 'header',
            scope: 'firstPage',
            blocks: [paragraph('hf-block', 'Header text')],
        }],
    };
}

function paragraph(id, text) {
    return {
        id,
        type: 'paragraph',
        content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
    };
}

test('block index is memoized per model reference and rebuilt for a new model', () => {
    const model = buildModel();
    const first = getBlockIndex(model);
    const second = getBlockIndex(model);
    assert.strictEqual(first, second, 'same model reference must reuse the cached index (no re-walk)');

    const edited = buildModel();
    assert.notStrictEqual(getBlockIndex(edited), first, 'a copy-on-write edit (new model ref) rebuilds the index');
});

test('index covers body, table cells, control-nested and header/footer blocks with metadata', () => {
    const index = getBlockIndex(buildModel());

    assert.equal(index.get('p1')?.headerFooterId, '');
    assert.equal(index.get('cell-block')?.nestedInControl, false, 'table cell blocks are body scope');
    assert.equal(index.get('nested-in-control')?.nestedInControl, true);
    assert.equal(index.get('hf-block')?.headerFooterId, 'hf-1');
    assert.equal(index.get('missing'), undefined);
});

test('coverage parity: content control lookup skips header/footer, signing lookup skips control-nested blocks', () => {
    const model = buildModel();

    // Content control čtečka historicky NEprohledávala header/footer bloky.
    const inHeader = findContentControlAtSelection(model, { focus: { blockId: 'hf-block', offset: 0 } });
    assert.equal(inHeader, null, 'content control lookup must not resolve header/footer blocks');

    // Signing čtečka historicky NEprohledávala bloky uvnitř block-scope content controlů.
    const inControl = findSigningFieldAtSelection(model, { focus: { blockId: 'nested-in-control', offset: 0 } });
    assert.equal(inControl, null, 'signing lookup must not resolve control-nested blocks');
});

test('signing field in a header/footer block keeps its scope metadata through the shared index', () => {
    const model = buildModel();
    model.headersFooters[0].blocks[0].content.runs.unshift({
        id: 'sig-run',
        type: 'signingField',
        signingField: { uuid: 'sig-1', fieldType: 'signature', submitterUuid: 'role-1' },
    });

    const found = findSigningFieldAtSelection(model, { focus: { blockId: 'hf-block', offset: 0 } });
    assert.ok(found, 'signing field at the caret in a header block must resolve');
    assert.equal(found.uuid, 'sig-1');
    assert.equal(found.headerFooterId, 'hf-1');
    assert.equal(found.scope, 'FirstPage');
    assert.equal(found.repeats, true);
});
