import assert from 'node:assert/strict';
import test from 'node:test';
import { resolveNumberingState } from '../numbering-engine.mjs';

test('numbering sequences recompute after insert delete and move operations', () => {
    const model = createModel([
        item('a', 10),
        item('b', 20),
        item('c', 30),
    ]);

    assert.deepEqual(labels(model), ['1.', '2.', '3.']);

    model.body.blocks.splice(1, 0, item('inserted', 15));
    assert.deepEqual(labels(model), ['1.', '2.', '3.', '4.']);

    model.body.blocks = model.body.blocks.filter(block => block.id !== 'b');
    assert.deepEqual(labels(model), ['1.', '2.', '3.']);

    const moved = model.body.blocks.find(block => block.id === 'c');
    moved.order = 5;
    assert.deepEqual(labels(model), ['1.', '2.', '3.']);
    assert.deepEqual(orderedIds(model), ['c', 'a', 'inserted']);
});

test('restart numbering set value and continue flags produce stable labels', () => {
    const model = createModel([
        item('first', 10),
        item('second', 20),
        item('restart-at-seven', 30, { restartNumbering: true, numberingValue: 7 }),
        item('after-restart', 40),
        item('continue', 50, { continueNumbering: true }),
    ]);

    assert.deepEqual(labels(model), ['1.', '2.', '7.', '8.', '9.']);
});

function labels(model) {
    const blocks = [...model.body.blocks].sort((left, right) => left.order - right.order);
    const state = resolveNumberingState(model, blocks);
    return blocks.map(block => state.labels.get(block.id));
}

function orderedIds(model) {
    return [...model.body.blocks].sort((left, right) => left.order - right.order).map(block => block.id);
}

function createModel(blocks) {
    return {
        documentId: 'e1-numbering-sequence',
        body: { blocks },
    };
}

function item(id, order, list = {}) {
    return {
        id,
        type: 'list',
        order,
        content: {
            type: 'list',
            list: {
                ordered: true,
                indentLevel: 0,
                startNumber: 1,
                ...list,
            },
            runs: [{ id: `${id}-run`, type: 'text', text: id, marks: [] }],
        },
    };
}
