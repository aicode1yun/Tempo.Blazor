import assert from 'node:assert/strict';
import test from 'node:test';
import { findSigningFieldAtSelection } from '../signing-field-selection.mjs';

// When the caret sits on a signing field box, the host needs to know which field is selected so it can
// open the properties panel (plan S2.11). A header/footer field additionally reports its headerFooterId
// + scope + repeats flag so the panel can show "repeats on every page".

function model() {
    return {
        body: {
            blocks: [
                {
                    id: 'p1',
                    type: 'paragraph',
                    content: {
                        type: 'paragraph',
                        runs: [
                            { id: 'r1', type: 'text', text: 'Sign: ', marks: [] },
                            { id: 'r2', type: 'signingField', text: '', marks: [], signingField: { uuid: 'body-field', fieldType: 'signature', submitterUuid: 'signer' } },
                        ],
                    },
                },
            ],
        },
        headersFooters: [
            {
                id: 'footer-1',
                type: 1,
                scope: 0,
                blocks: [
                    {
                        id: 'f1',
                        type: 'paragraph',
                        content: { type: 'paragraph', runs: [{ id: 'fr1', type: 'signingField', text: '', marks: [], signingField: { uuid: 'footer-field', fieldType: 'initials', submitterUuid: 'signer' } }] },
                    },
                ],
            },
        ],
    };
}

test('a caret on a body signing field reports the field with no header/footer scope', () => {
    const found = findSigningFieldAtSelection(model(), { focus: { blockId: 'p1', offset: 6 }, anchor: { blockId: 'p1', offset: 6 } });

    assert.ok(found, 'the body field is detected');
    assert.equal(found.uuid, 'body-field');
    assert.equal(found.fieldType, 'signature');
    assert.equal(found.submitterUuid, 'signer');
    assert.equal(found.headerFooterId, '');
    assert.equal(found.repeats, false);
});

test('a caret on a footer signing field reports headerFooterId, scope and repeats', () => {
    const found = findSigningFieldAtSelection(model(), { focus: { blockId: 'f1', offset: 0 }, anchor: { blockId: 'f1', offset: 0 } });

    assert.ok(found, 'the footer field is detected');
    assert.equal(found.uuid, 'footer-field');
    assert.equal(found.headerFooterId, 'footer-1');
    assert.equal(found.scope, 'Primary');
    assert.equal(found.repeats, true);
});

test('no signing field at the caret returns null', () => {
    const found = findSigningFieldAtSelection(model(), { focus: { blockId: 'p1', offset: 0 }, anchor: { blockId: 'p1', offset: 0 } });
    assert.equal(found, null);
});
