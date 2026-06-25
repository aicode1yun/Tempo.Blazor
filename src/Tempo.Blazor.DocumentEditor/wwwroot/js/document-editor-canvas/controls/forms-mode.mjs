import {
    CONTENT_CONTROL_KINDS,
    contentControlDisplayText,
    normalizeContentControl,
    setContentControlValue,
    validateContentControl,
} from './sdt-model.mjs';
import { createCanvasRunText } from '../layout/canvas-text-style.mjs';

const FORM_COMMAND_ALIASES = new Map([
    ['setcontentcontrolvalue', 'setContentControlValue'],
    ['setcontentcontroltext', 'setContentControlText'],
    ['setcontentcontroldate', 'setContentControlDate'],
    ['setcontentcontroldatevalue', 'setContentControlDate'],
    ['setcontentcontrolpicture', 'setContentControlPicture'],
    ['insertcontentcontrolpicture', 'setContentControlPicture'],
    ['setcontentcontrolcombotext', 'setContentControlComboText'],
    ['togglecontentcontrol', 'toggleContentControl'],
    ['togglecontentcontrolcheckbox', 'toggleContentControl'],
    ['selectcontentcontroloption', 'selectContentControlOption'],
    ['navigatecontentcontrol', 'navigateContentControl'],
    ['nextcontentcontrol', 'navigateContentControl'],
    ['previouscontentcontrol', 'navigateContentControl'],
    ['focuscontentcontrol', 'focusContentControl'],
    ['addrepeatingsectionitem', 'addRepeatingSectionItem'],
    ['removerepeatingsectionitem', 'removeRepeatingSectionItem'],
    ['removecontentcontrolrepeatingitem', 'removeRepeatingSectionItem'],
]);

export function isContentControlCommand(commandId) {
    return FORM_COMMAND_ALIASES.has(compact(commandId));
}

export function canonicalContentControlCommandId(commandId) {
    return FORM_COMMAND_ALIASES.get(compact(commandId)) || '';
}

export function applyContentControlCommand(model, selection, commandId, payload = {}) {
    const command = canonicalContentControlCommandId(commandId);
    const working = clone(model || {});
    ensureModelCollections(working);

    if (command === 'navigateContentControl' || command === 'focusContentControl') {
        return applyContentControlNavigation(working, selection, command, payload, commandId);
    }

    if (command === 'addRepeatingSectionItem' || command === 'removeRepeatingSectionItem') {
        return applyRepeatingSectionCommand(working, selection, command, payload);
    }

    const target = findContentControlRun(working, readControlId(payload));
    if (!target) {
        return unchanged(working, selection, command, 'notFound');
    }

    const control = normalizeContentControl(target.run.contentControl?.control || target.run.contentControl || {});
    let nextValue = payload?.value ?? payload?.Value ?? {};
    if (command === 'setContentControlText') {
        nextValue = { text: String(payload?.text ?? payload?.Text ?? '') };
    } else if (command === 'setContentControlDate') {
        if (control.kind !== CONTENT_CONTROL_KINDS.date) {
            return unchanged(working, selection, command, 'notDate', target);
        }

        nextValue = { dateIso: String(payload?.dateIso ?? payload?.DateIso ?? payload?.date ?? payload?.Date ?? '') };
    } else if (command === 'setContentControlPicture') {
        if (control.kind !== CONTENT_CONTROL_KINDS.picture) {
            return unchanged(working, selection, command, 'notPicture', target);
        }

        nextValue = { assetId: String(payload?.assetId ?? payload?.AssetId ?? payload?.value ?? payload?.Value ?? '') };
    } else if (command === 'setContentControlComboText') {
        if (control.kind !== CONTENT_CONTROL_KINDS.comboBox) {
            return unchanged(working, selection, command, 'notComboBox', target);
        }

        nextValue = {
            text: String(payload?.text ?? payload?.Text ?? ''),
            selectedValue: String(payload?.selectedValue ?? payload?.SelectedValue ?? ''),
        };
    } else if (command === 'toggleContentControl') {
        if (control.kind !== CONTENT_CONTROL_KINDS.checkbox) {
            return unchanged(working, selection, command, 'notCheckbox', target);
        }

        nextValue = { checked: !(control.value?.checked === true) };
    } else if (command === 'selectContentControlOption') {
        nextValue = { selectedValue: String(payload?.selectedValue ?? payload?.SelectedValue ?? payload?.value ?? payload?.Value ?? '') };
    }

    const result = setContentControlValue(control, nextValue);
    if (!result.changed) {
        return unchanged(working, selection, command, result.reason || 'unchanged', target, result.control);
    }

    target.run.contentControl = {
        ...(target.run.contentControl || {}),
        control: result.control,
        runs: Array.isArray(target.run.contentControl?.runs) ? target.run.contentControl.runs : [],
    };
    target.run.text = contentControlDisplayText(result.control);
    syncParallelBlockCopies(working, target.block);
    working.version = Number(working.version || 0) + 1;
    const validation = validateContentControl(result.control);
    return {
        changed: true,
        model: working,
        selection: selection || focusSelectionForTarget(target),
        operation: command,
        controlId: result.control.controlId,
        control: result.control,
        validation,
        dirtyBlockIds: [target.block.id],
    };
}

export function queryContentControlCommandState(model) {
    const controls = listContentControls(model);
    const hasControls = controls.length > 0;
    const hasUnlocked = controls.some(item => item.control.lockContent !== true);
    const hasDate = controls.some(item => item.control.kind === CONTENT_CONTROL_KINDS.date && item.control.lockContent !== true);
    const hasPicture = controls.some(item => item.control.kind === CONTENT_CONTROL_KINDS.picture && item.control.lockContent !== true);
    const hasCombo = controls.some(item => item.control.kind === CONTENT_CONTROL_KINDS.comboBox && item.control.lockContent !== true);
    const hasRepeating = controls.some(item => item.control.kind === CONTENT_CONTROL_KINDS.repeatingSection && item.control.lockContent !== true);
    return {
        forms: {
            controlCount: controls.length,
            requiredInvalidCount: controls.filter(item => item.validation.valid !== true).length,
        },
        commands: {
            setcontentcontrolvalue: commandState(hasUnlocked),
            setcontentcontroltext: commandState(hasUnlocked),
            setcontentcontroldate: commandState(hasDate),
            setcontentcontrolpicture: commandState(hasPicture),
            setcontentcontrolcombotext: commandState(hasCombo),
            togglecontentcontrol: commandState(hasUnlocked),
            selectcontentcontroloption: commandState(hasUnlocked),
            navigatecontentcontrol: commandState(hasControls),
            focuscontentcontrol: commandState(hasControls),
            addrepeatingsectionitem: commandState(hasRepeating),
            removerepeatingsectionitem: commandState(hasRepeating),
        },
    };
}

export function findContentControlRun(model, controlId) {
    const id = String(controlId || '');
    if (!id) {
        return null;
    }

    for (const block of allBlocks(model)) {
        const runs = Array.isArray(block?.content?.runs) ? block.content.runs : [];
        let offset = 0;
        for (const run of runs) {
            const runText = createCanvasRunText(run);
            const start = offset;
            const end = offset + runText.length;
            const contentControl = run?.contentControl?.control || run?.contentControl || null;
            if (!contentControl) {
                offset = end;
                continue;
            }

            const control = normalizeContentControl(contentControl, { fallbackId: run.id });
            if (control.controlId === id) {
                return { block, run, control, start, end };
            }

            offset = end;
        }
    }

    return null;
}

export function findContentControlBlock(model, controlId) {
    const id = String(controlId || '');
    if (!id) {
        return null;
    }

    for (const block of allBlocks(model)) {
        const contentControl = block?.content?.contentControl?.control || null;
        if (!contentControl) {
            continue;
        }

        const control = normalizeContentControl(contentControl, { fallbackId: block.id, scope: 'block' });
        if (control.controlId === id) {
            return { block, control };
        }
    }

    return null;
}

export function listContentControls(model) {
    const controls = [];
    for (const block of allBlocks(model)) {
        for (const run of Array.isArray(block?.content?.runs) ? block.content.runs : []) {
            const contentControl = run?.contentControl?.control || run?.contentControl || null;
            if (contentControl) {
                const control = normalizeContentControl(contentControl, { fallbackId: run.id });
                controls.push({
                    blockId: String(block?.id || ''),
                    runId: String(run?.id || ''),
                    control,
                    validation: validateContentControl(control),
                });
            }
        }

        const blockControl = block?.content?.contentControl?.control || null;
        if (blockControl) {
            const control = normalizeContentControl(blockControl, { fallbackId: block.id, scope: 'block' });
            controls.push({
                blockId: String(block?.id || ''),
                runId: '',
                control,
                validation: validateContentControl(control),
            });
        }
    }

    return controls;
}

function applyContentControlNavigation(model, selection, command, payload, commandId) {
    const targets = listContentControlRunTargets(model);
    if (targets.length === 0) {
        return unchanged(model, selection, command, 'notFound');
    }

    let target = null;
    if (command === 'focusContentControl') {
        const id = readControlId(payload);
        target = targets.find(item => item.control.controlId === id) || null;
    } else {
        const direction = navigationDirection(commandId, payload);
        target = nextNavigationTarget(targets, selection, payload, direction);
    }

    if (!target) {
        return unchanged(model, selection, command, 'notFound');
    }

    return {
        changed: false,
        model,
        selection: focusSelectionForTarget(target),
        selectionChanged: true,
        operation: command,
        controlId: target.control.controlId,
        control: target.control,
        dirtyBlockIds: [],
    };
}

function applyRepeatingSectionCommand(model, selection, command, payload) {
    const target = findContentControlBlock(model, readControlId(payload));
    if (!target) {
        return unchanged(model, selection, command, 'notFound');
    }

    if (target.control.kind !== CONTENT_CONTROL_KINDS.repeatingSection) {
        return unchanged(model, selection, command, 'notRepeatingSection', target, target.control);
    }

    if (target.control.lockContent === true) {
        return unchanged(model, selection, command, 'locked', target, target.control);
    }

    const payloadBlock = target.block.content.contentControl || {};
    payloadBlock.blocks = Array.isArray(payloadBlock.blocks) ? payloadBlock.blocks : [];

    if (command === 'addRepeatingSectionItem') {
        const addedBlocks = createRepeatingSectionBlocks(payload, target, payloadBlock.blocks.length);
        if (addedBlocks.length === 0) {
            return unchanged(model, selection, command, 'emptyRepeatingSection', target, target.control);
        }

        payloadBlock.blocks.push(...addedBlocks);
        syncParallelBlockCopies(model, target.block);
        model.version = Number(model.version || 0) + 1;
        const firstEditable = firstEditableBlock(addedBlocks);
        return {
            changed: true,
            model,
            selection: firstEditable ? collapsedSelection(firstEditable.id, textLength(firstEditable)) : selection || null,
            operation: command,
            controlId: target.control.controlId,
            control: normalizeContentControl(payloadBlock.control || target.control, { scope: 'block', fallbackId: target.block.id }),
            repeatingSection: {
                itemCount: payloadBlock.blocks.length,
                addedBlockIds: addedBlocks.map(block => String(block?.id || '')).filter(Boolean),
            },
            dirtyBlockIds: [target.block.id, ...addedBlocks.map(block => String(block?.id || '')).filter(Boolean)],
        };
    }

    const removal = resolveRepeatingSectionRemoval(payloadBlock.blocks, payload);
    if (!removal) {
        return unchanged(model, selection, command, 'notFound', target, target.control);
    }

    const [removed] = payloadBlock.blocks.splice(removal.index, 1);
    syncParallelBlockCopies(model, target.block);
    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: selection || null,
        operation: command,
        controlId: target.control.controlId,
        control: normalizeContentControl(payloadBlock.control || target.control, { scope: 'block', fallbackId: target.block.id }),
        repeatingSection: {
            itemCount: payloadBlock.blocks.length,
            removedBlockId: String(removed?.id || ''),
        },
        dirtyBlockIds: [target.block.id, String(removed?.id || '')].filter(Boolean),
    };
}

function createRepeatingSectionBlocks(payload, target, currentCount) {
    const blocks = Array.isArray(payload?.blocks ?? payload?.Blocks)
        ? (payload.blocks ?? payload.Blocks).map(block => cloneBlockWithFreshIds(block, 'sdt-item'))
        : [];
    if (blocks.length > 0) {
        return blocks;
    }

    const text = String(payload?.text ?? payload?.Text ?? '').trim();
    if (text) {
        const index = currentCount + 1;
        const id = createId(`${target.control.controlId || target.block.id}-item-${index}`);
        return [{
            id,
            type: 'paragraph',
            order: Number(target.block?.order || 0) + index / 100,
            paragraphProperties: {},
            content: {
                type: 'paragraph',
                runs: [{
                    id: `${id}-run`,
                    type: 'text',
                    text,
                    marks: [],
                }],
            },
        }];
    }

    const existing = target.block?.content?.contentControl?.blocks || [];
    const template = existing.at(-1);
    return template ? [cloneBlockWithFreshIds(template, `${target.control.controlId || target.block.id}-copy`)] : [];
}

function resolveRepeatingSectionRemoval(blocks, payload) {
    const blockId = String(payload?.blockId ?? payload?.BlockId ?? '');
    if (blockId) {
        const index = blocks.findIndex(block => String(block?.id || '') === blockId);
        return index >= 0 ? { index } : null;
    }

    const requestedIndex = Number(payload?.index ?? payload?.Index);
    if (Number.isFinite(requestedIndex)) {
        const index = Math.trunc(requestedIndex);
        return index >= 0 && index < blocks.length ? { index } : null;
    }

    return blocks.length > 0 ? { index: blocks.length - 1 } : null;
}

function listContentControlRunTargets(model) {
    const result = [];
    for (const block of allBlocks(model)) {
        const runs = Array.isArray(block?.content?.runs) ? block.content.runs : [];
        let offset = 0;
        for (const run of runs) {
            const runText = createCanvasRunText(run);
            const start = offset;
            const end = offset + runText.length;
            const contentControl = run?.contentControl?.control || run?.contentControl || null;
            if (contentControl) {
                result.push({
                    block,
                    run,
                    control: normalizeContentControl(contentControl, { fallbackId: run.id }),
                    start,
                    end,
                });
            }

            offset = end;
        }
    }

    return result;
}

function nextNavigationTarget(targets, selection, payload, direction) {
    const currentId = readControlId(payload);
    const currentIndex = currentId
        ? targets.findIndex(item => item.control.controlId === currentId)
        : indexFromSelection(targets, selection);

    if (currentIndex >= 0) {
        return targets[(currentIndex + direction + targets.length) % targets.length];
    }

    if (direction < 0) {
        return targets.at(-1) || null;
    }

    const blockId = String(selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    const offset = Number(selection?.focus?.offset ?? selection?.anchor?.offset ?? 0) || 0;
    return targets.find(item => String(item.block?.id || '') === blockId && item.start >= offset) || targets[0] || null;
}

function indexFromSelection(targets, selection) {
    const blockId = String(selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    const offset = Number(selection?.focus?.offset ?? selection?.anchor?.offset ?? 0) || 0;
    return targets.findIndex(item =>
        String(item.block?.id || '') === blockId
        && offset >= item.start
        && (offset < item.end || item.start === item.end));
}

function navigationDirection(commandId, payload) {
    const requested = String(payload?.direction ?? payload?.Direction ?? '').replace(/[\s_-]/g, '').toLowerCase();
    const command = compact(commandId);
    if (requested === 'previous' || requested === 'prev' || requested === 'backward' || command === 'previouscontentcontrol') {
        return -1;
    }

    return 1;
}

function focusSelectionForTarget(target) {
    return collapsedSelection(target.block?.id, target.start);
}

function collapsedSelection(blockId, offset) {
    const position = { blockId: String(blockId || ''), offset: Math.max(0, Number(offset || 0) || 0) };
    return { anchor: position, focus: { ...position } };
}

function firstEditableBlock(blocks) {
    const stack = Array.isArray(blocks) ? [...blocks].reverse() : [];
    while (stack.length > 0) {
        const block = stack.pop();
        if (Array.isArray(block?.content?.runs)) {
            return block;
        }

        const nested = block?.content?.contentControl?.blocks;
        if (Array.isArray(nested)) {
            for (const child of [...nested].reverse()) {
                stack.push(child);
            }
        }
    }

    return null;
}

function textLength(block) {
    return (Array.isArray(block?.content?.runs) ? block.content.runs : [])
        .reduce((total, run) => total + createCanvasRunText(run).length, 0);
}

function cloneBlockWithFreshIds(block, prefix) {
    const copy = clone(block);
    refreshBlockIds(copy, prefix || 'sdt-block');
    return copy;
}

function refreshBlockIds(block, prefix) {
    if (!block || typeof block !== 'object') {
        return;
    }

    block.id = createId(prefix);
    if (Array.isArray(block?.content?.runs)) {
        for (const run of block.content.runs) {
            run.id = createId(`${block.id}-run`);
            if (run?.contentControl?.control?.controlId) {
                run.contentControl.control.controlId = createId(`${run.contentControl.control.controlId}-copy`);
            }
        }
    }

    for (const nested of block?.content?.contentControl?.blocks || []) {
        refreshBlockIds(nested, `${block.id}-item`);
    }
}

function unchanged(model, selection, operation, reason, target = null, control = null) {
    return {
        changed: false,
        model,
        selection: selection || null,
        selectionChanged: false,
        operation,
        reason,
        controlId: control?.controlId || target?.control?.controlId || null,
        control: control || target?.control || null,
        dirtyBlockIds: target?.block?.id ? [target.block.id] : [],
    };
}

function allBlocks(model) {
    const stack = topLevelBlocks(model).slice().reverse();
    const result = [];
    while (stack.length > 0) {
        const block = stack.pop();
        if (!block) {
            continue;
        }

        result.push(block);
        const rows = block?.content?.table?.rows;
        if (Array.isArray(rows)) {
            for (let rowIndex = rows.length - 1; rowIndex >= 0; rowIndex -= 1) {
                for (const cell of [...(rows[rowIndex]?.cells || [])].reverse()) {
                    for (const nested of [...(cell?.blocks || [])].reverse()) {
                        stack.push(nested);
                    }
                }
            }
        }

        const nestedControls = block?.content?.contentControl?.blocks;
        if (Array.isArray(nestedControls)) {
            for (const nested of [...nestedControls].reverse()) {
                stack.push(nested);
            }
        }
    }

    return result;
}

function topLevelBlocks(model) {
    if (Array.isArray(model?.body?.blocks) && model.body.blocks.length > 0) {
        return model.body.blocks;
    }

    if (Array.isArray(model?.sections)) {
        return model.sections.flatMap(section => Array.isArray(section?.blocks) ? section.blocks : []);
    }

    return [];
}

function ensureModelCollections(model) {
    if (Array.isArray(model?.body?.blocks) || Array.isArray(model?.sections)) {
        return;
    }

    model.body = { blocks: [] };
}

function syncParallelBlockCopies(model, updatedBlock) {
    const blockId = String(updatedBlock?.id || '');
    if (!blockId) {
        return;
    }

    const replacement = clone(updatedBlock);
    if (Array.isArray(model?.body?.blocks)) {
        replaceBlockInCollection(model.body.blocks, blockId, replacement);
    }

    if (Array.isArray(model?.sections)) {
        for (const section of model.sections) {
            if (Array.isArray(section?.blocks)) {
                replaceBlockInCollection(section.blocks, blockId, replacement);
            }
        }
    }
}

function replaceBlockInCollection(blocks, blockId, replacement) {
    for (let index = 0; index < blocks.length; index += 1) {
        const block = blocks[index];
        if (!block) {
            continue;
        }

        if (String(block.id || '') === blockId && block !== replacement) {
            blocks[index] = clone(replacement);
            continue;
        }

        replaceNestedBlockCopies(block, blockId, replacement);
    }
}

function replaceNestedBlockCopies(block, blockId, replacement) {
    const rows = block?.content?.table?.rows;
    if (Array.isArray(rows)) {
        for (const row of rows) {
            for (const cell of Array.isArray(row?.cells) ? row.cells : []) {
                if (Array.isArray(cell?.blocks)) {
                    replaceBlockInCollection(cell.blocks, blockId, replacement);
                }
            }
        }
    }

    const nestedControls = block?.content?.contentControl?.blocks;
    if (Array.isArray(nestedControls)) {
        replaceBlockInCollection(nestedControls, blockId, replacement);
    }
}

function readControlId(payload) {
    return String(payload?.controlId ?? payload?.ControlId ?? payload?.id ?? payload?.Id ?? '');
}

function createId(prefix) {
    const random = Math.random().toString(36).slice(2, 10);
    const time = Date.now().toString(36);
    return `${String(prefix || 'sdt').replace(/[^a-zA-Z0-9_-]/g, '-')}-${time}-${random}`;
}

function commandState(enabled) {
    return {
        disabled: enabled !== true,
        active: false,
        mixed: false,
        value: null,
        state: enabled === true ? 'available' : 'disabled',
    };
}

function compact(value) {
    return String(value ?? '').replace(/[\s_-]/g, '').toLowerCase();
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
