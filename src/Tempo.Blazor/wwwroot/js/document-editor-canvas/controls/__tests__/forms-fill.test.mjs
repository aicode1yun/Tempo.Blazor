import assert from 'node:assert/strict';
import test from 'node:test';
import { applyContentControlCommand, listContentControls } from '../forms-mode.mjs';

test('forms fill mode supports keyboard field navigation and every editable content-control value kind', () => {
    let model = createFormsFillModel();
    let selection = { focus: { blockId: 'form-p1', offset: 0 }, anchor: { blockId: 'form-p1', offset: 0 } };

    const focusName = applyContentControlCommand(model, selection, 'focusContentControl', { controlId: 'name' });
    assert.equal(focusName.selectionChanged, true);
    assert.equal(focusName.controlId, 'name');

    const next = applyContentControlCommand(model, focusName.selection, 'nextContentControl', {});
    assert.equal(next.selectionChanged, true);
    assert.equal(next.controlId, 'approved');

    const previous = applyContentControlCommand(model, next.selection, 'previousContentControl', {});
    assert.equal(previous.selectionChanged, true);
    assert.equal(previous.controlId, 'name');
    selection = previous.selection;

    model = applyContentControlCommand(model, selection, 'setContentControlText', {
        controlId: 'name',
        text: 'Ada Lovelace',
    }).model;
    model = applyContentControlCommand(model, selection, 'toggleContentControl', {
        controlId: 'approved',
    }).model;
    model = applyContentControlCommand(model, selection, 'selectContentControlOption', {
        controlId: 'plan',
        value: 'enterprise',
    }).model;
    model = applyContentControlCommand(model, selection, 'setContentControlDate', {
        controlId: 'renewal',
        dateIso: '2026-12-31',
    }).model;
    model = applyContentControlCommand(model, selection, 'setContentControlComboText', {
        controlId: 'contact',
        text: 'Partner portal',
    }).model;
    model = applyContentControlCommand(model, selection, 'setContentControlPicture', {
        controlId: 'photo',
        assetId: 'contract-evidence-asset',
    }).model;

    assert.equal(control(model, 'name').value.text, 'Ada Lovelace');
    assert.equal(control(model, 'approved').value.checked, true);
    assert.equal(control(model, 'plan').value.selectedValue, 'enterprise');
    assert.equal(control(model, 'renewal').value.dateIso, '2026-12-31');
    assert.equal(control(model, 'contact').value.text, 'Partner portal');
    assert.equal(control(model, 'photo').value.assetId, 'contract-evidence-asset');
    assert.equal(control(model, 'name').validation.valid, true);
});

test('forms fill mode rejects invalid required choices and locked content edits', () => {
    const model = createFormsFillModel();
    const selection = { focus: { blockId: 'form-p1', offset: 0 }, anchor: { blockId: 'form-p1', offset: 0 } };

    const invalidPlan = applyContentControlCommand(model, selection, 'selectContentControlOption', {
        controlId: 'plan',
        value: 'unlisted',
    });
    const locked = applyContentControlCommand(model, selection, 'setContentControlText', {
        controlId: 'locked',
        text: 'Changed',
    });

    assert.equal(invalidPlan.changed, true);
    assert.equal(invalidPlan.validation.valid, false);
    assert.equal(invalidPlan.validation.reason, 'unknownOption');
    assert.equal(locked.changed, false);
    assert.equal(locked.reason, 'locked');
});

function control(model, controlId) {
    return listContentControls(model).find(item => item.control.controlId === controlId).control;
}

function createFormsFillModel() {
    return {
        documentId: 'phase-e9-forms-fill',
        body: {
            blocks: [
                {
                    id: 'form-p1',
                    type: 'paragraph',
                    content: {
                        type: 'paragraph',
                        runs: [
                            run('name-run', 'name', 'plainText', { text: '' }, { placeholderText: 'Customer name', isRequired: true }),
                            { id: 'space-1', type: 'text', text: ' ', marks: [] },
                            run('approved-run', 'approved', 'checkbox', { checked: false }, { placeholderText: 'Approved' }),
                            { id: 'space-2', type: 'text', text: ' ', marks: [] },
                            run('plan-run', 'plan', 'dropDown', { selectedValue: 'basic' }, {
                                isRequired: true,
                                items: [
                                    { value: 'basic', displayText: 'Basic' },
                                    { value: 'enterprise', displayText: 'Enterprise' },
                                ],
                            }),
                            { id: 'space-3', type: 'text', text: ' ', marks: [] },
                            run('renewal-run', 'renewal', 'date', { dateIso: '' }, { placeholderText: 'Renewal' }),
                            { id: 'space-4', type: 'text', text: ' ', marks: [] },
                            run('contact-run', 'contact', 'comboBox', { text: '' }, {
                                placeholderText: 'Contact',
                                items: [{ value: 'email', displayText: 'Email' }],
                            }),
                            { id: 'space-5', type: 'text', text: ' ', marks: [] },
                            run('photo-run', 'photo', 'picture', { assetId: '' }, { placeholderText: 'Photo' }),
                            { id: 'space-6', type: 'text', text: ' ', marks: [] },
                            run('locked-run', 'locked', 'plainText', { text: 'Locked' }, { lockContent: true }),
                        ],
                    },
                },
            ],
        },
    };
}

function run(id, controlId, kind, value, extra = {}) {
    return {
        id,
        type: 'contentControl',
        text: '',
        marks: [],
        contentControl: {
            control: {
                controlId,
                kind,
                scope: 'inline',
                alias: extra.alias || '',
                tag: extra.tag || '',
                placeholderText: extra.placeholderText || '',
                isRequired: extra.isRequired === true,
                lockContent: extra.lockContent === true,
                lockDelete: extra.lockDelete === true,
                value,
                items: extra.items || [],
            },
            runs: [],
        },
    };
}
