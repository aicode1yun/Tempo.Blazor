import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';
import { applyContentControlCommand, findContentControlBlock, listContentControls } from '../forms-mode.mjs';

test('forms mode updates text checkbox and dropdown controls with lock enforcement', () => {
    let model = createFormsModel();
    const selection = { anchor: { blockId: 'form-p', offset: 0 }, focus: { blockId: 'form-p', offset: 0 } };

    const name = applyContentControlCommand(model, selection, 'setContentControlText', {
        controlId: 'customer-name',
        text: 'Ada Lovelace',
    });
    assert.equal(name.changed, true);
    model = name.model;
    assert.equal(findControl(model, 'customer-name').value.text, 'Ada Lovelace');
    assert.equal(model.body.blocks[0].content.runs[0].text, 'Ada Lovelace');

    const approved = applyContentControlCommand(model, selection, 'toggleContentControl', {
        controlId: 'approved',
    });
    assert.equal(approved.changed, true);
    model = approved.model;
    assert.equal(findControl(model, 'approved').value.checked, true);
    assert.equal(model.body.blocks[0].content.runs[1].text, '☑');

    const plan = applyContentControlCommand(model, selection, 'selectContentControlOption', {
        controlId: 'plan',
        selectedValue: 'enterprise',
    });
    assert.equal(plan.changed, true);
    model = plan.model;
    assert.equal(findControl(model, 'plan').value.selectedValue, 'enterprise');
    assert.equal(model.body.blocks[0].content.runs[2].text, 'Enterprise');

    const locked = applyContentControlCommand(model, selection, 'setContentControlText', {
        controlId: 'locked',
        text: 'Changed',
    });
    assert.equal(locked.changed, false);
    assert.equal(locked.reason, 'locked');
    assert.equal(findControl(model, 'locked').value.text, 'Read only');
});

test('forms mode updates date picture combo controls and navigates between fields', () => {
    let model = createFormsModel();
    const selection = { anchor: { blockId: 'form-p', offset: 0 }, focus: { blockId: 'form-p', offset: 0 } };

    const date = applyContentControlCommand(model, selection, 'setContentControlDate', {
        controlId: 'renewal',
        dateIso: '2026-12-31',
    });
    assert.equal(date.changed, true);
    model = date.model;
    assert.equal(findControl(model, 'renewal').value.dateIso, '2026-12-31');
    assert.equal(model.body.blocks[0].content.runs[3].text, '2026-12-31');

    const picture = applyContentControlCommand(model, selection, 'setContentControlPicture', {
        controlId: 'profile-photo',
        assetId: 'asset-contract-photo',
    });
    assert.equal(picture.changed, true);
    model = picture.model;
    assert.equal(findControl(model, 'profile-photo').value.assetId, 'asset-contract-photo');

    const combo = applyContentControlCommand(model, selection, 'setContentControlComboText', {
        controlId: 'contact-method',
        text: 'Partner portal',
    });
    assert.equal(combo.changed, true);
    model = combo.model;
    assert.equal(findControl(model, 'contact-method').value.text, 'Partner portal');

    const first = applyContentControlCommand(model, selection, 'focusContentControl', {
        controlId: 'customer-name',
    });
    assert.equal(first.selection.focus.blockId, 'form-p');
    assert.equal(first.controlId, 'customer-name');

    const next = applyContentControlCommand(model, first.selection, 'navigateContentControl', {
        direction: 'next',
    });
    assert.equal(next.changed, false);
    assert.equal(next.selectionChanged, true);
    assert.equal(next.controlId, 'approved');

    const previous = applyContentControlCommand(model, next.selection, 'previousContentControl', {});
    assert.equal(previous.controlId, 'customer-name');
});

test('forms mode repeating section commands add remove and undo through runtime history', () => {
    let model = createFormsModel();
    let selection = { anchor: { blockId: 'form-p', offset: 0 }, focus: { blockId: 'form-p', offset: 0 } };
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const added = runtime.execCommand('addRepeatingSectionItem', {
        controlId: 'addresses',
        text: 'Shipping address: 1 Infinite Loop',
    });
    assert.equal(added.handled, true);
    assert.equal(added.result.changed, true);
    assert.equal(repeatingBlock(model).content.contentControl.blocks.length, 2);
    assert.equal(repeatingBlock(model).content.contentControl.blocks[1].content.runs[0].text, 'Shipping address: 1 Infinite Loop');
    assert.equal(selection.focus.blockId, repeatingBlock(model).content.contentControl.blocks[1].id);

    const removed = runtime.execCommand('removeRepeatingSectionItem', {
        controlId: 'addresses',
        index: 1,
    });
    assert.equal(removed.result.changed, true);
    assert.equal(repeatingBlock(model).content.contentControl.blocks.length, 1);

    const undoRemove = runtime.execCommand('undo');
    assert.equal(undoRemove.result.changed, true);
    assert.equal(repeatingBlock(model).content.contentControl.blocks.length, 2);

    const undoAdd = runtime.execCommand('undo');
    assert.equal(undoAdd.result.changed, true);
    assert.equal(repeatingBlock(model).content.contentControl.blocks.length, 1);
});

test('content control runtime commands are undoable and redoable', () => {
    let model = createFormsModel();
    let selection = { anchor: { blockId: 'form-p', offset: 0 }, focus: { blockId: 'form-p', offset: 0 } };
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const changed = runtime.execCommand('setContentControlText', {
        controlId: 'customer-name',
        text: 'Grace Hopper',
    });
    assert.equal(changed.handled, true);
    assert.equal(changed.result.changed, true);
    assert.equal(findControl(model, 'customer-name').value.text, 'Grace Hopper');
    assert.equal(runtime.queryCommandState().forms.controlCount, 8);

    const undone = runtime.execCommand('undo');
    assert.equal(undone.result.changed, true);
    assert.equal(findControl(model, 'customer-name').value.text, '');

    const redone = runtime.execCommand('redo');
    assert.equal(redone.result.changed, true);
    assert.equal(findControl(model, 'customer-name').value.text, 'Grace Hopper');
});

test('forms mode keeps body and section block projections synchronized', () => {
    const model = createFormsModel({ duplicateBlocksInSections: true });
    const selection = { anchor: { blockId: 'form-p', offset: 0 }, focus: { blockId: 'form-p', offset: 0 } };

    const result = applyContentControlCommand(model, selection, 'setContentControlText', {
        controlId: 'customer-name',
        text: 'Katherine Johnson',
    });

    assert.equal(result.changed, true);
    assert.equal(result.model.body.blocks[0].content.runs[0].text, 'Katherine Johnson');
    assert.equal(result.model.sections[0].blocks[0].content.runs[0].text, 'Katherine Johnson');
    assert.equal(
        result.model.sections[0].blocks[0].content.runs[0].contentControl.control.value.text,
        'Katherine Johnson');
});

function findControl(model, controlId) {
    return listContentControls(model).find(item => item.control.controlId === controlId).control;
}

function repeatingBlock(model) {
    return findContentControlBlock(model, 'addresses').block;
}

function createFormsModel(options = {}) {
    const block = {
        id: 'form-p',
        type: 'paragraph',
        order: 1,
        paragraphProperties: {},
        content: {
            type: 'paragraph',
            runs: [
                controlRun('name-run', 'customer-name', 'plainText', { text: '' }, { placeholderText: 'Customer name', isRequired: true }),
                controlRun('approved-run', 'approved', 'checkbox', { checked: false }),
                controlRun('plan-run', 'plan', 'dropDown', { selectedValue: 'basic' }, {
                    items: [
                        { displayText: 'Basic', value: 'basic' },
                        { displayText: 'Professional', value: 'pro' },
                        { displayText: 'Enterprise', value: 'enterprise' },
                    ],
                }),
                controlRun('renewal-run', 'renewal', 'date', { dateIso: '2026-06-05' }),
                controlRun('contact-method-run', 'contact-method', 'comboBox', { text: '' }, {
                    placeholderText: 'Contact method',
                    items: [
                        { displayText: 'Email', value: 'email' },
                        { displayText: 'Phone', value: 'phone' },
                    ],
                }),
                controlRun('profile-photo-run', 'profile-photo', 'picture', { assetId: '' }, { placeholderText: 'Profile photo' }),
                controlRun('locked-run', 'locked', 'plainText', { text: 'Read only' }, { lockContent: true }),
            ],
        },
    };

    return {
        documentId: 'phase-e9-forms',
        version: 0,
        body: {
            blocks: [block, repeatingSectionBlock()],
        },
        sections: [{
            id: 'section-1',
            blocks: options.duplicateBlocksInSections === true
                ? [JSON.parse(JSON.stringify(block)), repeatingSectionBlock()]
                : [],
        }],
    };
}

function repeatingSectionBlock() {
    return {
        id: 'addresses-block',
        type: 'contentControl',
        order: 2,
        paragraphProperties: {},
        content: {
            type: 'contentControl',
            contentControl: {
                control: {
                    controlId: 'addresses',
                    kind: 'repeatingSection',
                    scope: 'block',
                    value: {},
                    lockContent: false,
                },
                blocks: [{
                    id: 'address-line-1',
                    type: 'paragraph',
                    order: 3,
                    paragraphProperties: {},
                    content: {
                        type: 'paragraph',
                        runs: [{ id: 'address-line-1-run', type: 'text', text: 'Billing address: Main Street', marks: [] }],
                    },
                }],
            },
        },
    };
}

function controlRun(id, controlId, kind, value, extra = {}) {
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
                value,
                items: extra.items || [],
                placeholderText: extra.placeholderText || null,
                isRequired: extra.isRequired === true,
                lockContent: extra.lockContent === true,
                lockDeletion: false,
            },
            runs: [],
        },
    };
}
