import { createCanvasRunText, orderedCanvasBlocks } from '../layout/canvas-text-style.mjs';
import { formatGeneratedIndexText } from '../navigation/toc-generator.mjs';

export const FIELD_TYPES = Object.freeze({
    pageNumber: 0,
    pageCount: 1,
    pageXOfY: 2,
    date: 3,
    documentTitle: 4,
    author: 5,
    lastSaved: 6,
    sectionPageNumber: 7,
    sectionPageCount: 8,
    fileName: 9,
    revisionNumber: 10,
    time: 11,
    styleRef: 12,
    ref: 13,
    seq: 14,
    tableOfFigures: 15,
    bibliography: 16,
    citation: 17,
});

export function updateAllFields(model, options = {}) {
    const working = model && typeof model === 'object' ? model : {};
    const layout = options.layout || {};
    const context = createFieldContext(working, layout, options);
    const dirtyBlockIds = new Set();
    let changed = false;

    for (const block of orderedCanvasBlocks(working)) {
        let blockChanged = false;
        for (const run of runsOrEmpty(block)) {
            if (String(run?.type || '') !== 'field') {
                continue;
            }

            const result = resolveFieldRun(run, block, context);
            if (result.changed) {
                blockChanged = true;
                changed = true;
            }
        }

        if (blockChanged && block?.id) {
            dirtyBlockIds.add(String(block.id));
        }
    }

    if (changed) {
        working.version = Number(working.version || 0) + 1;
        working.fieldRevision = Number(working.fieldRevision || 0) + 1;
    }

    return {
        changed,
        model: working,
        dirtyBlockIds: Array.from(dirtyBlockIds),
        fieldRevision: Number(working.fieldRevision || 0) || 0,
    };
}

export function resolveFieldRun(run, block, context) {
    run.field = normalizeField(run.field);
    const before = JSON.stringify({
        displayText: run.field.displayText ?? null,
        cachedResult: run.field.cachedResult ?? null,
        instrText: run.field.instrText ?? null,
    });
    const displayText = resolveFieldText(run.field, block, context);
    run.field.displayText = displayText;
    run.field.cachedResult = displayText;
    run.field.instrText = run.field.instrText || buildInstructionText(run.field);
    return {
        changed: before !== JSON.stringify({
            displayText: run.field.displayText ?? null,
            cachedResult: run.field.cachedResult ?? null,
            instrText: run.field.instrText ?? null,
        }),
        displayText,
    };
}

export function createFieldContext(model, layout = {}, options = {}) {
    const pages = Array.isArray(layout?.pages) ? layout.pages : [];
    const blockPages = new Map();
    for (const block of Array.isArray(layout?.blocks) ? layout.blocks : []) {
        const blockId = block?.blockId || block?.id;
        if (blockId) {
            blockPages.set(String(blockId), Math.max(1, Number(block.pageIndex || 0) + 1));
        }
    }

    const targets = collectReferenceTargets(model, layout);
    const captions = Array.from(targets.values()).filter(target => target.kind === 'caption');
    return {
        model,
        layout,
        now: options.now instanceof Date ? options.now : options.now ? new Date(options.now) : new Date(),
        pageCount: Math.max(1, Number(options.pageCount || pages.length || model?.pageCount || 1) || 1),
        fileName: String(options.fileName || model?.metadata?.fileName || model?.metadata?.FileName || model?.documentId || ''),
        blockPages,
        targets,
        captions,
        sequenceCounters: new Map(),
    };
}

export function collectReferenceTargets(model, layout = {}) {
    const targets = new Map();
    const blockPages = new Map();
    for (const block of Array.isArray(layout?.blocks) ? layout.blocks : []) {
        const blockId = block?.blockId || block?.id;
        if (blockId) {
            blockPages.set(String(blockId), Math.max(1, Number(block.pageIndex || 0) + 1));
        }
    }

    const listCounters = new Map();
    for (const block of orderedCanvasBlocks(model || {})) {
        const text = blockText(block);
        const page = blockPages.get(String(block?.id || '')) || 1;
        const blockType = String(block?.type || block?.content?.type || '').toLowerCase();
        if (blockType === 'heading' || block?.content?.headingLevel) {
            targets.set(String(block.id), {
                id: String(block.id),
                kind: 'heading',
                text,
                number: block?.content?.headingLevel ? String(block.content.headingLevel) : '',
                page,
                blockId: String(block.id),
            });
        }

        const caption = block?.content?.caption;
        if (caption?.id) {
            const label = String(caption.label || caption.kind || 'Figure');
            const number = String(caption.number || '');
            const numberLabel = String(caption.numberLabel || `${label}${number ? ` ${number}` : ''}`.trim());
            targets.set(String(caption.id), {
                id: String(caption.id),
                kind: 'caption',
                captionKind: String(caption.kind || '').toLowerCase(),
                text: `${numberLabel}${caption.text ? ` ${caption.text}` : ''}`.trim(),
                number,
                numberLabel,
                page,
                blockId: String(block.id),
            });
        }

        const list = block?.content?.list;
        if (list?.ordered) {
            const level = Math.max(0, Number(list.indentLevel || 0) || 0);
            const key = `${list.numberingId || 'default'}:${level}`;
            const previous = listCounters.get(key);
            const start = Math.max(1, Number(list.startNumber || 1) || 1);
            const next = list.numberingValue || (list.restartNumbering ? start : previous == null ? start : previous + 1);
            listCounters.set(key, next);
            targets.set(String(block.id), {
                id: String(block.id),
                kind: 'numberedItem',
                text,
                number: String(next),
                numberLabel: `${next}.`,
                page,
                blockId: String(block.id),
            });
        }

        for (const run of runsOrEmpty(block)) {
            for (const mark of Array.isArray(run?.marks) ? run.marks : []) {
                const type = compact(mark?.type);
                const bookmarkId = mark?.value || mark?.bookmarkId || mark?.BookmarkId;
                if (type === 'bookmark' && bookmarkId) {
                    targets.set(String(bookmarkId), {
                        id: String(bookmarkId),
                        kind: 'bookmark',
                        text: createCanvasRunText(run) || text,
                        number: '',
                        page,
                        blockId: String(block.id),
                    });
                }
            }
        }
    }

    return targets;
}

export function buildInstructionText(field) {
    const type = normalizeFieldType(field?.fieldType ?? field?.FieldType);
    if (type === FIELD_TYPES.ref) {
        return `REF ${field.targetId || ''}`.trim();
    }

    if (type === FIELD_TYPES.seq) {
        return `SEQ ${field.sequenceLabel || field.sequenceId || ''}`.trim();
    }

    if (type === FIELD_TYPES.tableOfFigures) {
        return `TOF ${field.referenceKind || field.targetId || 'figure'}`.trim();
    }

    if (type === FIELD_TYPES.bibliography) {
        return 'BIBLIOGRAPHY';
    }

    if (type === FIELD_TYPES.citation) {
        return `CITATION ${field.citationId || field.targetId || ''}`.trim();
    }

    if (type === FIELD_TYPES.styleRef) {
        return `STYLEREF ${field.targetId || field.referenceKind || 'Heading 1'}`.trim();
    }

    return fieldTypeName(type);
}

export function normalizeFieldType(value) {
    if (typeof value === 'number') {
        return Math.max(0, Math.trunc(value));
    }

    const normalized = compact(value);
    return {
        pagenumber: FIELD_TYPES.pageNumber,
        page: FIELD_TYPES.pageNumber,
        pagecount: FIELD_TYPES.pageCount,
        numpages: FIELD_TYPES.pageCount,
        totalpages: FIELD_TYPES.pageCount,
        pagexofy: FIELD_TYPES.pageXOfY,
        date: FIELD_TYPES.date,
        documenttitle: FIELD_TYPES.documentTitle,
        title: FIELD_TYPES.documentTitle,
        author: FIELD_TYPES.author,
        lastsaved: FIELD_TYPES.lastSaved,
        sectionpagenumber: FIELD_TYPES.sectionPageNumber,
        sectionpage: FIELD_TYPES.sectionPageNumber,
        sectionpagecount: FIELD_TYPES.sectionPageCount,
        filename: FIELD_TYPES.fileName,
        revisionnumber: FIELD_TYPES.revisionNumber,
        revision: FIELD_TYPES.revisionNumber,
        time: FIELD_TYPES.time,
        styleref: FIELD_TYPES.styleRef,
        ref: FIELD_TYPES.ref,
        seq: FIELD_TYPES.seq,
        tableoffigures: FIELD_TYPES.tableOfFigures,
        tof: FIELD_TYPES.tableOfFigures,
        bibliography: FIELD_TYPES.bibliography,
        citation: FIELD_TYPES.citation,
    }[normalized] ?? FIELD_TYPES.pageNumber;
}

function resolveFieldText(field, block, context) {
    const type = normalizeFieldType(field.fieldType);
    if (type === FIELD_TYPES.pageNumber || type === FIELD_TYPES.sectionPageNumber) {
        return String(context.blockPages.get(String(block?.id || '')) || 1);
    }

    if (type === FIELD_TYPES.pageCount || type === FIELD_TYPES.sectionPageCount) {
        return String(context.pageCount);
    }

    if (type === FIELD_TYPES.pageXOfY) {
        return `${context.blockPages.get(String(block?.id || '')) || 1} / ${context.pageCount}`;
    }

    if (type === FIELD_TYPES.date || type === FIELD_TYPES.lastSaved) {
        return formatDate(context.now, field.format);
    }

    if (type === FIELD_TYPES.time) {
        return formatTime(context.now, field.format);
    }

    if (type === FIELD_TYPES.documentTitle) {
        return String(context.model?.metadata?.title || context.model?.metadata?.Title || field.fallbackText || '');
    }

    if (type === FIELD_TYPES.author) {
        return String(context.model?.metadata?.author?.displayName || context.model?.metadata?.Author?.DisplayName || field.fallbackText || '');
    }

    if (type === FIELD_TYPES.fileName) {
        return context.fileName || String(field.fallbackText || '');
    }

    if (type === FIELD_TYPES.revisionNumber) {
        return String(context.model?.version ?? context.model?.Version ?? field.fallbackText ?? '0');
    }

    if (type === FIELD_TYPES.styleRef) {
        return resolveStyleRef(block, context, field);
    }

    if (type === FIELD_TYPES.ref) {
        return resolveReference(field, context);
    }

    if (type === FIELD_TYPES.seq) {
        return resolveSequence(field, context);
    }

    if (type === FIELD_TYPES.tableOfFigures) {
        return resolveTableOfFigures(field, context);
    }

    if (type === FIELD_TYPES.bibliography) {
        return resolveBibliography(context);
    }

    if (type === FIELD_TYPES.citation) {
        return resolveCitation(field, context);
    }

    return String(field.displayText || field.cachedResult || field.fallbackText || '');
}

function resolveReference(field, context) {
    const target = context.targets.get(String(field.targetId || ''));
    if (!target) {
        return String(field.fallbackText || '');
    }

    const format = compact(field.referenceFormat || field.format || 'text');
    if (format === 'page' || format === 'pagenumber') {
        return String(target.page || 1);
    }

    if (format === 'number' || format === 'labelnumber') {
        return String(target.numberLabel || target.number || target.text || '');
    }

    if (format === 'full' || format === 'fullcaption') {
        return String(target.text || target.numberLabel || '');
    }

    return String(target.text || target.numberLabel || target.number || '');
}

function resolveSequence(field, context) {
    const id = compact(field.sequenceId || field.sequenceLabel || field.targetId || 'figure') || 'figure';
    const next = (context.sequenceCounters.get(id) || 0) + 1;
    context.sequenceCounters.set(id, next);
    const label = String(field.sequenceLabel || field.referenceKind || '').trim();
    return label ? `${label} ${next}` : String(next);
}

function resolveTableOfFigures(field, context) {
    const kind = compact(field.referenceKind || field.targetId || field.sequenceId || 'figure');
    const entries = context.captions
        .filter(caption => !kind || compact(caption.captionKind || caption.numberLabel || '') === kind)
        .map(caption => ({
            text: caption.text,
            pageNumber: caption.page || 1,
            blockId: caption.blockId,
            level: 1,
        }));
    return formatGeneratedIndexText(entries, { separator: '\t' });
}

function resolveBibliography(context) {
    return (context.model?.bibliographySources || [])
        .map(formatBibliographySource)
        .filter(Boolean)
        .join('\n');
}

function resolveCitation(field, context) {
    const sourceId = String(field.citationId || field.targetId || '');
    const source = (context.model?.bibliographySources || []).find(item => String(item?.id || '') === sourceId);
    if (!source) {
        return String(field.fallbackText || '');
    }

    const author = String(source.author || source.Author || '').split(/\s+/).filter(Boolean).at(-1) || String(source.author || source.Author || '');
    const year = source.year ?? source.Year;
    return year ? `(${author}, ${year})` : `(${author})`;
}

function resolveStyleRef(block, context, field) {
    const desired = compact(field.targetId || field.referenceKind || 'heading1');
    const blocks = orderedCanvasBlocks(context.model || {});
    const currentIndex = Math.max(0, blocks.findIndex(candidate => String(candidate?.id || '') === String(block?.id || '')));
    for (let index = currentIndex; index >= 0; index -= 1) {
        const candidate = blocks[index];
        const styleKey = compact(candidate?.content?.styleId || candidate?.content?.styleName || candidate?.type);
        const headingKey = candidate?.content?.headingLevel ? `heading${candidate.content.headingLevel}` : '';
        if (styleKey === desired || headingKey === desired || (desired.startsWith('heading') && headingKey === desired)) {
            return blockText(candidate);
        }
    }

    return String(field.fallbackText || '');
}

function normalizeField(field) {
    const source = field && typeof field === 'object' ? field : {};
    return {
        ...source,
        fieldType: normalizeFieldType(source.fieldType ?? source.FieldType),
        format: source.format ?? source.Format ?? null,
        fallbackText: source.fallbackText ?? source.FallbackText ?? '',
        displayText: source.displayText ?? source.DisplayText ?? null,
        instrText: source.instrText ?? source.InstrText ?? null,
        cachedResult: source.cachedResult ?? source.CachedResult ?? null,
        targetId: source.targetId ?? source.TargetId ?? null,
        referenceKind: source.referenceKind ?? source.ReferenceKind ?? null,
        referenceFormat: source.referenceFormat ?? source.ReferenceFormat ?? null,
        sequenceId: source.sequenceId ?? source.SequenceId ?? null,
        sequenceLabel: source.sequenceLabel ?? source.SequenceLabel ?? null,
        citationId: source.citationId ?? source.CitationId ?? null,
        metadata: source.metadata ?? source.Metadata ?? {},
    };
}

function formatBibliographySource(source) {
    const author = String(source?.author || source?.Author || '').trim();
    const title = String(source?.title || source?.Title || '').trim();
    const year = source?.year ?? source?.Year;
    const container = String(source?.container || source?.Container || '').trim();
    return [author, year ? `(${year}).` : '', title ? `${title}.` : '', container].filter(Boolean).join(' ');
}

function formatDate(date, format) {
    const normalized = compact(format || '');
    if (normalized === 'iso' || normalized === 'yyyyMMdd') {
        return date.toISOString().slice(0, 10);
    }

    return date.toLocaleDateString('en-US');
}

function formatTime(date) {
    return date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
}

function fieldTypeName(type) {
    return Object.entries(FIELD_TYPES).find(([, value]) => value === type)?.[0]?.toUpperCase() || 'PAGE';
}

function blockText(block) {
    return runsOrEmpty(block).map(run => createCanvasRunText(run)).join('').trim();
}

function runsOrEmpty(block) {
    return Array.isArray(block?.content?.runs) ? block.content.runs : [];
}

function compact(value) {
    return String(value == null ? '' : value).replace(/[\s_-]/g, '').toLowerCase();
}
