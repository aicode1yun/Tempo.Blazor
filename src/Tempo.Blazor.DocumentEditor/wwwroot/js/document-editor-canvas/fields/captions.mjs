import { createCanvasRunText, orderedCanvasBlocks } from '../layout/canvas-text-style.mjs';
import { FIELD_TYPES, updateAllFields } from './field-engine.mjs';

export function createCaptionBlock(options = {}) {
    const kind = normalizeCaptionKind(options.kind || options.referenceKind || 'figure');
    const label = captionLabel(kind, options.label);
    const id = String(options.id || createId(`${kind}-caption`));
    const text = String(options.text || defaultCaptionText(kind));
    return {
        id: String(options.blockId || createId('caption-block')),
        sectionId: options.sectionId || null,
        type: 'paragraph',
        order: Number(options.order || 0) || 0,
        paragraphProperties: {
            alignment: 0,
            spacingBefore: 4,
            spacingAfter: 8,
        },
        content: {
            type: 'paragraph',
            caption: {
                id,
                kind,
                label,
                text,
                number: null,
                numberLabel: null,
            },
            runs: [
                {
                    id: `${id}-seq`,
                    type: 'field',
                    text: '',
                    marks: [],
                    field: {
                        fieldType: FIELD_TYPES.seq,
                        instrText: `SEQ ${label}`,
                        sequenceId: kind,
                        sequenceLabel: label,
                        referenceKind: kind,
                        targetId: id,
                        fallbackText: `${label} 1`,
                        displayText: null,
                        cachedResult: null,
                    },
                },
                {
                    id: `${id}-text`,
                    type: 'text',
                    text: ` ${text}`,
                    marks: [],
                },
            ],
        },
        preserve: {},
    };
}

export function renumberCaptions(model) {
    const counters = new Map();
    let changed = false;
    const dirtyBlockIds = [];

    for (const block of orderedCanvasBlocks(model || {})) {
        const caption = block?.content?.caption;
        if (!caption?.id) {
            continue;
        }

        const kind = normalizeCaptionKind(caption.kind || caption.label || 'figure');
        const next = (counters.get(kind) || 0) + 1;
        counters.set(kind, next);
        const label = captionLabel(kind, caption.label);
        const numberLabel = `${label} ${next}`;
        const before = JSON.stringify(caption);
        caption.kind = kind;
        caption.label = label;
        caption.number = next;
        caption.numberLabel = numberLabel;
        if (JSON.stringify(caption) !== before) {
            changed = true;
            dirtyBlockIds.push(String(block.id || ''));
        }

        const seqRun = (block.content.runs || []).find(run => String(run?.type || '') === 'field'
            && Number(run?.field?.fieldType ?? run?.field?.FieldType) === FIELD_TYPES.seq);
        if (seqRun) {
            seqRun.field = {
                ...(seqRun.field || {}),
                fieldType: FIELD_TYPES.seq,
                targetId: caption.id,
                sequenceId: kind,
                sequenceLabel: label,
                referenceKind: kind,
                displayText: numberLabel,
                cachedResult: numberLabel,
                instrText: `SEQ ${label}`,
            };
        }
    }

    const fieldResult = updateAllFields(model);
    for (const id of fieldResult.dirtyBlockIds || []) {
        dirtyBlockIds.push(id);
    }

    return {
        changed: changed || fieldResult.changed,
        model,
        dirtyBlockIds: Array.from(new Set(dirtyBlockIds)),
    };
}

export function collectCaptionEntries(model) {
    return orderedCanvasBlocks(model || {})
        .filter(block => block?.content?.caption?.id)
        .map(block => ({
            id: String(block.content.caption.id),
            kind: normalizeCaptionKind(block.content.caption.kind || block.content.caption.label),
            label: String(block.content.caption.label || ''),
            number: block.content.caption.number,
            numberLabel: String(block.content.caption.numberLabel || ''),
            text: String(block.content.caption.text || '').trim() || blockTextWithoutSeq(block),
            blockId: String(block.id || ''),
        }));
}

export function createTableOfFiguresField(options = {}) {
    const kind = normalizeCaptionKind(options.kind || options.referenceKind || 'figure');
    return {
        id: options.id || createId('tof'),
        type: 'field',
        text: '',
        marks: [],
        field: {
            fieldType: FIELD_TYPES.tableOfFigures,
            instrText: `TOF ${kind}`,
            referenceKind: kind,
            targetId: kind,
            fallbackText: '',
            displayText: null,
            cachedResult: null,
        },
    };
}

function blockTextWithoutSeq(block) {
    return (block?.content?.runs || [])
        .filter(run => String(run?.type || '') !== 'field')
        .map(run => createCanvasRunText(run))
        .join('')
        .trim();
}

function captionLabel(kind, explicit) {
    const text = String(explicit || '').trim();
    if (text) {
        return text;
    }

    if (kind === 'table') {
        return 'Table';
    }

    if (kind === 'equation') {
        return 'Equation';
    }

    return 'Figure';
}

function normalizeCaptionKind(value) {
    const normalized = String(value || 'figure').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'table' || normalized === 'equation') {
        return normalized;
    }

    return 'figure';
}

function defaultCaptionText(kind) {
    if (kind === 'table') {
        return 'Table caption';
    }

    if (kind === 'equation') {
        return 'Equation caption';
    }

    return 'Figure caption';
}

function createId(prefix) {
    return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}
