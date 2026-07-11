import { createCanvasRunText } from '../layout/canvas-text-style.mjs';
import { getBlockIndex } from './block-index.mjs';

// Detects the signing field at the current caret (plan S2.11) so the host can open the field's
// properties panel. A signing field run is a length-0 atomic box, so it sits at a single text offset;
// the caret "is on" the field when its offset equals that position. Header/footer fields additionally
// report the header/footer id + scope + a repeats flag (they render — and yield areas — on every page).

const SCOPE_NAMES = ['Primary', 'FirstPage', 'EvenPages', 'OddPages'];

export function findSigningFieldAtSelection(model, selection) {
    const blockId = String(selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    const offset = Number(selection?.focus?.offset ?? selection?.anchor?.offset ?? 0) || 0;
    if (!blockId) {
        return null;
    }

    const location = locateBlock(model, blockId);
    if (!location) {
        return null;
    }

    const runs = Array.isArray(location.block?.content?.runs) ? location.block.content.runs : [];
    let cursor = 0;
    for (const run of runs) {
        const isField = String(run?.type || '') === 'signingField' && run?.signingField;
        if (isField && cursor === offset) {
            return describe(run.signingField, location);
        }

        cursor += createCanvasRunText(run).length;
    }

    return null;
}

function describe(field, location) {
    return {
        uuid: String(field.uuid || ''),
        fieldType: String(field.fieldType || 'text'),
        submitterUuid: String(field.submitterUuid || ''),
        required: field.required === true,
        label: String(field.label || ''),
        headerFooterId: location.headerFooterId,
        scope: location.scope,
        repeats: location.headerFooterId !== '',
    };
}

// Fáze 23 (code review N2): O(1) lookup přes memoizovaný per-model index (block-index.mjs) místo
// plného průchodu po každém settled editu. Pokrytí zachováno: body bloky + tabulkové buňky +
// header/footer bloky; bloky uvnitř block-scope content controlů se (historicky) nehledají.
function locateBlock(model, blockId) {
    const entry = getBlockIndex(model).get(String(blockId || ''));
    if (!entry || entry.nestedInControl === true) {
        return null;
    }

    return {
        block: entry.block,
        headerFooterId: entry.headerFooterId,
        scope: entry.headerFooterId === '' ? '' : scopeName(entry.headerFooterScope),
    };
}

function scopeName(value) {
    if (typeof value === 'number') {
        return SCOPE_NAMES[Math.max(0, Math.min(3, Math.trunc(value)))] || 'Primary';
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'firstpage') return 'FirstPage';
    if (normalized === 'evenpages' || normalized === 'even') return 'EvenPages';
    if (normalized === 'oddpages' || normalized === 'odd') return 'OddPages';
    return 'Primary';
}
