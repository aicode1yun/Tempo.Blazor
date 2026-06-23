// Phase D — layout/page-metrics.mjs
// Page-box geometry helpers extracted from the legacy IIFE. All pure (no DOM access,
// no closure over engine state). They translate a document's PageSettings/options into
// the rectangles used by the paragraph engine.

import { asArray, asText, clone, sortObject } from '../core/helpers.mjs';

// Normalize an options bag holding x/y/width/height (camel or Pascal). Width/height
// default to a Letter-ish 640×900 px when not provided, matching the legacy default.
export function normalizePageBox(options) {
    const opts = options || {};
    const page = opts.page || opts.Page || opts.pageBox || opts.PageBox || {};
    return {
        x: Number(page.x || page.X || opts.x || opts.X || 0) || 0,
        y: Number(page.y || page.Y || opts.y || opts.Y || 0) || 0,
        width: Math.max(1, Number(page.width || page.Width || opts.width || opts.Width || 640) || 640),
        height: Math.max(1, Number(page.height || page.Height || opts.height || opts.Height || 900) || 900),
    };
}

// Compute the full layout metrics for a document — page size, margins, header/footer
// heights, body dimensions, paragraph spacing. Used by the paragraph engine to decide
// where each line fits. `options` overrides anything in `model.pageSettings`.
export function normalizePageLayoutSettings(options, model) {
    const opts = options || {};
    const source = Object.assign({}, (model && (model.pageSettings || model.PageSettings)) || {}, opts);
    const page = normalizePageBox(source);
    const margins = source.margins || source.Margins || {};
    const marginTop = Number(margins.top ?? margins.Top ?? source.marginTop ?? source.MarginTop ?? 0) || 0;
    const marginRight = Number(margins.right ?? margins.Right ?? source.marginRight ?? source.MarginRight ?? 0) || 0;
    const marginBottom = Number(margins.bottom ?? margins.Bottom ?? source.marginBottom ?? source.MarginBottom ?? 0) || 0;
    const marginLeft = Number(margins.left ?? margins.Left ?? source.marginLeft ?? source.MarginLeft ?? 0) || 0;
    const headerHeight = Math.max(0, Number(source.headerHeight ?? source.HeaderHeight ?? 0) || 0);
    const footerHeight = Math.max(0, Number(source.footerHeight ?? source.FooterHeight ?? 0) || 0);
    const pageGap = Math.max(0, Number(source.pageGap ?? source.PageGap ?? 24) || 24);
    const bodyWidth = Math.max(1, page.width - marginLeft - marginRight);
    const bodyHeight = Math.max(1, page.height - marginTop - marginBottom - headerHeight - footerHeight);
    return sortObject({
        pageSize: { width: page.width, height: page.height },
        pageOrigin: { x: page.x, y: page.y },
        margins: { top: marginTop, right: marginRight, bottom: marginBottom, left: marginLeft },
        headerHeight,
        footerHeight,
        bodySize: { width: bodyWidth, height: bodyHeight },
        pageGap,
        paragraphSpacingBefore: Math.max(0,
            Number(source.paragraphSpacingBefore ?? source.ParagraphSpacingBefore ?? 0) || 0),
        paragraphSpacingAfter: Math.max(0,
            Number(source.paragraphSpacingAfter ?? source.ParagraphSpacingAfter
                ?? (source.blockGap ?? source.BlockGap ?? 8)) || 8),
        blockGap: Math.max(0, Number(source.blockGap ?? source.BlockGap ?? 8) || 8),
        lineGap: Math.max(0, Number(source.lineGap ?? source.LineGap ?? 0) || 0),
        minReadableWidth: Math.max(1, Number(source.minReadableWidth ?? source.MinReadableWidth ?? 48) || 48),
    });
}

// Build the page rectangle (outer + body + header/footer frames) for the page at
// `pageIndex`. Pages stack vertically with `metrics.pageGap` between them.
export function createPageLayout(pageIndex, metrics) {
    const rect = {
        x: metrics.pageOrigin.x,
        y: metrics.pageOrigin.y + pageIndex * (metrics.pageSize.height + metrics.pageGap),
        width: metrics.pageSize.width,
        height: metrics.pageSize.height,
    };
    const headerFrame = {
        x: rect.x + metrics.margins.left,
        y: rect.y + metrics.margins.top,
        width: metrics.bodySize.width,
        height: metrics.headerHeight,
    };
    const bodyFrame = {
        x: rect.x + metrics.margins.left,
        y: rect.y + metrics.margins.top + metrics.headerHeight,
        width: metrics.bodySize.width,
        height: metrics.bodySize.height,
    };
    const footerFrame = {
        x: rect.x + metrics.margins.left,
        y: bodyFrame.y + bodyFrame.height,
        width: metrics.bodySize.width,
        height: metrics.footerHeight,
    };
    return sortObject({
        pageNumber: pageIndex + 1,
        pageIndex,
        rect,
        marginBox: {
            x: rect.x + metrics.margins.left,
            y: rect.y + metrics.margins.top,
            width: metrics.bodySize.width,
            height: Math.max(1, rect.height - metrics.margins.top - metrics.margins.bottom),
        },
        headerFrame,
        bodyFrame,
        footerFrame,
        blockIds: [],
        exclusions: [],
    });
}

// Layout entry for a manual page-break block (height = 0, sits at top of body frame).
export function createPageBreakLayout(block, page, version) {
    const frame = page.bodyFrame;
    return sortObject({
        ok: true,
        id: 'layout-' + ((block && block.id) || 'page-break'),
        layoutVersion: version,
        blockId: (block && block.id) || 'page-break',
        type: 'pageBreak',
        pageIndex: page.pageIndex,
        rect: { x: frame.x, y: frame.y, width: frame.width, height: 0 },
        lines: [],
        segments: [],
        caretStops: [],
        baselines: [],
        manualPageBreak: true,
    });
}

// ────────────────────────────────────────────────────────────────────────────────
// Shift helpers — move a previously-laid-out fragment vertically to a new page.
// ────────────────────────────────────────────────────────────────────────────────

export function shiftRectY(rect, deltaY) {
    const next = clone(rect || {});
    next.y = Number(next.y || 0) + deltaY;
    return next;
}

export function shiftLayoutLine(line, deltaY, pageIndex) {
    const next = clone(line || {});
    next.pageIndex = pageIndex;
    next.rect = shiftRectY(next.rect, deltaY);
    next.baseline = Number(next.baseline || 0) + deltaY;
    next.availableIntervals = asArray(next.availableIntervals).map(interval => {
        const c = Object.assign({}, interval);
        c.y = Number(c.y || 0) + deltaY;
        c.pageIndex = pageIndex;
        return c;
    });
    return next;
}

export function shiftLayoutSegment(segment, deltaY, pageIndex) {
    const next = clone(segment || {});
    next.pageIndex = pageIndex;
    next.rect = shiftRectY(next.rect, deltaY);
    if (next.objectRect) next.objectRect = shiftRectY(next.objectRect, deltaY);
    return next;
}

export function shiftCaretStop(stop, deltaY, pageIndex) {
    const next = clone(stop || {});
    next.pageIndex = pageIndex;
    next.rect = shiftRectY(next.rect, deltaY);
    return next;
}

// ────────────────────────────────────────────────────────────────────────────────
// Field-text resolution — replace `pageNumber`/`totalPages` field runs with their
// concrete numeric values for a given page.
// ────────────────────────────────────────────────────────────────────────────────

export function resolveFieldRunText(run, pageNumber, totalPages) {
    const kind = String((run && (run.fieldType || run.FieldType || run.key || run.Key || '')) || '').toLowerCase();
    if (kind === 'pagenumber' || kind === 'page-number' || kind === 'page') return String(pageNumber);
    if (kind === 'totalpages' || kind === 'total-pages' || kind === 'pagecount' || kind === 'page-count') {
        return String(totalPages);
    }
    return asText(run && (run.text || run.Text || run.fallbackText || run.FallbackText || run.key || run.Key));
}

export function cloneBlockWithResolvedFields(block, pageNumber, totalPages) {
    const c = clone(block);
    if (c && c.type === 'paragraph') {
        asArray(c.content && c.content.runs).forEach(run => {
            if (run.kind === 'field' || run.fieldType || run.FieldType) {
                run.text = resolveFieldRunText(run, pageNumber, totalPages);
            }
        });
    }
    return c;
}
