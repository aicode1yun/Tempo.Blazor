import assert from 'node:assert/strict';
import test from 'node:test';
import { mergeSoftHyphenTokens } from '../line-breaker.mjs';

const shy = '\u00AD';

test('mergeSoftHyphenTokens folds repeated manual breaks into one word token', () => {
    const result = mergeSoftHyphenTokens([
        word('edi', 0, 3, 'run-a'),
        softHyphen(3, 'run-a'),
        word('tor', 4, 7, 'run-a'),
        softHyphen(7, 'run-a'),
        word('ial', 8, 11, 'run-a'),
    ]);

    assert.equal(result.length, 1);
    assert.equal(result[0].text, `edi${shy}tor${shy}ial`);
    assert.equal(result[0].start, 0);
    assert.equal(result[0].end, 11);
    assert.equal(result[0].length, 11);
});

test('mergeSoftHyphenTokens preserves soft hyphen opportunities across run boundaries', () => {
    const result = mergeSoftHyphenTokens([
        word('cross', 0, 5, 'run-a'),
        softHyphen(5, 'run-a'),
        word('run', 6, 9, 'run-b'),
    ]);

    assert.equal(result.length, 1);
    assert.equal(result[0].text, `cross${shy}run`);
    assert.equal(result[0].runId, null);
    assert.equal(result[0].end, 9);
});

function word(text, start, end, runId) {
    return { type: 'word', text, start, end, length: end - start, runId };
}

function softHyphen(start, runId) {
    return { type: 'softHyphen', text: shy, start, end: start + 1, length: 1, runId, breakAfter: true };
}
