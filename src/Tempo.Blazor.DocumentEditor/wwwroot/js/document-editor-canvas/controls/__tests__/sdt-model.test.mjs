import assert from 'node:assert/strict';
import test from 'node:test';
import {
    CONTENT_CONTROL_KINDS,
    contentControlDisplayText,
    normalizeContentControl,
    normalizeContentControlBlock,
    normalizeContentControlRun,
    setContentControlValue,
    validateContentControl,
} from '../sdt-model.mjs';

test('content control normalization preserves inline and block metadata deterministically', () => {
    const inline = normalizeContentControlRun({
        id: 'name-run',
        contentControl: {
            control: {
                controlId: 'customer-name',
                kind: 'plainText',
                alias: 'Customer name',
                tag: 'customer.name',
                placeholderText: 'Enter customer name',
                isRequired: true,
                lockContent: false,
                lockDeletion: true,
                value: { text: '' },
            },
            runs: [{ id: 'nested-run', type: 'text', text: 'Nested' }],
        },
    });
    const block = normalizeContentControlBlock({
        id: 'address-block-control',
        contentControl: {
            control: { kind: 'richText', placeholderText: 'Address' },
            blocks: [{ id: 'address-p', type: 'paragraph' }],
        },
    });

    assert.equal(inline.control.controlId, 'customer-name');
    assert.equal(inline.control.kind, CONTENT_CONTROL_KINDS.plainText);
    assert.equal(inline.control.scope, 'inline');
    assert.equal(inline.control.alias, 'Customer name');
    assert.equal(inline.control.tag, 'customer.name');
    assert.equal(inline.control.lockDeletion, true);
    assert.equal(inline.control.validation.valid, false);
    assert.equal(inline.control.validation.reason, 'required');
    assert.equal(inline.runs.length, 1);
    assert.equal(block.control.controlId, 'address-block-control');
    assert.equal(block.control.scope, 'block');
    assert.equal(block.blocks[0].id, 'address-p');
});

test('content control values render text checkbox dropdown date and picture displays', () => {
    const dropdown = normalizeContentControl({
        kind: 'dropDown',
        value: { selectedValue: 'pro' },
        items: [
            { displayText: 'Basic', value: 'basic' },
            { displayText: 'Professional', value: 'pro' },
        ],
    }, { fallbackId: 'plan-control' });

    assert.equal(contentControlDisplayText({ kind: 'checkbox', value: { checked: true } }), '☑');
    assert.equal(contentControlDisplayText({ kind: 'checkbox', value: { checked: false } }), '☐');
    assert.equal(contentControlDisplayText(dropdown), 'Professional');
    assert.equal(contentControlDisplayText({ kind: 'date', value: { dateIso: '2026-06-05' } }), '2026-06-05');
    assert.equal(contentControlDisplayText({ kind: 'picture', value: { assetId: 'asset-1' } }), 'asset-1');
});

test('set value enforces locks and validates required dropdown values', () => {
    const locked = normalizeContentControl({
        controlId: 'locked-name',
        kind: 'plainText',
        lockContent: true,
        value: { text: 'Protected' },
    });
    const lockedResult = setContentControlValue(locked, { text: 'Changed' });
    const dropdown = normalizeContentControl({
        controlId: 'plan',
        kind: 'dropDown',
        isRequired: true,
        items: [{ displayText: 'Basic', value: 'basic' }],
        value: { selectedValue: '' },
    });
    const invalidOption = setContentControlValue(dropdown, { selectedValue: 'enterprise' });

    assert.equal(lockedResult.changed, false);
    assert.equal(lockedResult.reason, 'locked');
    assert.equal(validateContentControl(dropdown).valid, false);
    assert.equal(validateContentControl(dropdown).reason, 'required');
    assert.equal(invalidOption.control.validation.valid, false);
    assert.equal(invalidOption.control.validation.reason, 'unknownOption');
});
