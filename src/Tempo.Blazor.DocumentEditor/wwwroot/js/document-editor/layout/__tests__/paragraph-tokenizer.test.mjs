import assert from 'node:assert/strict';
import test from 'node:test';
import { createParagraphTokenizer } from '../paragraph-tokenizer.mjs';
import { fontStringFromStyle } from '../font-metrics.mjs';

test('allcaps mark uppercases layout text without emitting invalid font-variant-caps', () => {
    const tokenizer = createParagraphTokenizer({ normalizeImageObject: value => value });
    const paragraph = {
        id: 'caps-paragraph',
        content: {
            runs: [{
                id: 'caps-run',
                text: 'Canvas caps ß',
                marks: [{ type: 'allCaps' }],
                style: { fontFamily: 'Arial', fontSize: 16 },
            }],
        },
    };

    const result = tokenizer.tokensForParagraph(paragraph);
    const token = result.tokens.find(item => item.type === 'word');

    assert.equal(paragraph.content.runs[0].text, 'Canvas caps ß');
    assert.equal(result.text, 'CANVAS CAPS ß');
    assert.equal(token.text, 'CANVAS');
    assert.equal(token.style.textTransform, 'uppercase');
    assert.equal(token.style.fontVariantCaps || 'normal', 'normal');
    assert.equal(fontStringFromStyle(token.style).includes('all-small-caps'), false);
});
