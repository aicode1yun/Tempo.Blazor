import test from 'node:test';
import assert from 'node:assert/strict';
import { extractAnnotations, countModelWords } from './annotations-state.mjs';

test('returns the model comment + revision lists', () => {
    const model = {
        comments: [{ id: 'c1', text: 'hi', status: 'Open' }],
        revisions: [{ id: 'r1', type: 'Insertion', action: 'Pending' }],
    };
    const a = extractAnnotations(model);
    assert.deepEqual(a.comments, model.comments);
    assert.deepEqual(a.revisions, model.revisions);
});

test('missing / empty model yields empty arrays (never null)', () => {
    for (const input of [null, undefined, {}, 'x', 42]) {
        const a = extractAnnotations(input);
        assert.ok(Array.isArray(a.comments) && a.comments.length === 0);
        assert.ok(Array.isArray(a.revisions) && a.revisions.length === 0);
    }
});

test('tolerates PascalCase keys (canonical C# serialisation casing)', () => {
    const a = extractAnnotations({ Comments: [{ id: 'c1' }], Revisions: [{ id: 'r1' }] });
    assert.equal(a.comments.length, 1);
    assert.equal(a.revisions.length, 1);
});

test('lowercase wins over PascalCase when both present', () => {
    const a = extractAnnotations({ comments: [{ id: 'lower' }], Comments: [{ id: 'upper' }] });
    assert.equal(a.comments[0].id, 'lower');
});

test('countModelWords counts run text across body blocks (incl. nested cells)', () => {
    const model = {
        body: {
            blocks: [
                { content: { runs: [{ type: 'text', text: 'Hello world' }, { type: 'text', text: ' again' }] } },
                {
                    type: 'table',
                    rows: [{ cells: [{ content: { blocks: [{ content: { runs: [{ text: 'cell text here' }] } }] } }] }],
                },
            ],
        },
    };
    // "Hello world again" (3) + "cell text here" (3) = 6
    assert.equal(countModelWords(model), 6);
});

test('countModelWords returns 0 for empty / missing body', () => {
    assert.equal(countModelWords(null), 0);
    assert.equal(countModelWords({}), 0);
    assert.equal(countModelWords({ body: { blocks: [{ content: { runs: [{ text: '   ' }] } }] } }), 0);
});
