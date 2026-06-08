import assert from 'node:assert/strict';
import test from 'node:test';
import { ensureStyleStore, upsertStyle } from '../style-store.mjs';
import { directFormattingDelta, resolveBlockStyleFormatting, resolveStyle } from '../style-resolver.mjs';

test('style resolver inherits based-on paragraph and character formatting', () => {
    const model = { styles: [] };
    ensureStyleStore(model);
    upsertStyle(model, {
        id: 'contract-body',
        name: 'Contract Body',
        basedOn: 'normal',
        paragraphFormat: { spacingAfter: 12, leftIndent: 18 },
        characterFormat: { fontFamily: 'Aptos', fontSize: 10 },
    });
    upsertStyle(model, {
        id: 'contract-clause',
        name: 'Contract Clause',
        basedOn: 'contract-body',
        paragraphFormat: { leftIndent: 36 },
        characterFormat: { fontWeight: '700' },
    });

    const resolved = resolveStyle(model, 'Contract Clause');

    assert.equal(resolved.id, 'contract-clause');
    assert.equal(resolved.paragraphFormat.spacingAfter, 12);
    assert.equal(resolved.paragraphFormat.leftIndent, 36);
    assert.equal(resolved.characterFormat.fontFamily, 'Aptos');
    assert.equal(resolved.characterFormat.fontSize, 10);
    assert.equal(resolved.characterFormat.fontWeight, '700');
});

test('direct paragraph formatting is reported as an override delta', () => {
    const model = {
        styles: [{
            id: 'body-loose',
            name: 'Body Loose',
            type: 'paragraph',
            basedOn: 'normal',
            paragraphFormat: { alignment: 0, spacingAfter: 12 },
            characterFormat: {},
        }],
    };
    const block = {
        type: 'paragraph',
        paragraphProperties: { alignment: 2, spacingAfter: 12 },
        content: { type: 'paragraph', styleId: 'body-loose', styleName: 'Body Loose', runs: [] },
    };

    const resolved = resolveBlockStyleFormatting(model, block);

    assert.equal(resolved.paragraphFormat.alignment, 2);
    assert.equal(resolved.paragraphFormat.spacingAfter, 12);
    assert.deepEqual(directFormattingDelta(block, resolved.style), { alignment: 2 });
});
