import { createCanvasRunText } from '../layout/canvas-text-style.mjs';
import { normalizeContentControl } from './sdt-model.mjs';

// Phase N2: detects the popover-eligible content control at the caret so the host can drive the
// content-control popover from the (O(focused block)) selection state instead of marshalling the
// full document into C# after every settled edit. Mirrors the semantics of the retired C#
// FindCanvasContentControlAtSelection: the caret is "on" the control when the focus offset lies
// inside the control's display-text span — or exactly on it for a zero-length control — and only
// date / comboBox / dropDown / picture kinds open the popover.

const POPOVER_KINDS = new Set(['date', 'comboBox', 'dropDown', 'picture']);

export function findContentControlAtSelection(model, selection) {
    const blockId = String(selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    const offset = Number(selection?.focus?.offset ?? selection?.anchor?.offset ?? 0) || 0;
    if (!blockId) {
        return null;
    }

    const block = findBlock(model, blockId);
    if (!block) {
        return null;
    }

    const runs = Array.isArray(block?.content?.runs) ? block.content.runs : [];
    let cursor = 0;
    for (const run of runs) {
        const length = createCanvasRunText(run).length;
        const start = cursor;
        const end = cursor + length;
        if (String(run?.type || '') === 'contentControl'
            && offset >= start
            && (offset < end || start === end)) {
            const control = normalizeContentControl(run?.contentControl?.control || run?.contentControl || {}, {
                fallbackId: run?.id,
            });
            if (POPOVER_KINDS.has(control.kind)) {
                return describe(control);
            }
        }

        cursor = end;
    }

    return null;
}

// Payload mirrors C# CanvasContentControlPopoverState (camelCase over the interop boundary).
function describe(control) {
    return {
        controlId: control.controlId,
        kind: control.kind,
        title: firstNonEmpty(control.alias, control.tag, control.controlId),
        isRequired: control.isRequired === true,
        lockContent: control.lockContent === true,
        text: String(control.value?.text || ''),
        selectedValue: String(control.value?.selectedValue || ''),
        dateIso: String(control.value?.dateIso || control.value?.text || ''),
        assetId: String(control.value?.assetId || ''),
        items: (Array.isArray(control.items) ? control.items : []).map(item => ({
            value: String(item?.value || ''),
            displayText: String(item?.displayText || ''),
        })),
    };
}

// Depth-first walk over body blocks, descending into table cells and block-scope content controls
// (same coverage as the C# EnumerateBlocks this replaces).
function findBlock(model, blockId) {
    const stack = Array.isArray(model?.body?.blocks) ? [...model.body.blocks].reverse() : [];
    while (stack.length > 0) {
        const block = stack.pop();
        if (!block) {
            continue;
        }

        if (String(block?.id || '') === blockId) {
            return block;
        }

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

        const nestedControlBlocks = block?.content?.contentControl?.blocks;
        if (Array.isArray(nestedControlBlocks)) {
            for (let index = nestedControlBlocks.length - 1; index >= 0; index -= 1) {
                stack.push(nestedControlBlocks[index]);
            }
        }
    }

    return null;
}

function firstNonEmpty(...values) {
    for (const value of values) {
        const text = value == null ? '' : String(value).trim();
        if (text) {
            return String(value);
        }
    }

    return '';
}
