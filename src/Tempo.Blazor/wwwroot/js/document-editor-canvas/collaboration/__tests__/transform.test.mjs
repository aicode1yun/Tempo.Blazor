import assert from 'node:assert/strict';
import test from 'node:test';
import { diffModels } from '../op-log.mjs';
import { applyRemoteOperationBatch, transformOperationAgainstLocal } from '../transform.mjs';

test('canvas operation log serializes local text insert as realtime DocumentOperation shape', () => {
    const before = documentModel('collab-op-log', 'p1', 'Hello');
    const after = documentModel('collab-op-log', 'p1', 'Hello world');
    const operations = diffModels(before, after, { clientId: 'client-a', sequence: 1, source: 'typing' });

    assert.equal(operations.length, 1);
    assert.equal(operations[0].type, 'insertText');
    assert.equal(operations[0].target.blockId, 'p1');
    assert.equal(operations[0].target.offset, 5);
    assert.equal(operations[0].text, ' world');
    assert.equal(JSON.parse(JSON.stringify(operations))[0].schemaVersion, 1);
});

test('remote insert transforms deterministically after a pending local insert at the same block', () => {
    const remote = {
        operationId: 'remote-b',
        type: 'insertText',
        target: { blockId: 'p1', offset: 3, length: 1 },
        metadata: { clientId: 'client-b' },
        text: 'B',
    };
    const local = {
        operationId: 'local-a',
        type: 'insertText',
        target: { blockId: 'p1', offset: 3, length: 1 },
        metadata: { clientId: 'client-a' },
        text: 'A',
    };

    const transformed = transformOperationAgainstLocal(remote, [local]);

    assert.equal(transformed.target.offset, 4);
});

test('remote batches converge the canvas model through insert, delete, and block updates', () => {
    const model = documentModel('collab-transform', 'p1', 'ABCD');
    const result = applyRemoteOperationBatch(model, {
        sequence: 7,
        batch: {
            operations: [
                {
                    operationId: 'remote-delete',
                    type: 'deleteText',
                    target: { blockId: 'p1', offset: 1, length: 2 },
                    text: 'BC',
                },
                {
                    operationId: 'remote-insert',
                    type: 'insertText',
                    target: { blockId: 'p1', offset: 1, length: 1 },
                    text: 'x',
                },
            ],
        },
    });

    assert.equal(result.success, true);
    assert.deepEqual(result.appliedOperationIds, ['remote-delete', 'remote-insert']);
    assert.equal(result.model.body.blocks[0].content.runs[0].text, 'AxD');
});

test('moving a heading invalidates outline and table of contents revisions', () => {
    const model = outlineModel('collab-heading-move');

    const headingMove = applyRemoteOperationBatch(model, {
        sequence: 8,
        batch: {
            operations: [{
                operationId: 'remote-move-heading',
                type: 'moveBlock',
                target: { blockId: 'h2', order: 0 },
            }],
        },
    });

    assert.equal(headingMove.success, true);
    assert.equal(headingMove.model.body.blocks[0].id, 'h2');
    assert.equal(headingMove.model.outlineRevision, 4);
    assert.equal(headingMove.model.tableOfContentsRevision, 6);

    const paragraphMove = applyRemoteOperationBatch(model, {
        sequence: 9,
        batch: {
            operations: [{
                operationId: 'remote-move-paragraph',
                type: 'moveBlock',
                target: { blockId: 'p1', order: 0 },
            }],
        },
    });

    assert.equal(paragraphMove.success, true);
    assert.equal(paragraphMove.model.body.blocks[0].id, 'p1');
    assert.equal(paragraphMove.model.outlineRevision, 3);
    assert.equal(paragraphMove.model.tableOfContentsRevision, 5);
});

function documentModel(documentId, blockId, text) {
    return {
        documentId,
        body: {
            blocks: [{
                id: blockId,
                type: 'paragraph',
                content: {
                    type: 'paragraph',
                    runs: [{ id: `${blockId}-run`, type: 'text', text, marks: [] }],
                },
            }],
        },
    };
}

function outlineModel(documentId) {
    const blocks = [
        heading('h1', 'Architecture', 1),
        paragraph('p1', 'Body copy'),
        heading('h2', 'Implementation', 2),
    ];

    return {
        documentId,
        outlineRevision: 3,
        tableOfContentsRevision: 5,
        body: { blocks },
        sections: [{ id: 's1', order: 0, blocks }],
    };
}

function heading(id, text, level) {
    return {
        id,
        sectionId: 's1',
        type: 'heading',
        content: {
            type: 'heading',
            headingLevel: level,
            outlineLevel: level,
            runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }],
        },
    };
}

function paragraph(id, text) {
    return {
        id,
        sectionId: 's1',
        type: 'paragraph',
        content: {
            type: 'paragraph',
            runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }],
        },
    };
}
