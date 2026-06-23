import assert from 'node:assert/strict';
import test from 'node:test';
import {
    SIGNING_FIELD_TYPES,
    createSigningFieldRun,
    normalizeSigningFieldRun,
    normalizeSigningFieldType,
} from '../signing-field-model.mjs';

// Signing fields (plan S2) are a new canvas run type. The model module is position-agnostic: the
// same normalized field lives identically in a body block or a header/footer block; `uuid` is the
// stable key used later to group per-page layout occurrences into one field's areas.

test('normalizeSigningFieldRun fills defaults for an empty payload', () => {
    const field = normalizeSigningFieldRun({});

    assert.ok(field.uuid && typeof field.uuid === 'string', 'a stable uuid is generated');
    assert.equal(field.fieldType, 'text', 'the default field type is text');
    assert.equal(field.required, false);
    assert.equal(field.label, '');
    assert.equal(field.submitterUuid, '');
    assert.deepEqual(field.options, []);
    assert.ok(field.boxWidth > 0 && field.boxHeight > 0, 'a default box size is provided');
});

test('signature, initials and stamp get a larger default box than text and checkbox', () => {
    const area = type => {
        const field = normalizeSigningFieldRun({ signingField: { fieldType: type } });
        return field.boxWidth * field.boxHeight;
    };

    const checkbox = area('checkbox');
    const text = area('text');
    assert.ok(checkbox < text, 'a checkbox is the most compact field');
    for (const big of ['signature', 'initials', 'stamp']) {
        assert.ok(area(big) > text, `${big} reserves a larger box than text`);
        assert.ok(area(big) > checkbox, `${big} reserves a larger box than a checkbox`);
    }
});

test('an unknown field type falls back to text while preserving the rest of the payload', () => {
    const field = normalizeSigningFieldRun({
        signingField: { fieldType: 'frobnicate', submitterUuid: 'role-1', required: true, label: 'Mystery' },
    });

    assert.equal(field.fieldType, 'text');
    assert.equal(field.submitterUuid, 'role-1');
    assert.equal(field.required, true);
    assert.equal(field.label, 'Mystery');
});

test('normalizeSigningFieldRun tolerates PascalCase and camelCase payloads', () => {
    const pascal = normalizeSigningFieldRun({
        SigningField: { Uuid: 'field-1', FieldType: 'Signature', SubmitterUuid: 'signer', Required: true, Label: 'Sign here' },
    });
    assert.equal(pascal.uuid, 'field-1');
    assert.equal(pascal.fieldType, 'signature');
    assert.equal(pascal.submitterUuid, 'signer');
    assert.equal(pascal.required, true);
    assert.equal(pascal.label, 'Sign here');

    const camel = normalizeSigningFieldRun({ signingField: { fieldType: 'initials' } });
    assert.equal(camel.fieldType, 'initials');
});

test('explicit box dimensions and options are preserved', () => {
    const field = normalizeSigningFieldRun({
        signingField: {
            fieldType: 'select',
            boxWidth: 222,
            boxHeight: 33,
            options: [
                { value: 'a', label: 'Option A' },
                { value: 'b', label: 'Option B' },
            ],
        },
    });

    assert.equal(field.boxWidth, 222);
    assert.equal(field.boxHeight, 33);
    assert.equal(field.options.length, 2);
    assert.equal(field.options[0].value, 'a');
    assert.equal(field.options[0].label, 'Option A');
});

test('SIGNING_FIELD_TYPES mirrors the signing enum without heading/strikethrough', () => {
    assert.equal(SIGNING_FIELD_TYPES.heading, undefined);
    assert.equal(SIGNING_FIELD_TYPES.strikethrough, undefined);
    for (const type of [
        'text', 'signature', 'initials', 'date', 'dateNow', 'number', 'image', 'file', 'select',
        'checkbox', 'multiple', 'radio', 'cells', 'stamp', 'payment', 'phone', 'verification', 'kba',
    ]) {
        assert.equal(SIGNING_FIELD_TYPES[type], type, `${type} is an allowed inline signing type`);
    }
});

test('normalizeSigningFieldType is case-insensitive and falls back to text', () => {
    assert.equal(normalizeSigningFieldType('Signature'), 'signature');
    assert.equal(normalizeSigningFieldType('DateNow'), 'dateNow');
    assert.equal(normalizeSigningFieldType(''), 'text');
    assert.equal(normalizeSigningFieldType('nope'), 'text');
});

test('createSigningFieldRun produces a normalized signingField run object', () => {
    const run = createSigningFieldRun({ fieldType: 'signature', submitterUuid: 'signer', label: 'Sign' });

    assert.equal(run.type, 'signingField');
    assert.equal(run.text, '');
    assert.deepEqual(run.marks, []);
    assert.ok(run.id && typeof run.id === 'string');
    assert.equal(run.signingField.fieldType, 'signature');
    assert.equal(run.signingField.submitterUuid, 'signer');
    assert.equal(run.signingField.label, 'Sign');
    assert.ok(run.signingField.uuid);
});
