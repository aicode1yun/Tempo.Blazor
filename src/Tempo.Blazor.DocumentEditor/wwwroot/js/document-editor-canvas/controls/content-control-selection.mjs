import { createCanvasRunText } from '../layout/canvas-text-style.mjs';
import { normalizeContentControl } from './sdt-model.mjs';
import { getBlockIndex } from './block-index.mjs';

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

// Fáze 23 (code review N2): O(1) lookup přes memoizovaný per-model index (block-index.mjs) místo
// plného DFS po každém settled editu. Pokrytí zachováno: body bloky + tabulkové buňky + bloky
// uvnitř block-scope content controlů; header/footer bloky se (historicky) nehledají.
function findBlock(model, blockId) {
    const entry = getBlockIndex(model).get(blockId);
    return entry && entry.headerFooterId === '' ? entry.block : null;
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
