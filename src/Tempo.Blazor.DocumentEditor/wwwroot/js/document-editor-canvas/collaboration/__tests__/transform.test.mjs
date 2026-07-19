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

test('operations resolve blocks nested in content controls (template sections)', () => {
    const model = contentControlModel('collab-cc');

    const result = applyRemoteOperationBatch(model, {
        sequence: 11,
        batch: {
            operations: [
                {
                    operationId: 'cc-insert',
                    type: 'insertText',
                    target: { blockId: 'cc-child', offset: 4 },
                    text: ' upravený',
                },
                {
                    operationId: 'cc-mark',
                    type: 'addInlineMark',
                    target: { blockId: 'cc-child', offset: 0, length: 4 },
                    mark: { type: 'bold' },
                },
                {
                    operationId: 'cc-move',
                    type: 'moveBlock',
                    target: { blockId: 'cc-child-2', order: 0 },
                },
            ],
        },
    });

    assert.equal(result.success, true);
    const control = result.model.body.blocks[0].content.contentControl;
    // Index move inside the control container.
    assert.equal(control.blocks[0].id, 'cc-child-2');
    const child = control.blocks.find(block => block.id === 'cc-child');
    const text = child.content.runs.map(run => run.text).join('');
    assert.equal(text, 'Text upravený');
    const bold = child.content.runs.find(run => (run.marks || []).some(mark => mark.type === 'bold'));
    assert.equal(bold.text, 'Text');
});

test('deleteBlock removes a content-control child but keeps the control', () => {
    const model = contentControlModel('collab-cc-delete');

    const result = applyRemoteOperationBatch(model, {
        sequence: 12,
        batch: {
            operations: [{
                operationId: 'cc-delete',
                type: 'deleteBlock',
                target: { blockId: 'cc-child-2' },
            }],
        },
    });

    assert.equal(result.success, true);
    const control = result.model.body.blocks[0].content.contentControl;
    assert.deepEqual(control.blocks.map(block => block.id), ['cc-child']);
    assert.equal(result.model.body.blocks[0].id, 'cc-1');
});

test('setBlockAttribute table.cell.text replaces the first cell paragraph text', () => {
    const model = tableModel('collab-cell-text', [{ id: 'cell-1', blocks: [paragraphInCell('cell-p', 'Původní')] }]);

    const result = applyRemoteOperationBatch(model, {
        sequence: 13,
        batch: {
            operations: [{
                operationId: 'cell-text-1',
                type: 'setBlockAttribute',
                target: { blockId: 'table-1', tableCellId: 'cell-1' },
                attributeName: 'table.cell.text',
                attributeValueJson: JSON.stringify('Nová hodnota'),
            }],
        },
    });

    assert.equal(result.success, true);
    const cell = result.model.body.blocks[0].content.table.rows[0].cells[0];
    assert.equal(cell.blocks.length, 1);
    assert.equal(cell.blocks[0].id, 'cell-p', 'the existing paragraph identity must survive');
    assert.equal(cell.blocks[0].content.runs.map(run => run.text).join(''), 'Nová hodnota');
});

test('setBlockAttribute table.cell.text creates a deterministic paragraph in an empty cell', () => {
    const model = tableModel('collab-cell-empty', [{ id: 'empty-cell', blocks: [] }]);

    const result = applyRemoteOperationBatch(model, {
        sequence: 14,
        batch: {
            operations: [{
                operationId: 'cell-text-2',
                type: 'setBlockAttribute',
                target: { blockId: 'table-1', tableCellId: 'empty-cell' },
                attributeName: 'table.cell.text',
                attributeValueJson: JSON.stringify('Vytvořeno'),
            }],
        },
    });

    assert.equal(result.success, true);
    const cell = result.model.body.blocks[0].content.table.rows[0].cells[0];
    assert.equal(cell.blocks.length, 1);
    // Mirrors the C# applier: the created paragraph id must be deterministic across replicas.
    assert.equal(cell.blocks[0].id, 'empty-cell-text');
    assert.equal(cell.blocks[0].content.runs.map(run => run.text).join(''), 'Vytvořeno');
});

function contentControlModel(documentId) {
    return {
        documentId,
        body: {
            blocks: [{
                id: 'cc-1',
                type: 'contentControl',
                content: {
                    type: 'contentControl',
                    contentControl: {
                        control: { controlId: 'cc-1', kind: 'richText', scope: 'block', metadata: { 'tmAssembly:branch': 'if' } },
                        blocks: [
                            {
                                id: 'cc-child',
                                type: 'paragraph',
                                content: { type: 'paragraph', runs: [{ id: 'cc-child-run', type: 'text', text: 'Text', marks: [] }] },
                            },
                            {
                                id: 'cc-child-2',
                                type: 'paragraph',
                                content: { type: 'paragraph', runs: [{ id: 'cc-child-2-run', type: 'text', text: 'Druhý', marks: [] }] },
                            },
                        ],
                    },
                },
            }],
        },
    };
}

function tableModel(documentId, cells) {
    return {
        documentId,
        body: {
            blocks: [{
                id: 'table-1',
                type: 'table',
                content: {
                    type: 'table',
                    table: { rows: [{ cells }] },
                },
            }],
        },
    };
}

function paragraphInCell(id, text) {
    return {
        id,
        type: 'paragraph',
        content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
    };
}

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
