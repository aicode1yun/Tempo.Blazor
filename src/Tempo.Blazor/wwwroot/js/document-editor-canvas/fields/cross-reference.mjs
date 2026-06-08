import { collectReferenceTargets, FIELD_TYPES } from './field-engine.mjs';

export function createCrossReferenceField(target, options = {}) {
    const targetId = String(options.targetId || target?.id || '').trim();
    const referenceKind = String(options.referenceKind || target?.kind || 'heading');
    const referenceFormat = String(options.referenceFormat || options.format || 'text');
    return {
        id: options.id || createId('xref'),
        type: 'field',
        text: '',
        marks: [],
        field: {
            fieldType: FIELD_TYPES.ref,
            instrText: `REF ${targetId}`.trim(),
            targetId,
            referenceKind,
            referenceFormat,
            fallbackText: options.fallbackText || '',
            displayText: options.displayText || null,
            cachedResult: options.cachedResult || null,
        },
    };
}

export function resolveCrossReferenceNavigation(model, fieldOrRun, layout = {}) {
    const field = fieldOrRun?.field || fieldOrRun || {};
    const targets = collectReferenceTargets(model, layout);
    const target = targets.get(String(field.targetId || field.TargetId || ''));
    if (!target?.blockId) {
        return null;
    }

    return {
        target,
        selection: {
            anchor: { blockId: target.blockId, offset: 0 },
            focus: { blockId: target.blockId, offset: 0 },
        },
    };
}

function createId(prefix) {
    return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}
