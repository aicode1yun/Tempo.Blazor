import { createFontMetricsService } from '../../document-editor/layout/font-metrics.mjs';
import { createCanvasRunStyle, normalizeCanvasAlignment } from './canvas-text-style.mjs';
import { CANVAS_RENDER_LAYERS } from '../render/layers.mjs';
import { normalizeSigningFieldRun } from '../controls/signing-field-model.mjs';

const HEADER = 0;
const FOOTER = 1;
const PRIMARY = 0;
const FIRST_PAGE = 1;
const EVEN_PAGES = 2;
const ODD_PAGES = 3;

export function buildHeaderFooterLayout(model, textLayout, options = {}) {
    const metrics = ensureMetrics(options.fontMetrics);
    const pages = Array.isArray(textLayout?.pages) ? textLayout.pages : [];
    const totalPages = Math.max(1, pages.length);
    const commands = [];
    const regions = [];
    let sequence = 0;

    for (const page of pages) {
        const section = resolveSection(model, page.sectionId);
        for (const type of [HEADER, FOOTER]) {
            const headerFooter = resolveHeaderFooter(model, section, type, page.index);
            if (!headerFooter || !Array.isArray(headerFooter.blocks) || headerFooter.blocks.length === 0) {
                continue;
            }

            const region = createRegion(page, headerFooter, type);
            regions.push(region);
            commands.push({
                id: `${region.id}-frame`,
                type: 'headerFooterFrame',
                layer: CANVAS_RENDER_LAYERS.annotations,
                pageIndex: region.pageIndex,
                headerFooterId: region.headerFooterId,
                region: region.region,
                scope: region.scope,
                x: region.x,
                y: region.y,
                width: region.width,
                height: region.height,
                stroke: 'rgba(99, 102, 241, 0.32)',
                fill: 'rgba(248, 250, 252, 0.42)',
                lineWidth: 1,
                sequence: sequence++,
            });

            const textCommands = layoutHeaderFooterBlocks({
                model,
                headerFooter,
                page,
                region,
                totalPages,
                metrics,
                sequenceStart: sequence,
            });
            commands.push(...textCommands);
            sequence += textCommands.length;
        }
    }

    return { regions, commands };
}

export function resolveHeaderFooter(model, section, type, pageIndex) {
    const headersFooters = Array.isArray(model?.headersFooters) ? model.headersFooters : [];
    if (headersFooters.length === 0) {
        return null;
    }

    const scope = resolveScope(section, pageIndex);
    return findByReference(model, section, type, scope)
        || findByReference(model, section, type, PRIMARY)
        || findByShape(headersFooters, section, type, scope)
        || findByShape(headersFooters, section, type, PRIMARY)
        || null;
}

export function resolveFieldText(run, context) {
    const field = run?.field || run?.Field || {};
    const fieldType = normalizeFieldType(field.fieldType ?? field.FieldType);
    const pageIndex = Number(context.pageIndex ?? context.page?.index ?? 0) || 0;
    if (fieldType === 0) {
        return String(pageIndex + 1);
    }

    if (fieldType === 1) {
        return String(Math.max(1, Number(context.totalPages || 1) || 1));
    }

    if (fieldType === 2) {
        return `${pageIndex + 1} / ${Math.max(1, Number(context.totalPages || 1) || 1)}`;
    }

    if (fieldType === 3) {
        return formatDate(context.model, field.format ?? field.Format);
    }

    if (fieldType === 4) {
        return String(context.model?.metadata?.title || context.model?.metadata?.Title || field.fallbackText || field.FallbackText || '');
    }

    if (fieldType === 5) {
        return String(context.model?.metadata?.author?.displayName || context.model?.metadata?.Author?.DisplayName || field.fallbackText || field.FallbackText || '');
    }

    return String(field.displayText || field.DisplayText || field.fallbackText || field.FallbackText || '');
}

function layoutHeaderFooterBlocks(context) {
    const commands = [];
    const blocks = (context.headerFooter.blocks || [])
        .slice()
        .sort((left, right) => (Number(left?.order || 0) || 0) - (Number(right?.order || 0) || 0));
    let y = context.region.y + 6;
    let sequence = Number(context.sequenceStart || 0) || 0;
    for (const block of blocks) {
        const runs = Array.isArray(block?.content?.runs) ? block.content.runs : [];
        const line = layoutInlineLine({
            ...context,
            block,
            runs,
            y,
        });
        commands.push(...line.commands.map(command => ({ ...command, sequence: sequence++ })));
        y += line.height;
        if (y > context.region.y + context.region.height - 4) {
            break;
        }
    }

    return commands;
}

function layoutInlineLine(context) {
    const segments = [];
    let width = 0;
    let lineHeight = 18;
    for (const run of context.runs) {
        const style = createCanvasRunStyle(context.model, context.block, run);
        if (String(run?.type || '') === 'signingField' || run?.signingField) {
            const field = normalizeSigningFieldRun(run);
            const maxHeight = Math.max(8, (Number(context.region?.height) || 0) - 8);
            const boxHeight = Math.max(1, Math.min(field.boxHeight, maxHeight));
            const segment = { run, signingField: field, isSigningField: true, style, width: Math.max(1, field.boxWidth), height: boxHeight };
            lineHeight = Math.max(lineHeight, segment.height);
            width += segment.width;
            segments.push(segment);
            continue;
        }

        const text = String(run?.type || '') === 'field'
            ? resolveFieldText(run, context)
            : defaultRunText(run);
        if (!text) {
            continue;
        }

        const measured = context.metrics.measureText
            ? context.metrics.measureText(text, style)
            : context.metrics.measureRun({ text, ...style });
        const segment = {
            run,
            text,
            style,
            width: Math.max(1, Number(measured.width) || 1),
            height: Math.max(1, Number(measured.height ?? measured.lineHeight) || Number(style.fontSize || 14) * 1.25),
        };
        lineHeight = Math.max(lineHeight, segment.height);
        width += segment.width;
        segments.push(segment);
    }

    const alignment = normalizeCanvasAlignment(context.block?.paragraphProperties?.alignment ?? context.block?.paragraphProperties?.Alignment);
    let x = context.region.x;
    if (alignment === 'center') {
        x += Math.max(0, (context.region.width - width) / 2);
    } else if (alignment === 'right') {
        x += Math.max(0, context.region.width - width);
    }

    const commands = [];
    const baseline = context.y + lineHeight * 0.78;
    for (const segment of segments) {
        if (segment.isSigningField) {
            const field = segment.signingField;
            commands.push({
                id: `${context.headerFooter.id}-${context.block.id || 'block'}-${field.uuid}-${context.page.index}`,
                type: 'signingField',
                layer: CANVAS_RENDER_LAYERS.content,
                pageIndex: Number(context.page.index || 0) || 0,
                blockId: context.block?.id || '',
                runId: segment.run?.id || '',
                headerFooterId: context.headerFooter.id || '',
                region: context.region.region,
                fieldUuid: field.uuid,
                fieldType: field.fieldType,
                submitterUuid: field.submitterUuid,
                required: field.required,
                label: field.label,
                options: field.options,
                signingField: field,
                x,
                y: context.y,
                width: segment.width,
                height: segment.height,
                style: segment.style,
                marks: [],
            });
            x += segment.width;
            continue;
        }

        const isField = String(segment.run?.type || '') === 'field';
        commands.push({
            id: `${context.headerFooter.id}-${context.block.id || 'block'}-${segment.run?.id || commands.length}-${context.page.index}`,
            type: isField ? 'field' : 'textRun',
            layer: CANVAS_RENDER_LAYERS.content,
            pageIndex: Number(context.page.index || 0) || 0,
            blockId: context.block?.id || '',
            runId: segment.run?.id || '',
            headerFooterId: context.headerFooter.id || '',
            region: context.region.region,
            text: segment.text,
            x,
            y: context.y,
            baseline,
            width: segment.width,
            height: lineHeight,
            style: segment.style,
            marks: Array.isArray(segment.run?.marks) ? segment.run.marks : [],
        });
        x += segment.width;
    }

    return { commands, height: lineHeight + 2 };
}

function createRegion(page, headerFooter, type) {
    const frame = type === HEADER ? page.header || fallbackHeader(page) : page.footer || fallbackFooter(page);
    return {
        id: `${headerFooter.id}-${Number(page.index || 0)}`,
        headerFooterId: headerFooter.id || '',
        sectionId: page.sectionId || headerFooter.sectionId || '',
        pageIndex: Number(page.index || 0) || 0,
        region: type === HEADER ? 'Header' : 'Footer',
        scope: scopeName(headerFooter.scope ?? headerFooter.Scope),
        x: frame.x,
        y: frame.y,
        width: frame.width,
        height: frame.height,
    };
}

function resolveScope(section, pageIndex) {
    const properties = section?.properties || {};
    if (Number(pageIndex || 0) === 0 && properties.differentFirstPage === true) {
        return FIRST_PAGE;
    }

    if (properties.differentOddAndEvenPages === true) {
        return (Number(pageIndex || 0) + 1) % 2 === 0 ? EVEN_PAGES : ODD_PAGES;
    }

    return PRIMARY;
}

function findByReference(model, section, type, scope) {
    const references = Array.isArray(section?.properties?.headerFooterReferences)
        ? section.properties.headerFooterReferences
        : [];
    const reference = references.find(item => normalizeType(item?.type ?? item?.Type) === type && normalizeScope(item?.scope ?? item?.Scope) === scope);
    const id = String(reference?.headerFooterId ?? reference?.HeaderFooterId ?? '');
    return id
        ? (model?.headersFooters || []).find(item => String(item?.id || '') === id) || null
        : null;
}

function findByShape(headersFooters, section, type, scope) {
    const sectionId = String(section?.id || '');
    return headersFooters.find(item =>
        normalizeType(item?.type ?? item?.Type) === type
        && normalizeScope(item?.scope ?? item?.Scope) === scope
        && (!sectionId || !item?.sectionId || String(item.sectionId) === sectionId))
        || null;
}

function resolveSection(model, sectionId) {
    const id = String(sectionId || '');
    const sections = Array.isArray(model?.sections) ? model.sections : [];
    return sections.find(item => String(item?.id || '') === id)
        || sections.slice().sort((left, right) => (Number(left?.order || 0) || 0) - (Number(right?.order || 0) || 0))[0]
        || null;
}

function defaultRunText(run) {
    if (String(run?.type || '') === 'noteReference') {
        return String(run?.noteReference?.displayMarker || '');
    }

    if (String(run?.type || '') === 'token') {
        return String(run?.token?.displayName || run?.token?.fallbackText || run?.text || '');
    }

    return String(run?.text || '');
}

function formatDate(model, format) {
    const raw = model?.metadata?.modifiedAt || model?.metadata?.ModifiedAt || model?.metadata?.createdAt || model?.metadata?.CreatedAt;
    const value = raw ? new Date(raw) : new Date();
    if (Number.isNaN(value.getTime())) {
        return '';
    }

    const normalized = String(format || '').toLowerCase();
    if (normalized === 'yyyy-mm-dd') {
        return value.toISOString().slice(0, 10);
    }

    return value.toLocaleDateString();
}

function normalizeType(value) {
    if (typeof value === 'number') {
        return value === FOOTER ? FOOTER : HEADER;
    }

    return String(value || '').toLowerCase() === 'footer' ? FOOTER : HEADER;
}

function normalizeScope(value) {
    if (typeof value === 'number') {
        return Math.max(PRIMARY, Math.min(ODD_PAGES, Math.trunc(value)));
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'firstpage') {
        return FIRST_PAGE;
    }

    if (normalized === 'evenpages' || normalized === 'even') {
        return EVEN_PAGES;
    }

    if (normalized === 'oddpages' || normalized === 'odd') {
        return ODD_PAGES;
    }

    return PRIMARY;
}

function normalizeFieldType(value) {
    if (typeof value === 'number') {
        return Math.max(0, Math.trunc(value));
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'pagecount' || normalized === 'totalpages') {
        return 1;
    }

    if (normalized === 'pagexofy') {
        return 2;
    }

    if (normalized === 'date') {
        return 3;
    }

    if (normalized === 'documenttitle' || normalized === 'title') {
        return 4;
    }

    if (normalized === 'author') {
        return 5;
    }

    return 0;
}

function scopeName(value) {
    return ['Primary', 'FirstPage', 'EvenPages', 'OddPages'][normalizeScope(value)] || 'Primary';
}

function fallbackHeader(page) {
    return { x: page.body.x, y: 24, width: page.body.width, height: 36 };
}

function fallbackFooter(page) {
    return { x: page.body.x, y: page.height - 60, width: page.body.width, height: 36 };
}

function ensureMetrics(metrics) {
    return metrics || createFontMetricsService();
}
