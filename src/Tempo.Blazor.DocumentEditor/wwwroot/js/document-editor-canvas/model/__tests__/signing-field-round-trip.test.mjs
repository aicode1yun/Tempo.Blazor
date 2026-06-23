import test from 'node:test';
import assert from 'node:assert/strict';
import { createCanvasDocumentModel } from '../canvas-document-model.mjs';

// A signing field run must survive the model normalization (getModelJson -> replaceModel) without
// being coerced away — both in a body block and in a header/footer block (plan S2.3/S2.4).

function modelWithSigningFields() {
    return {
        documentId: 'signing-round-trip',
        body: {
            blocks: [
                {
                    id: 'p1',
                    type: 'paragraph',
                    content: {
                        type: 'paragraph',
                        runs: [
                            { id: 'r1', type: 'text', text: 'Signed by ', marks: [] },
                            {
                                id: 'r2',
                                type: 'signingField',
                                text: '',
                                marks: [],
                                signingField: { uuid: 'field-body', fieldType: 'signature', submitterUuid: 'signer', required: true, label: 'Sign' },
                            },
                        ],
                    },
                },
            ],
        },
        headersFooters: [
            {
                id: 'footer-1',
                type: 'footer',
                scope: 'default',
                blocks: [
                    {
                        id: 'fb1',
                        type: 'paragraph',
                        content: {
                            type: 'paragraph',
                            runs: [
                                {
                                    id: 'fr1',
                                    type: 'signingField',
                                    text: '',
                                    marks: [],
                                    signingField: { uuid: 'field-footer', fieldType: 'initials', submitterUuid: 'signer', label: 'Initial' },
                                },
                            ],
                        },
                    },
                ],
            },
        ],
    };
}

test('a signing field run in a body block survives normalization round-trip', () => {
    const once = createCanvasDocumentModel(modelWithSigningFields());
    const twice = createCanvasDocumentModel(JSON.parse(JSON.stringify(once)));

    const run = twice.body.blocks[0].content.runs.find(item => item.id === 'r2');
    assert.ok(run, 'the signing field run is retained');
    assert.equal(run.type, 'signingField', 'the run type is not coerced to text');
    assert.ok(run.signingField, 'the signing field payload is retained');
    assert.equal(run.signingField.uuid, 'field-body');
    assert.equal(run.signingField.fieldType, 'signature');
    assert.equal(run.signingField.submitterUuid, 'signer');
    assert.equal(run.signingField.required, true);
});

test('a signing field run in a header/footer block survives normalization round-trip', () => {
    const once = createCanvasDocumentModel(modelWithSigningFields());
    const twice = createCanvasDocumentModel(JSON.parse(JSON.stringify(once)));

    const footer = twice.headersFooters.find(item => item.id === 'footer-1');
    assert.ok(footer, 'the footer is retained');
    const run = footer.blocks[0].content.runs.find(item => item.id === 'fr1');
    assert.ok(run, 'the footer signing field run is retained');
    assert.equal(run.type, 'signingField');
    assert.equal(run.signingField.uuid, 'field-footer');
    assert.equal(run.signingField.fieldType, 'initials');
});
