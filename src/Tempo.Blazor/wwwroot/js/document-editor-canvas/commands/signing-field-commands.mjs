import { createCanvasRunText } from '../layout/canvas-text-style.mjs';
import { createSigningFieldRun, normalizeSigningFieldRun } from '../controls/signing-field-model.mjs';

// Signing field commands (plan S2.9/S2.9b): insert / update / remove a signing field run. Insertion
// places the run at the current caret, which may be in the body OR in a header/footer block — the same
// run lives identically in either, so a header/footer field naturally renders (and yields areas) on
// every page. Update/remove locate the run by its stable signing field uuid anywhere in the document.

const ALIASES = new Map([
    ['insertsigningfield', 'insertSigningField'],
    ['addsigningfield', 'insertSigningField'],
    ['updatesigningfield', 'updateSigningField'],
    ['setsigningfield', 'updateSigningField'],
    ['removesigningfield', 'removeSigningField'],
    ['deletesigningfield', 'removeSigningField'],
]);

export function isSigningFieldCommand(commandId) {
    return ALIASES.has(compact(commandId));
}

export function canonicalSigningFieldCommandId(commandId) {
    return ALIASES.get(compact(commandId)) || '';
}

export function applySigningFieldCommand(model, selection, commandId, payload = {}) {
    const command = canonicalSigningFieldCommandId(commandId);
    const working = clone(model || {});
    ensureCollections(working);
    const data = payload || {};

    if (command === 'insertSigningField') {
        return insertSigningField(working, selection, data);
    }

    if (command === 'updateSigningField') {
        return updateSigningField(working, selection, data);
    }

    if (command === 'removeSigningField') {
        return removeSigningField(working, selection, data);
    }

    return unchanged(working, selection, command);
}

export function querySigningFieldCommandState(model, selection) {
    const target = findFieldRunsByUuid(model, selectionFieldUuid(selection))[0] || null;
    return {
        commands: {
            insertSigningField: { disabled: false, active: false, mixed: false, value: null, state: 'inactive' },
            updateSigningField: { disabled: !target, active: !!target, mixed: false, value: target?.field?.uuid || null, state: target ? 'active' : 'inactive' },
            removeSigningField: { disabled: !target, active: false, mixed: false, value: null, state: target ? 'active' : 'inactive' },
        },
    };
}

function insertSigningField(model, selection, payload) {
    const run = createSigningFieldRun(payload);
    const target = resolveInsertionTarget(model, selection, payload);
    if (!target) {
        return unchanged(model, selection, 'insertSigningField');
    }

    // The canvas model keeps body content in BOTH model.body.blocks AND model.sections[].blocks as
    // separate copies (and the layout renders the section copy). Insert into every block sharing the
    // target id so the field renders AND survives reconcile regardless of which copy is read.
    for (const block of blocksWithId(model, target.blockId)) {
        insertRunAtOffset(block, cloneRun(run), target.offset);
    }

    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: {
            anchor: { blockId: target.blockId, offset: target.offset },
            focus: { blockId: target.blockId, offset: target.offset },
        },
        operation: 'insertSigningField',
        fieldUuid: run.signingField.uuid,
        dirtyBlockIds: [target.blockId],
    };
}

function updateSigningField(model, selection, payload) {
    const uuid = String(payload.uuid ?? payload.Uuid ?? selectionFieldUuid(selection) ?? '');
    const matches = findFieldRunsByUuid(model, uuid);
    if (matches.length === 0) {
        return unchanged(model, selection, 'updateSigningField');
    }

    const dirty = new Set();
    for (const found of matches) {
        const current = found.run.signingField || {};
        const merged = {
            uuid: current.uuid,
            fieldType: payload.fieldType ?? payload.FieldType ?? current.fieldType,
            submitterUuid: payload.submitterUuid ?? payload.SubmitterUuid ?? current.submitterUuid,
            required: payload.required ?? payload.Required ?? current.required,
            label: payload.label ?? payload.Label ?? current.label,
            boxWidth: payload.boxWidth ?? payload.BoxWidth ?? current.boxWidth,
            boxHeight: payload.boxHeight ?? payload.BoxHeight ?? current.boxHeight,
            options: payload.options ?? payload.Options ?? current.options,
        };
        found.run.signingField = normalizeSigningFieldRun({ signingField: merged });
        dirty.add(found.block.id);
    }

    model.version = Number(model.version || 0) + 1;
    return { changed: true, model, selection, operation: 'updateSigningField', fieldUuid: uuid, dirtyBlockIds: [...dirty] };
}

function removeSigningField(model, selection, payload) {
    const uuid = String(payload.uuid ?? payload.Uuid ?? selectionFieldUuid(selection) ?? '');
    const matches = findFieldRunsByUuid(model, uuid);
    if (matches.length === 0) {
        return unchanged(model, selection, 'removeSigningField');
    }

    const dirty = new Set();
    for (const found of matches) {
        const runs = found.block.content.runs;
        const index = runs.indexOf(found.run);
        if (index >= 0) {
            runs.splice(index, 1);
            dirty.add(found.block.id);
        }
    }

    model.version = Number(model.version || 0) + 1;
    return { changed: true, model, selection, operation: 'removeSigningField', fieldUuid: uuid, dirtyBlockIds: [...dirty] };
}

// ── model helpers ───────────────────────────────────────────────────────────────────────────────

function resolveInsertionTarget(model, selection, payload) {
    const requestedId = String(payload.blockId ?? payload.BlockId ?? selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    const block = findEditableBlock(model, requestedId) || firstEditableBlock(model);
    if (!block) {
        return null;
    }

    const blockId = String(block.id || '');
    const runs = runsOrEmpty(block);
    const textLength = runs.reduce((total, run) => total + createCanvasRunText(run).length, 0);
    const requested = payload.offset ?? payload.Offset ?? selection?.focus?.offset ?? selection?.anchor?.offset ?? textLength;
    const offset = Math.max(0, Math.min(textLength, Number(requested) || 0));
    return { blockId, offset };
}

// All blocks sharing an id across body, sections, and header/footer copies (the canvas model keeps
// body content duplicated in body.blocks and sections[].blocks).
function blocksWithId(model, blockId) {
    const id = String(blockId || '');
    return allEditableBlocks(model).filter(block => String(block?.id || '') === id);
}

function findFieldRunsByUuid(model, uuid) {
    const id = String(uuid || '');
    if (!id) {
        return [];
    }

    const matches = [];
    for (const block of allEditableBlocks(model)) {
        for (const run of runsOrEmpty(block)) {
            if (String(run?.type || '') === 'signingField' && String(run?.signingField?.uuid || '') === id) {
                matches.push({ block, run });
            }
        }
    }

    return matches;
}

function cloneRun(run) {
    return JSON.parse(JSON.stringify(run));
}

function insertRunAtOffset(block, run, offset) {
    const runs = runsOrEmpty(block);
    let cursor = 0;
    for (let index = 0; index < runs.length; index += 1) {
        const text = createCanvasRunText(runs[index]);
        const end = cursor + text.length;
        if (offset <= end) {
            if (offset === cursor) {
                runs.splice(index, 0, run);
                return;
            }

            if (offset === end) {
                runs.splice(index + 1, 0, run);
                return;
            }

            const local = offset - cursor;
            const left = { ...runs[index], id: runs[index].id ? `${runs[index].id}-l` : 'text-l', text: text.slice(0, local) };
            const right = { ...runs[index], id: runs[index].id ? `${runs[index].id}-r` : 'text-r', text: text.slice(local) };
            runs.splice(index, 1, left, run, right);
            return;
        }

        cursor = end;
    }

    runs.push(run);
}

function selectionFieldUuid(selection) {
    return selection?.signingField?.uuid ?? selection?.signingFieldUuid ?? null;
}

function allEditableBlocks(model) {
    const sectionBlocks = Array.isArray(model?.sections)
        ? model.sections.flatMap(section => collectBlocks(Array.isArray(section?.blocks) ? section.blocks : []))
        : [];
    return [...allBodyBlocks(model), ...sectionBlocks, ...allHeaderFooterBlocks(model)];
}

function collectBlocks(blocks) {
    const stack = [...blocks].reverse();
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
    }

    return result;
}

function allBodyBlocks(model) {
    const stack = Array.isArray(model?.body?.blocks) ? [...model.body.blocks].reverse() : [];
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
    }

    return result;
}

function allHeaderFooterBlocks(model) {
    return (model?.headersFooters || []).flatMap(item => Array.isArray(item?.blocks) ? item.blocks : []);
}

function findEditableBlock(model, blockId) {
    const id = String(blockId || '');
    return allEditableBlocks(model).find(block => String(block?.id || '') === id) || null;
}

function firstEditableBlock(model) {
    return allEditableBlocks(model).find(block => Array.isArray(block?.content?.runs)) || null;
}

function runsOrEmpty(block) {
    block.content = block.content || { type: 'paragraph', runs: [] };
    block.content.runs = Array.isArray(block.content.runs) ? block.content.runs : [];
    return block.content.runs;
}

function ensureCollections(model) {
    model.body = model.body && typeof model.body === 'object' ? model.body : { blocks: [] };
    model.body.blocks = Array.isArray(model.body.blocks) ? model.body.blocks : [];
    model.headersFooters = Array.isArray(model.headersFooters) ? model.headersFooters : [];
    return model;
}

function unchanged(model, selection, command) {
    return { changed: false, model, selection, operation: command, fieldUuid: null, dirtyBlockIds: [] };
}

function clone(value) {
    return value == null ? value : JSON.parse(JSON.stringify(value));
}

function compact(commandId) {
    return String(commandId || '').replace(/[\s_-]/g, '').toLowerCase();
}
