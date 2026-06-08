import { createFontMetricsService } from '../../document-editor/layout/font-metrics.mjs';
import { createCanvasRunStyle } from './canvas-text-style.mjs';
import { resolveFieldText } from './header-footer-layout.mjs';
import { CANVAS_RENDER_LAYERS } from '../render/layers.mjs';

const FOOTNOTE = 0;
const ENDNOTE = 1;

export function buildNotesLayout(model, textLayout, options = {}) {
    const metrics = options.fontMetrics || createFontMetricsService();
    const pages = Array.isArray(textLayout?.pages) ? textLayout.pages : [];
    const notes = Array.isArray(model?.notes) ? model.notes : [];
    const referencePages = noteReferencePageMap(textLayout);
    const commands = [];
    const regions = [];
    let sequence = 0;

    for (const page of pages) {
        const footnotes = notes.filter(note => normalizeNoteType(note?.type ?? note?.Type) === FOOTNOTE && noteOnPage(note, referencePages, page.index));
        if (footnotes.length > 0) {
            const layout = layoutNoteGroup({
                model,
                notes: footnotes,
                page,
                noteType: FOOTNOTE,
                metrics,
                totalPages: pages.length,
                sequenceStart: sequence,
            });
            commands.push(...layout.commands);
            regions.push(layout.region);
            sequence += layout.commands.length;
        }
    }

    const endnotes = notes.filter(note => normalizeNoteType(note?.type ?? note?.Type) === ENDNOTE);
    if (endnotes.length > 0 && pages.length > 0) {
        const page = pages[pages.length - 1];
        const layout = layoutNoteGroup({
            model,
            notes: endnotes,
            page,
            noteType: ENDNOTE,
            metrics,
            totalPages: pages.length,
            sequenceStart: sequence,
        });
        commands.push(...layout.commands);
        regions.push(layout.region);
    }

    return { regions, commands };
}

function layoutNoteGroup(context) {
    const lineHeight = 16;
    const requestedHeight = Math.min(112, Math.max(28, 10 + context.notes.length * lineHeight * 1.45));
    const frame = noteFrame(context.page, requestedHeight);
    const region = {
        id: `${context.noteType === ENDNOTE ? 'endnotes' : 'footnotes'}-${context.page.index}`,
        noteType: context.noteType === ENDNOTE ? 'Endnote' : 'Footnote',
        pageIndex: Number(context.page.index || 0) || 0,
        x: frame.x,
        y: frame.y,
        width: frame.width,
        height: frame.height,
    };
    const commands = [
        {
            id: `${region.id}-separator`,
            type: 'noteSeparator',
            layer: CANVAS_RENDER_LAYERS.annotations,
            pageIndex: region.pageIndex,
            noteType: region.noteType,
            x: region.x,
            y: region.y,
            width: Math.min(180, region.width * 0.36),
            height: 1,
            stroke: 'rgba(100, 116, 139, 0.7)',
            lineWidth: 1,
            sequence: context.sequenceStart,
        },
    ];
    let sequence = context.sequenceStart + 1;
    let y = region.y + 10;
    for (const note of context.notes) {
        const marker = String(note?.marker ?? note?.Marker ?? '');
        const markerStyle = noteMarkerStyle(context.model);
        const markerWidth = Math.max(10, context.metrics.measureText ? context.metrics.measureText(marker, markerStyle).width : 10);
        commands.push({
            id: `${note.id || note.Id}-marker`,
            type: 'noteMarker',
            layer: CANVAS_RENDER_LAYERS.content,
            pageIndex: region.pageIndex,
            noteId: note.id || note.Id || '',
            noteType: region.noteType,
            text: marker,
            x: region.x,
            y,
            baseline: y + lineHeight * 0.78,
            width: markerWidth,
            height: lineHeight,
            style: markerStyle,
            sequence: sequence++,
        });

        const textCommands = layoutNoteBlocks({
            ...context,
            note,
            region,
            x: region.x + markerWidth + 8,
            y,
            lineHeight,
            sequenceStart: sequence,
        });
        commands.push(...textCommands);
        sequence += textCommands.length;
        y += Math.max(lineHeight + 4, textCommands.reduce((max, command) => Math.max(max, Number(command.y || 0) + Number(command.height || 0) - y), lineHeight + 4));
    }

    return { region, commands };
}

function noteFrame(page, requestedHeight) {
    const body = page.body || { x: 72, y: 72, width: Math.max(1, Number(page.width || 794) - 144), height: Math.max(1, Number(page.height || 1123) - 144) };
    const belowBodyY = Number(body.y || 0) + Number(body.height || 0) + 4;
    const footerY = Number(page.footer?.y ?? (Number(page.height || 0) - 8));
    const availableBelowBody = Math.max(0, footerY - belowBodyY - 4);
    if (availableBelowBody >= 24) {
        return {
            x: body.x,
            y: belowBodyY,
            width: body.width,
            height: Math.max(24, Math.min(requestedHeight, availableBelowBody)),
        };
    }

    return {
        x: body.x,
        y: Math.max(Number(body.y || 0), Number(body.y || 0) + Number(body.height || 0) - requestedHeight),
        width: body.width,
        height: requestedHeight,
    };
}

function layoutNoteBlocks(context) {
    const commands = [];
    let sequence = context.sequenceStart;
    let x = context.x;
    let y = context.y;
    for (const block of (context.note.blocks || context.note.Blocks || [])) {
        for (const run of (block?.content?.runs || block?.Content?.Runs || [])) {
            const style = createCanvasRunStyle(context.model, block, run);
            const text = String(run?.type || '') === 'field'
                ? resolveFieldText(run, { ...context, pageIndex: context.page.index })
                : runText(run);
            if (!text) {
                continue;
            }

            const measured = context.metrics.measureText
                ? context.metrics.measureText(text, style)
                : context.metrics.measureRun({ text, ...style });
            const width = Math.max(1, Math.min(context.region.x + context.region.width - x, Number(measured.width || 0) || 1));
            commands.push({
                id: `${context.note.id || context.note.Id}-${block.id || 'block'}-${run.id || commands.length}`,
                type: String(run?.type || '') === 'field' ? 'field' : 'textRun',
                layer: CANVAS_RENDER_LAYERS.content,
                pageIndex: context.region.pageIndex,
                blockId: block?.id || '',
                runId: run?.id || '',
                noteId: context.note.id || context.note.Id || '',
                noteType: context.region.noteType,
                text,
                x,
                y,
                baseline: y + context.lineHeight * 0.78,
                width,
                height: context.lineHeight,
                style,
                marks: Array.isArray(run?.marks) ? run.marks : [],
                sequence: sequence++,
            });
            x += width;
        }

        y += context.lineHeight + 2;
        x = context.x;
    }

    return commands;
}

function noteReferencePageMap(textLayout) {
    const map = new Map();
    for (const block of textLayout?.blocks || []) {
        for (const segment of block?.segments || []) {
            const runId = String(segment?.runId || '');
            if (runId) {
                map.set(runId, Number(segment.pageIndex ?? block.pageIndex ?? 0) || 0);
            }
        }
    }

    for (const rect of textLayout?.textRects || []) {
        const runId = String(rect?.runId || '');
        if (runId && !map.has(runId)) {
            map.set(runId, Number(rect.pageIndex || 0) || 0);
        }
    }

    return map;
}

function noteOnPage(note, referencePages, pageIndex) {
    const references = Array.isArray(note?.referenceIds) ? note.referenceIds : [];
    if (references.length === 0) {
        return Number(pageIndex || 0) === 0;
    }

    return references.some(id => Number(referencePages.get(String(id)) ?? 0) === Number(pageIndex || 0));
}

function noteMarkerStyle(model) {
    const fontSize = Math.max(10, Number(model?.theme?.bodyFontSize || model?.theme?.BodyFontSize || 11) * 96 / 72 * 0.78);
    return {
        fontFamily: model?.theme?.bodyFontFamily || model?.theme?.BodyFontFamily || 'Aptos, Arial, sans-serif',
        fontSize,
        fontWeight: '600',
        fontStyle: 'normal',
        color: '#334155',
        backgroundColor: null,
        decorations: [],
    };
}

function runText(run) {
    if (String(run?.type || '') === 'noteReference') {
        return String(run?.noteReference?.displayMarker || '');
    }

    if (String(run?.type || '') === 'token') {
        return String(run?.token?.displayName || run?.token?.fallbackText || run?.text || '');
    }

    return String(run?.text || '');
}

function normalizeNoteType(value) {
    if (typeof value === 'number') {
        return value === ENDNOTE ? ENDNOTE : FOOTNOTE;
    }

    return String(value || '').toLowerCase() === 'endnote' ? ENDNOTE : FOOTNOTE;
}
