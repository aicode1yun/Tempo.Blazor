import assert from 'node:assert/strict';
import test from 'node:test';
import { applyBlockStyleToBlock, blockStyleState, resolveBlockStyle } from '../heading-style.mjs';

test('heading style resolver exposes name inheritance outline level and direct formatting metadata', () => {
    const heading = resolveBlockStyle('Heading 3');
    assert.equal(heading.id, 'heading-3');
    assert.equal(heading.name, 'Heading 3');
    assert.equal(heading.basedOn, 'Heading 2');
    assert.equal(heading.outlineLevel, 3);
    assert.equal(heading.directFormatting, false);

    const quote = resolveBlockStyle('Quote');
    assert.equal(quote.type, 'quote');
    assert.equal(quote.basedOn, 'Normal');
});

test('block style application round trips semantic type level and style name', () => {
    const block = {
        id: 'block-1',
        type: 'paragraph',
        content: {
            type: 'paragraph',
            runs: [{ id: 'run-1', type: 'text', text: 'Heading text', marks: [{ type: 'italic', value: null }] }],
        },
    };

    const result = applyBlockStyleToBlock(block, 'Heading 6');
    assert.equal(result.changed, true);
    assert.equal(result.block.type, 'heading');
    assert.equal(result.block.content.headingLevel, 6);
    assert.equal(result.block.content.styleId, 'heading-6');
    assert.equal(result.block.content.styleName, 'Heading 6');
    assert.equal(blockStyleState(result.block).name, 'Heading 6');
    assert.deepEqual(result.block.content.runs[0].marks.map(mark => mark.type), ['italic']);

    const normal = applyBlockStyleToBlock(result.block, 'Normal');
    assert.equal(normal.block.type, 'paragraph');
    assert.equal(blockStyleState(normal.block).name, 'Normal');
});
