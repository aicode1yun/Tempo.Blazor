import assert from 'node:assert/strict';
import test from 'node:test';
import { findCanvasMatches, replaceAllCanvasMatches, replaceCanvasMatch } from '../search-engine.mjs';

test('canvas search supports regex matches and replacement backreferences', () => {
    const model = modelWithText('p1', 'Alpha-12 Beta-34 Alpha-56');
    const matches = findCanvasMatches(model, {
        query: '(Alpha)-(\\d+)',
        options: { regex: true, caseSensitive: true },
    });

    assert.equal(matches.length, 2);
    assert.deepEqual(matches[0].captures, ['Alpha', '12']);

    const replaced = replaceCanvasMatch(model, selection('p1'), matches[0], '$2/$1');
    assert.equal(replaced.changed, true);
    assert.equal(replaced.model.body.blocks[0].content.runs.map(run => run.text).join(''), '12/Alpha Beta-34 Alpha-56');
});

test('canvas search replace all processes matches from the end and keeps offsets stable', () => {
    const model = modelWithText('p1', 'AB-1 AB-22 AB-333');
    const matches = findCanvasMatches(model, { query: 'AB-(\\d+)', options: { regex: true } });

    const replaced = replaceAllCanvasMatches(model, selection('p1'), matches, 'N$1');

    assert.equal(replaced.changed, true);
    assert.equal(replaced.replaceCount, 3);
    assert.equal(replaced.model.body.blocks[0].content.runs.map(run => run.text).join(''), 'N1 N22 N333');
});

test('canvas search respects whole-word matching', () => {
    const model = modelWithText('p1', 'tempo tempos Tempo');
    const matches = findCanvasMatches(model, {
        query: 'tempo',
        options: { wholeWord: true, caseSensitive: false },
    });

    assert.equal(matches.length, 2);
    assert.equal(matches[0].start, 0);
    assert.equal(matches[1].start, 13);
});

function modelWithText(id, text) {
    return {
        body: {
            blocks: [
                {
                    id,
                    type: 'paragraph',
                    order: 10,
                    content: { type: 'paragraph', runs: [{ id: `${id}-r`, type: 'text', text, marks: [] }] },
                },
            ],
        },
    };
}

function selection(blockId) {
    return { anchor: { blockId, offset: 0 }, focus: { blockId, offset: 0 } };
}
