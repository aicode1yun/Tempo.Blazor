import { buildDisplayList } from './display-list.mjs';
import { normalizeReviewDisplayMode, normalizeRevisionType, REVIEW_DISPLAY_MODES } from '../annotations/revision-render.mjs';

const REDLINE_COLORS = Object.freeze({
    insertion: '#1d4ed8',
    deletion: '#dc2626',
    formatting: '#7c3aed',
});
const REDLINE_NOTE_WIDTH = 84;
const REDLINE_BAR_X = 18;

// Print/PDF layout snapshot export (schema v1).
//
// Distils the canvas display list — the exact commands the editor paints — into a
// page-indexed list of print primitives (text / rect / line / image / path) whose
// field names mirror Tempo.Reporting.Engine's ReportSnapshotCommand, so the server
// PDF renderer can translate them 1:1 and inherit the editor's line and page
// breaking (WYSIWYG parity by construction). Screen-only chrome (margin guides,
// body outlines, comment/revision anchors, signing overlays, diagnostics) never
// reaches the export.

const EXCLUDED_TYPES = new Set([
    'pageFill', 'pageBorder', 'bodyArea', 'marginGuide', 'columnSeparator',
    'paragraphBox', 'glyphRun', 'commentAnchor', 'revisionAnchor', 'signingField',
    'diagnosticOverlay', 'debugBounds', 'headerFooterFrame',
]);

const TEXT_TYPES = new Set([
    'textRun', 'field', 'listLabel', 'lineNumber', 'noteMarker', 'imageCaption',
    'drawingText', 'watermarkText',
]);

const RECT_TYPES = new Set(['tableBox', 'tableCell', 'drawingRun', 'noteSeparator']);

export function buildLayoutSnapshotExport(model, layout, options = {}) {
    return translateDisplayListToLayoutSnapshot(buildDisplayList(model, layout, options), {
        revisions: model?.revisions,
        reviewDisplayMode: options.reviewDisplayMode,
    });
}

// Translate an already-built display list (typically the engine's LIVE one, laid out with the
// browser's real font metrics) into the print snapshot. This is the interop entry's core.
// options.revisions + options.reviewDisplayMode enable redline printing: revision-marked runs get
// tracked-changes styling (deletions struck through red, insertions underlined blue) plus margin
// change bars and author notes — in markup review modes only.
export function translateDisplayListToLayoutSnapshot(displayList, options = {}) {
    const pages = (displayList?.pages || []).map((page, index) => ({
        index: Number(page.index ?? index) || index,
        width: Number(page.width) || 0,
        height: Number(page.height) || 0,
        commands: [],
    }));
    const pagesByIndex = new Map(pages.map(page => [page.index, page]));
    const redline = createRedlineContext(displayList, options);

    for (const command of displayList?.commands || []) {
        if (!command || EXCLUDED_TYPES.has(command.type)) {
            continue;
        }

        const page = pagesByIndex.get(Number(command.pageIndex) || 0);
        if (!page) {
            continue;
        }

        const revision = redline?.byRunId.get(String(command.runId || ''));
        for (const exported of translateCommand(command)) {
            if (revision && exported.type === 'text') {
                applyRedlineStyle(exported, revision);
            }

            page.commands.push(exported);
            if (revision && exported.type === 'text') {
                pushRedlineMarginMarkers(page, exported, revision, redline);
            }
        }
    }

    return {
        schemaVersion: 1,
        pageCount: pages.length,
        pages,
    };
}

function createRedlineContext(displayList, options) {
    const revisions = Array.isArray(options?.revisions) ? options.revisions : [];
    if (revisions.length === 0) {
        return null;
    }

    const mode = normalizeReviewDisplayMode(options.reviewDisplayMode);
    if (mode !== REVIEW_DISPLAY_MODES.allMarkup && mode !== REVIEW_DISPLAY_MODES.simpleMarkup) {
        return null;
    }

    const byRevisionId = new Map();
    for (const revision of revisions) {
        const id = String(revision?.id || revision?.Id || '');
        if (!id) {
            continue;
        }

        byRevisionId.set(id, {
            id,
            type: normalizeRevisionType(revision?.type ?? revision?.Type),
            author: String(revision?.author?.displayName || revision?.Author?.DisplayName || ''),
        });
    }

    // revisionAnchor commands pair each revision-marked text run (by runId) with its revision.
    const byRunId = new Map();
    for (const command of displayList?.commands || []) {
        if (command?.type !== 'revisionAnchor') {
            continue;
        }

        const revision = byRevisionId.get(String(command.revisionId || ''));
        const runId = String(command.runId || '');
        if (revision && runId && !byRunId.has(runId)) {
            byRunId.set(runId, revision);
        }
    }

    return byRunId.size > 0 ? { byRunId, notedRevisions: new Set() } : null;
}

function applyRedlineStyle(exported, revision) {
    const color = REDLINE_COLORS[revision.type] || REDLINE_COLORS.insertion;
    if (revision.type === 'deletion') {
        exported.strikeThrough = true;
        exported.fill = color;
    } else if (revision.type === 'insertion') {
        exported.underline = true;
        exported.fill = color;
    }
    // formatting revisions keep the text style — the margin bar + note carry the signal.
}

function pushRedlineMarginMarkers(page, exported, revision, redline) {
    const color = REDLINE_COLORS[revision.type] || REDLINE_COLORS.insertion;
    page.commands.push({
        id: `${exported.id}-redline-bar`,
        type: 'line',
        sourceType: 'revisionBar',
        x: REDLINE_BAR_X,
        y: exported.y,
        width: 0,
        height: exported.height,
        stroke: color,
        strokeWidth: 2,
    });

    const noteKey = `${revision.id}@${page.index}`;
    if (redline.notedRevisions.has(noteKey) || !revision.author) {
        return;
    }

    redline.notedRevisions.add(noteKey);
    const sign = revision.type === 'deletion' ? '−' : revision.type === 'formatting' ? '±' : '+';
    page.commands.push({
        id: `${revision.id}-redline-note-${page.index}`,
        type: 'text',
        sourceType: 'revisionNote',
        x: Math.max(0, page.width - REDLINE_NOTE_WIDTH - 6),
        y: exported.y,
        width: REDLINE_NOTE_WIDTH,
        height: 10,
        baseline: exported.baseline ?? exported.y + 8,
        text: `${sign} ${revision.author}`,
        fontFamily: 'Arial',
        fontSize: 8,
        fontWeight: '400',
        fontStyle: 'italic',
        fill: color,
    });
}

function translateCommand(command) {
    const type = String(command.type || '');

    if (TEXT_TYPES.has(type) || (type === 'formControl' && command.renderState?.showChrome !== true)) {
        return textCommand(command);
    }

    if (type === 'mathEquation') {
        return mathCommand(command);
    }

    if (RECT_TYPES.has(type)) {
        return rectCommand(command);
    }

    if (type === 'tabLeader') {
        return tabLeaderCommand(command);
    }

    if (type === 'imageObject' || type === 'image' || type === 'watermarkImage') {
        return imageCommand(command);
    }

    if (type === 'drawingLine') {
        return drawingLineCommand(command);
    }

    if (type === 'drawingShapeFill' || type === 'drawingShapeStroke' || type === 'drawingShape') {
        return drawingShapeCommand(command);
    }

    // drawingShapeEffect (shadows) and unrecognised screen-only helpers are not print primitives.
    return [];
}

function textCommand(command) {
    const style = command.style || {};
    // Some commands (e.g. caption continuation lines) carry no explicit font size — derive it from
    // the laid-out line height instead of a fixed default, or the PDF prints them oversized.
    const rawHeight = Number(command.height) || 0;
    const fontSize = Number(style.fontSize) || (rawHeight > 0 ? round(rawHeight / 1.25) : 16);
    const height = Math.max(1, rawHeight || fontSize * 1.25);
    const baseline = Number(command.baseline) || Number(command.y) || 0;
    const decorations = Array.isArray(style.decorations) ? style.decorations : [];
    const text = String(command.text || '');
    if (text.length === 0) {
        return [];
    }

    return [compact({
        id: String(command.id || ''),
        type: 'text',
        sourceType: command.type,
        x: round(Number(command.x) || 0),
        y: round(baseline - height * 0.78),
        width: round(Math.max(0, Number(command.width) || 0)),
        height: round(height),
        baseline: round(baseline),
        text,
        fontFamily: String(style.fontFamily || ''),
        fontSize: round(fontSize),
        fontWeight: String(style.fontWeight || '400'),
        fontStyle: String(style.fontStyle || 'normal'),
        letterSpacing: Number(style.letterSpacing) || 0,
        fill: String(style.color || '#111827'),
        underline: decorations.includes('underline'),
        strikeThrough: decorations.includes('line-through') || decorations.includes('strikethrough'),
        highlight: style.backgroundColor ? String(style.backgroundColor) : null,
        rotation: Number(command.rotation) || 0,
    })];
}

function mathCommand(command) {
    // Math equations print as their linearised text at the laid-out bounds. The canvas
    // paints the structural layout; the PDF keeps position, size and searchability.
    const layout = command.mathLayout || {};
    const height = Math.max(1, Number(layout.height) || Number(command.height) || 18);
    const baseline = Number(command.baseline) || (Number(command.y) || 0) + height * 0.78;
    const text = String(command.text || '');
    if (text.length === 0) {
        return [];
    }

    return [compact({
        id: String(command.id || ''),
        type: 'text',
        sourceType: 'mathEquation',
        x: round(Number(command.x) || 0),
        y: round(baseline - height * 0.78),
        width: round(Math.max(0, Number(layout.width) || Number(command.width) || 0)),
        height: round(height),
        baseline: round(baseline),
        text,
        fontFamily: String(command.style?.fontFamily || ''),
        fontSize: round(Number(command.style?.fontSize) || height * 0.8),
        fontWeight: '400',
        fontStyle: 'italic',
        fill: String(command.style?.color || '#111827'),
    })];
}

function rectCommand(command) {
    const fill = command.fill ? String(command.fill) : null;
    const stroke = command.stroke ? String(command.stroke) : null;
    if (!fill && !stroke) {
        return [];
    }

    return [compact({
        id: String(command.id || ''),
        type: 'rect',
        sourceType: command.type,
        x: round(Number(command.x) || 0),
        y: round(Number(command.y) || 0),
        width: round(Math.max(0, Number(command.width) || 0)),
        height: round(Math.max(0, Number(command.height) || 0)),
        fill,
        stroke,
        strokeWidth: stroke ? Math.max(0.5, Number(command.lineWidth) || 1) : 0,
    })];
}

function tabLeaderCommand(command) {
    const height = Math.max(1, Number(command.height) || 14);
    const baseline = Number(command.baseline) || (Number(command.y) || 0) + height * 0.78;
    const color = String(command.style?.color || command.style?.foreground || '#334155');
    return [compact({
        id: String(command.id || ''),
        type: 'line',
        sourceType: 'tabLeader',
        x: round(Number(command.x) || 0),
        y: round(baseline - 2),
        width: round(Math.max(0, Number(command.width) || 0)),
        height: 0,
        stroke: color,
        strokeWidth: 0.75,
    })];
}

function imageCommand(command) {
    const url = String(command.url || command.src || command.source || '');
    const base = {
        id: String(command.id || ''),
        x: round(Number(command.x) || 0),
        y: round(Number(command.y) || 0),
        width: round(Math.max(1, Number(command.width) || 1)),
        height: round(Math.max(1, Number(command.height) || 1)),
    };

    if (url.length > 0) {
        return [compact({
            ...base,
            type: 'image',
            sourceType: command.type,
            source: url,
            rotation: Number(command.rotation) || 0,
        })];
    }

    // Sourceless images keep their footprint visible (same fallback the canvas paints).
    return [compact({
        ...base,
        type: 'rect',
        sourceType: command.type,
        fill: String(command.fill || 'rgba(226, 232, 240, 0.48)'),
        stroke: String(command.stroke || '#94a3b8'),
        strokeWidth: Math.max(0.5, Number(command.lineWidth) || 1),
    })];
}

function drawingLineCommand(command) {
    const stroke = command.shape?.stroke || {};
    return [compact({
        id: String(command.id || ''),
        type: 'line',
        sourceType: 'drawingLine',
        x: round(Number(command.x) || 0),
        y: round(Number(command.y) || 0),
        width: round(Number(command.width) || 0),
        height: round(Number(command.height) || 0),
        stroke: String(stroke.color || '#334155'),
        strokeWidth: Math.max(0.5, Number(stroke.width) || 1.5),
    })];
}

function drawingShapeCommand(command) {
    const shape = command.shape || {};
    const fillSpec = shape.fill || {};
    const strokeSpec = shape.stroke || {};
    const wantsFill = command.type !== 'drawingShapeStroke' && String(fillSpec.type || 'solid').toLowerCase() !== 'none';
    const wantsStroke = command.type !== 'drawingShapeFill' && Number(strokeSpec.width ?? 1.5) > 0;
    const fill = wantsFill ? String(fillSpec.color || fillSpec.value || '#e2e8f0') : null;
    const stroke = wantsStroke ? String(strokeSpec.color || '#334155') : null;
    if (!fill && !stroke) {
        return [];
    }

    const x = Number(command.x) || 0;
    const y = Number(command.y) || 0;
    const width = Math.max(1, Number(command.width) || 1);
    const height = Math.max(1, Number(command.height) || 1);
    const preset = String(shape.preset || 'rectangle').toLowerCase();
    const base = {
        id: String(command.id || ''),
        sourceType: command.type,
        x: round(x),
        y: round(y),
        width: round(width),
        height: round(height),
        fill,
        stroke,
        strokeWidth: stroke ? Math.max(0.5, Number(strokeSpec.width) || 1.5) : 0,
    };

    if (preset === 'ellipse' || preset === 'circle') {
        const rx = width / 2;
        const ry = height / 2;
        const cx = x + rx;
        const cy = y + ry;
        return [compact({
            ...base,
            type: 'path',
            pathData: `M ${round(cx - rx)} ${round(cy)} `
                + `A ${round(rx)} ${round(ry)} 0 1 0 ${round(cx + rx)} ${round(cy)} `
                + `A ${round(rx)} ${round(ry)} 0 1 0 ${round(cx - rx)} ${round(cy)} Z`,
        })];
    }

    return [compact({ ...base, type: 'rect' })];
}

function round(value) {
    return Math.round((Number(value) || 0) * 100) / 100;
}

function compact(object) {
    const result = {};
    for (const [key, value] of Object.entries(object)) {
        if (value !== null && value !== undefined) {
            result[key] = value;
        }
    }

    return result;
}
