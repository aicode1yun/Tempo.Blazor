import { createCanvasRunText } from '../layout/canvas-text-style.mjs';

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

function locateBlock(model, blockId) {
    const id = String(blockId || '');
    for (const block of bodyBlocks(model)) {
        if (String(block?.id || '') === id) {
            return { block, headerFooterId: '', scope: '' };
        }
    }

    for (const headerFooter of Array.isArray(model?.headersFooters) ? model.headersFooters : []) {
        for (const block of Array.isArray(headerFooter?.blocks) ? headerFooter.blocks : []) {
            if (String(block?.id || '') === id) {
                return {
                    block,
                    headerFooterId: String(headerFooter.id || ''),
                    scope: scopeName(headerFooter.scope ?? headerFooter.Scope),
                };
            }
        }
    }

    return null;
}

function bodyBlocks(model) {
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
