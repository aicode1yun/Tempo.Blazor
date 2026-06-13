// Phase D — layout/paragraph-engine.mjs
// `createParagraphLayoutEngineFactory(deps)` → `createParagraphLayoutEngine(measurementService?, options?)` →
//   the paragraph / document layout engine. Orchestrates text measurement, line
//   breaking, anchored drawing placement, table cell layout, cross-page pagination,
//   and header/footer region layout.
//
// Injected deps (all required unless noted):
//   findBlock(model, blockId)               — index-based block lookup
//   normalizeImageObject(block, opts)        — normalises floating image records
//   createAnchoredDrawingResolvers(opts)     — factory → {resolveAnchoredDrawingReference,
//                                              createAnchoredDrawingLayoutObject}
//   createAnchoredDrawingRunCollector(opts)  — factory → {collectAnchoredDrawingRuns}
//   applySegmentStyleToElement? (DOM-only)  — called only inside renderParagraphLayout
//
// Pure sub-modules are imported directly (no injection needed).

import { asArray, asText, clone, unique } from '../core/helpers.mjs';

// Cold-layout optimization (perf+rendering fix 2026-06-08): the layout output is consumed by field
// name (canvas renderer, hit-test, selection) — never by serialized key order — so the deep canonical
// key sort that previously dominated layout time (and GC) is skipped. Kept as a named pass-through so
// the many call sites read unchanged. Determinism is preserved by the engine's fixed insertion order.
function sortObject(value) {
    return value;
}
import { createFontMetricsService } from './font-metrics.mjs';
import { createLineBreakerModule } from './line-breaker.mjs';
import {
    normalizeLineBreakerOptions,
    resolveLineRangesForBreaker,
    lineRangesAreInvalid,
    coalesceNonBreakingTokens,
    splitTokenIntoFittingPieces,
    applyJustifyMetadata,
} from './line-breaker-helpers.mjs';
import { createLineDraft, materializeLineDraft } from './line-draft.mjs';
import {
    tokenizeText, runForOffset as tokenRunForOffset, createParagraphTokenizer,
} from './paragraph-tokenizer.mjs';
import { createLineBreakerFallback } from './line-breaker-fallback.mjs';
import { normalizeParagraphLayoutOptions } from './paragraph-layout-options.mjs';
import { normalizeParagraphAlignment } from './paragraph-alignment.mjs';
import { normalizeLayoutSegmentStyle, decorationsFromMarks, applySegmentStyleToElement } from './segment-style.mjs';
import {
    normalizePageLayoutSettings, createPageLayout, createPageBreakLayout,
    shiftRectY, shiftLayoutLine, shiftLayoutSegment, shiftCaretStop,
    cloneBlockWithResolvedFields,
} from './page-metrics.mjs';
import {
    paragraphRectFromLines,
    createInlineObjectLayoutFromSegmentFactory,
    createLayoutObjectBlockFactory,
    firstScopeBlockId,
    findLayoutBlock,
} from './paragraph-layout-tree.mjs';
import { flattenParagraphRuns, runForOffset } from './paragraph-runs.mjs';
import { createLayoutScope, inferLayoutScopeFromOperation } from './layout-scope.mjs';
import { LayoutScopeKinds } from './scope-kinds.mjs';
import { getAvailableIntervals } from './available-intervals-cache.mjs';
import { createTextExclusionScopeDescriptor, textExclusionMatchesScope } from './text-exclusion-scope.mjs';
import { createTextExclusion } from './text-exclusion-factory.mjs';
import { createAnchoredDrawingLayoutScope, createAnchoredDrawingScopeAggregator } from './anchored-drawing-scope.mjs';
import { createScopedLayoutMetadataDecorator } from './scoped-layout-metadata.mjs';
import { createBlockIndexContext } from '../core/indexes.mjs';
import { collectLayoutLineIntervals } from './caret-interval-collector.mjs';
import { normalizeAnchorRegionName } from '../objects/anchor-region.mjs';
import { normalizeTextExclusionColumnIndex } from '../core/normalize-target.mjs';
import { readTextExclusionScope } from './text-exclusion.mjs';
import { readObjectLayoutInCell } from '../objects/anchor-region.mjs';
import { rectFromGeometry, rectIntersectsGeometry } from '../objects/geometry.mjs';
import { resolveObjectOverlapGeometry } from '../objects/overlap-geometry.mjs';
import { tableColumnCount } from '../core/text-helpers.mjs';
import { inlineAtOffset } from '../core/run-finders.mjs';
import { normalizeWrapModeName } from '../objects/wrap-modes.mjs';
// Anchored-drawing resolver/collector + their deps are imported directly (pure modules)
// so the engine owns the wiring; only `findBlock` (index-based, engine-state) is injected.
import { normalizeImageObject as defaultNormalizeImageObject } from '../objects/image-object.mjs';
import { createAnchoredDrawingResolvers } from '../objects/anchored-drawing-layout.mjs';
import { createAnchoredDrawingRunCollector } from '../objects/anchored-drawing-collector.mjs';
import { findLayoutBlockById, findReferenceLineForOffset } from './layout-block-finder.mjs';
import { drawingLayerForWrapMode } from '../objects/layer-priority.mjs';
import { wrapModeCreatesTextExclusion } from '../objects/wrap-mode-value.mjs';
import { resolveAnchoredDrawingRect } from '../objects/anchored-drawing-position.mjs';

const REQUIRED_DEPS = ['findBlock'];

export function createParagraphLayoutEngineFactory(deps) {
    const opts = deps || {};
    for (const key of REQUIRED_DEPS) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createParagraphLayoutEngineFactory requires deps.${key} (function)`);
        }
    }
    const { findBlock } = opts;
    // Allow an explicit override (tests) but default to the real module.
    const normalizeImageObject = typeof opts.normalizeImageObject === 'function'
        ? opts.normalizeImageObject
        : defaultNormalizeImageObject;

    // Build the anchored-drawing resolvers + run collector with their full real dep set.
    const { resolveAnchoredDrawingReference, createAnchoredDrawingLayoutObject } =
        createAnchoredDrawingResolvers({
            findLayoutBlockById,
            findReferenceLineForOffset,
            drawingLayerForWrapMode,
            wrapModeCreatesTextExclusion,
            readObjectLayoutInCell,
            normalizeTextExclusionColumnIndex,
            resolveAnchoredDrawingRect,
        });
    // The collector factory returns the function directly (not an object).
    const collectAnchoredDrawingRuns = createAnchoredDrawingRunCollector({ normalizeImageObject });

    // Per-block layout helpers (factories need injected normalizeImageObject)
    const createInlineObjectLayoutFromSegment = createInlineObjectLayoutFromSegmentFactory({ normalizeImageObject, normalizeWrapModeName });
    const layoutObjectBlock = createLayoutObjectBlockFactory({ normalizeImageObject });

    return function createParagraphLayoutEngine(measurementService, options) {
        // R.4.0 — default to the real-canvas font-metrics service. In headless (Node)
        // environments it auto-falls-back to the identical synthetic width model, so
        // existing Node tests stay byte-stable; in the browser it upgrades to real
        // glyph metrics. Callers may still inject any measurement service explicitly.
        const service = measurementService || createFontMetricsService();
        const defaults = options || {};
        const tokenizerFactory = createParagraphTokenizer({
            tokenizeText,
            createTextMeasurementService: () => service,
            normalizeParagraphAlignment,
            normalizeImageObject,
        });
        // The fallback builder needs `service.measureText(text, style) → {width,height}`.
        // The font-metrics service (R.4.0) provides it natively; the legacy synthetic
        // service does not, so we wrap only when it's missing. (Wrapping a frozen
        // service that already owns `measureText` would throw on the read-only prop.)
        const serviceWithMeasureText = typeof service.measureText === 'function'
            ? service
            : Object.assign(Object.create(service), {
                measureText: function (text, style) {
                    const result = service.measureTextRun({ text, ...style });
                    return { width: result ? result.width : 0, height: style && style.fontSize ? style.fontSize * 1.25 : 20 };
                },
            });
        const fallbackFactory = createLineBreakerFallback({
            tokensForParagraph: tokenizerFactory.tokensForParagraph,
        });
        const breakerModule = createLineBreakerModule({
            createTextMeasurementService: () => serviceWithMeasureText,
            normalizeLineBreakerOptions,
            resolveLineRangesForBreaker,
            lineRangesAreInvalid,
            buildLineBreakerFallback: fallbackFactory.buildLineBreakerFallback,
            tokensForParagraph: tokenizerFactory.tokensForParagraph,
            coalesceNonBreakingTokens,
            normalizeParagraphAlignment,
            createLineDraft,
            materializeLineDraft,
            splitTokenIntoFittingPieces,
            applyJustifyMetadata,
        });
        const breaker = breakerModule.createLineBreaker(serviceWithMeasureText);
        let layoutVersion = 0;

        // createScopedLayoutMetadataDecorator returns the decorate function directly.
        const scopedMetadataDecorator = createScopedLayoutMetadataDecorator({ createBlockIndexContext });

        function decorateScopedLayoutMetadata(layout, context) {
            return scopedMetadataDecorator(layout, context);
        }

        function anchoredLayoutScope() {
            return createAnchoredDrawingLayoutScope();
        }

        function addAnchoredDrawingRunsToLayoutScope(block, targetScope, frame, context, fallbackY, laidOutBlocks) {
            const scope = targetScope || anchoredLayoutScope();
            const scopeFrame = rectFromGeometry(frame || { x: 0, y: 0, width: 640, height: 900 });
            scopeFrame.height = Math.max(1, Number(scopeFrame.height || 0) || 900);
            const ctx = context || {};
            const pageIndexValue = Number(ctx.pageIndex ?? 0) || 0;
            collectAnchoredDrawingRuns(block, ctx).forEach(function (entry) {
                const objectId = (entry.object && entry.object.objectId) || (entry.run && (entry.run.objectId || entry.run.id)) || '';
                if (!objectId || scope.anchoredIds.has(objectId)) return;
                const usesPageScope = ctx.region === 'TableCell'
                    && readObjectLayoutInCell(entry.object) === false
                    && !!ctx.pageFrame;
                const placementFrame = usesPageScope ? rectFromGeometry(ctx.pageFrame) : scopeFrame;
                let reference = resolveAnchoredDrawingReference(entry.object, laidOutBlocks || [], [], {
                    blockId: (block && block.id) || '',
                    pageIndex: pageIndexValue,
                    bodyFrame: placementFrame,
                    y: fallbackY,
                    region: ctx.region,
                    headerFooterId: ctx.headerFooterId || null,
                    tableId: ctx.tableId || null,
                    cellId: ctx.cellId || null,
                    columnIndex: ctx.columnIndex,
                });
                reference = Object.assign({}, reference, {
                    region: ctx.region || reference.region || 'Body',
                    headerFooterId: ctx.headerFooterId || reference.headerFooterId || null,
                    tableId: ctx.tableId || reference.tableId || null,
                    cellId: ctx.cellId || reference.cellId || null,
                    columnIndex: ctx.columnIndex ?? reference.columnIndex ?? null,
                });
                const placed = createAnchoredDrawingLayoutObject(block, entry, reference, {
                    pageIndex: pageIndexValue,
                    rect: placementFrame,
                    bodyFrame: placementFrame,
                });
                placed.region = ctx.region || placed.region || 'Body';
                placed.headerFooterId = ctx.headerFooterId || placed.headerFooterId || null;
                placed.tableId = ctx.tableId || placed.tableId || null;
                placed.cellId = ctx.cellId || placed.cellId || null;
                placed.columnIndex = normalizeTextExclusionColumnIndex(ctx.columnIndex ?? placed.columnIndex);
                placed.anchorRegion = placed.anchorRegion || placed.region;
                placed.anchorHeaderFooterId = placed.anchorHeaderFooterId || placed.headerFooterId || '';
                placed.anchorTableId = placed.anchorTableId || placed.tableId || '';
                placed.anchorCellId = placed.anchorCellId || placed.cellId || '';
                placed.anchorColumnIndex = normalizeTextExclusionColumnIndex(placed.anchorColumnIndex ?? placed.columnIndex);
                placed.layoutInCell = readObjectLayoutInCell(placed);
                resolveObjectOverlapGeometry(scope.objects, placed, placementFrame);
                scope.objects.push(sortObject(placed));
                scope.anchoredIds.add(objectId);
                const exclusion = createTextExclusion(placed, placementFrame);
                if (exclusion) scope.exclusions.push(exclusion);
            });
            return scope;
        }

        function layoutParagraph(block, paragraphOptions) {
            const popts = normalizeParagraphLayoutOptions(Object.assign({}, defaults, paragraphOptions || {}));
            if (!block || block.type !== 'paragraph') {
                return layoutObjectBlock(block, popts, ++layoutVersion);
            }

            const paragraphInput = {
                id: block.id,
                runs: asArray(block.content && block.content.runs),
                style: Object.assign({}, block.style || {}, (block.content && block.content.style) || {}),
                // Pass through unset (undefined) so the line-breaker can apply its default —
                // RTL paragraphs default to right-aligned (R.5.16); LTR fall back to 'left'.
                alignment: (block.content && (block.content.alignment ?? block.content.Alignment)) ?? undefined,
            };
            const lineLayout = breaker.breakParagraph(paragraphInput, {
                x: popts.x,
                y: popts.y,
                width: popts.width,
                minReadableWidth: popts.minReadableWidth,
                lineGap: popts.lineGap,
                availableIntervals: popts.availableIntervals,
                resolveAvailableIntervals: popts.resolveAvailableIntervals,
                hyphenation: popts.hyphenation,
            });
            const runs = flattenParagraphRuns(paragraphInput, normalizeImageObject);
            const lines = asArray(lineLayout.lines).map(function (line, index) {
                const id = block.id + '-line-' + index;
                const rect = clone(line.rect || {});
                const baseline = rect.y + Math.max(1, rect.height || 1) * 0.78;
                const firstLineInterval = asArray(line.availableIntervals)[0] || null;
                const pageIndex = line.pageIndex ?? (firstLineInterval && firstLineInterval.pageIndex) ?? null;
                return Object.assign({}, line, {
                    id,
                    blockId: block.id,
                    lineId: id,
                    pageIndex,
                    index,
                    rect,
                    baseline,
                    baselineOffset: baseline - rect.y,
                    availableIntervals: asArray(line.availableIntervals).map(function (interval) {
                        return Object.assign({}, interval, { blockId: block.id, lineId: id, pageIndex: interval.pageIndex ?? pageIndex });
                    }),
                    ranges: asArray(line.ranges).map(function (range, rangeIndex) {
                        return Object.assign({}, range, { blockId: block.id, lineId: id, pageIndex: range.pageIndex ?? pageIndex, index: range.index ?? rangeIndex });
                    }),
                });
            });
            const lineByOriginalId = {};
            asArray(lineLayout.lines).forEach(function (line, index) {
                lineByOriginalId[line.id] = lines[index].id;
            });
            const segments = asArray(lineLayout.segments).map(function (segment, index) {
                const line = lines.find(function (candidate) {
                    return segment.rect && candidate.rect
                        && Math.abs(candidate.rect.y - segment.rect.y) < 0.5
                        && segment.start >= candidate.start
                        && segment.end <= candidate.end;
                }) || lines[0];
                const run = runs.find(function (item) { return item.id === segment.runId; }) || runForOffset(runs, segment.start);
                return Object.assign({}, segment, {
                    id: block.id + '-segment-' + index,
                    blockId: block.id,
                    lineId: (line && line.id) || null,
                    runId: (run && run.id) || segment.runId || null,
                    kind: (run && run.kind) || 'text',
                    objectId: segment.objectId || (run && run.objectId) || null,
                    inlineObject: segment.inlineObject === true || segment.type === 'inlineObject',
                    objectRect: segment.objectRect ? clone(segment.objectRect) : null,
                    object: segment.object ? clone(segment.object) : (run && run.object ? clone(run.object) : null),
                    math: segment.math ? clone(segment.math) : (run && run.math ? clone(run.math) : null),
                    signingField: segment.signingField ? clone(segment.signingField) : (run && run.signingField ? clone(run.signingField) : null),
                    style: normalizeLayoutSegmentStyle((run && run.style) || segment.style || {}),
                    decorations: decorationsFromMarks((run && run.marks) || []),
                    marks: asArray(run && run.marks),
                    mapping: { blockId: block.id, runId: (run && run.id) || null, start: segment.start, end: segment.end },
                });
            });
            const segmentsByLine = new Map();
            segments.forEach(function (segment) {
                if (!segmentsByLine.has(segment.lineId)) segmentsByLine.set(segment.lineId, []);
                segmentsByLine.get(segment.lineId).push(segment);
            });
            lines.forEach(function (line) {
                line.segments = segmentsByLine.get(line.id) || [];
                line.ranges = asArray(line.ranges && line.ranges.length ? line.ranges : line.availableIntervals).map(function (range, rangeIndex) {
                    const indexValue = Number(range.index ?? rangeIndex);
                    const rangeSegments = line.segments.filter(function (s) { return Number(s.rangeIndex || 0) === indexValue; });
                    const start = rangeSegments.length
                        ? rangeSegments.reduce(function (v, s) { return Math.min(v, Number(s.start || 0) || 0); }, Number.MAX_SAFE_INTEGER)
                        : Number(range.start ?? line.end ?? 0) || 0;
                    const end = rangeSegments.length
                        ? rangeSegments.reduce(function (v, s) { return Math.max(v, Number(s.end || 0) || 0); }, start)
                        : Number(range.end ?? start) || start;
                    return Object.assign({}, range, {
                        blockId: block.id, lineId: line.id,
                        pageIndex: range.pageIndex ?? line.pageIndex ?? null,
                        index: indexValue, start, end: Math.max(start, end),
                        empty: rangeSegments.length === 0,
                        collapsedOffset: rangeSegments.length === 0 ? start : null,
                        segments: rangeSegments,
                    });
                });
                line.textRanges = line.ranges;
                line.availableIntervals = asArray(line.availableIntervals).map(function (interval, intervalIndex) {
                    const range = line.ranges[intervalIndex] || null;
                    return Object.assign({}, interval, {
                        blockId: block.id, lineId: line.id,
                        pageIndex: interval.pageIndex ?? line.pageIndex ?? null,
                        start: range ? range.start : interval.start,
                        end: range ? range.end : interval.end,
                        collapsedOffset: range ? range.collapsedOffset : interval.collapsedOffset,
                        empty: range ? range.empty : interval.empty,
                    });
                });
                line.inlineObjects = line.segments.filter(function (s) {
                    return s.inlineObject === true || s.kind === 'drawing';
                }).map(function (s) { return createInlineObjectLayoutFromSegment(block, s, line); });
            });
            const caretStops = asArray(lineLayout.caretStops).map(function (stop) {
                const inline = inlineAtOffset(block, stop.offset);
                return Object.assign({}, stop, {
                    blockId: block.id,
                    inlineId: (inline && inline.run) ? inline.run.id : null,
                    lineId: lineByOriginalId[stop.lineId] || stop.lineId,
                    affinity: stop.affinity || (Number(stop.offset || 0) === 0 ? 'before' : 'after'),
                });
            });
            const baselines = lines.map(function (line) {
                return { blockId: block.id, lineId: line.id, y: line.baseline, offset: line.baselineOffset };
            });
            let inlineObjects = [];
            lines.forEach(function (line) { inlineObjects = inlineObjects.concat(asArray(line.inlineObjects)); });
            const rect = paragraphRectFromLines(popts, lines);
            return sortObject({
                ok: lineLayout.ok !== false,
                id: 'layout-' + block.id,
                layoutVersion: ++layoutVersion,
                blockId: block.id,
                type: 'paragraph',
                scope: createLayoutScope(LayoutScopeKinds.ActiveParagraph, { blockId: block.id, affectedScopeIds: [block.id], reason: 'layoutParagraph' }),
                rect, lines, segments, inlineObjects, caretStops, baselines,
                fallback: lineLayout.fallback === true,
                debug: { source: 'paragraph-layout-tree', lineBreaker: lineLayout.debug || {}, invalidatedScopes: [block.id] },
            });
        }

        function layoutParagraphInScopedFrame(block, frame, y, layoutOptions, metrics, exclusions, context) {
            const ctx = context || {};
            const scopeFrame = rectFromGeometry(frame || { x: 0, y, width: 640, height: 900 });
            scopeFrame.height = Math.max(1, Number(scopeFrame.height || 0) || 900);
            const layoutMetrics = metrics || {};
            const minReadableWidth = Math.max(1, Number(layoutMetrics.minReadableWidth ?? ((layoutOptions && layoutOptions.minReadableWidth) ?? 48)) || 48);
            const lineGap = Number(layoutMetrics.lineGap ?? ((layoutOptions && layoutOptions.lineGap) ?? 0)) || 0;
            const pageIndexValue = Number(ctx.pageIndex ?? ((context && context.pageIndex) ?? 0)) || 0;
            const initial = layoutParagraph(block, Object.assign({}, layoutOptions || {}, {
                page: scopeFrame, x: scopeFrame.x, y, width: scopeFrame.width, lineGap, minReadableWidth,
                resolveAvailableIntervals: function (atY, lineHeight, requestedMinWidth) {
                    return getAvailableIntervals(atY, lineHeight, scopeFrame, exclusions || [], requestedMinWidth || minReadableWidth, Object.assign({}, ctx, { pageIndex: pageIndexValue }));
                },
            }));
            let scoped = {
                ok: initial.ok !== false,
                id: 'layout-' + block.id + '-scope-' + (ctx.cellId || ctx.headerFooterId || ctx.region || 'body') + '-' + pageIndexValue,
                layoutVersion: ++layoutVersion, blockId: block.id, type: 'paragraph',
                pageIndex: pageIndexValue,
                rect: { x: scopeFrame.x, y, width: scopeFrame.width, height: 0 },
                lines: [], segments: [], inlineObjects: [], caretStops: [], baselines: [],
                scope: createLayoutScope(LayoutScopeKinds.PageRegion, Object.assign({}, ctx, { blockId: block.id, pageIndex: pageIndexValue, affectedScopeIds: [block.id], reason: 'layoutParagraphRegion' })),
                fallback: initial.fallback === true,
                debug: Object.assign({}, initial.debug || {}, { source: 'paragraph-layout-scoped-region', invalidatedScopes: [block.id] }),
            };
            asArray(initial.lines).forEach(function (line) {
                const shiftedLine = shiftLayoutLine(line, 0, pageIndexValue);
                const segmentIds = new Set(asArray(line.segments).map(function (s) { return s.id; }));
                const shiftedSegments = asArray(initial.segments).filter(function (s) { return s.lineId === line.id || segmentIds.has(s.id); }).map(function (s) { return shiftLayoutSegment(s, 0, pageIndexValue); });
                const shiftedStops = asArray(initial.caretStops).filter(function (stop) { return stop.lineId === line.id; }).map(function (stop) { return shiftCaretStop(stop, 0, pageIndexValue); });
                const shiftedInlineObjects = shiftedSegments.filter(function (s) { return s.inlineObject === true || s.kind === 'drawing'; }).map(function (s) { return createInlineObjectLayoutFromSegment(block, s, shiftedLine); });
                shiftedLine.segments = shiftedSegments;
                shiftedLine.ranges = asArray(shiftedLine.ranges).map(function (range, rangeIndex) {
                    const indexValue = Number(range.index ?? rangeIndex);
                    return sortObject(Object.assign({}, range, { pageIndex: pageIndexValue, segments: shiftedSegments.filter(function (s) { return Number(s.rangeIndex || 0) === indexValue; }) }));
                });
                shiftedLine.textRanges = shiftedLine.ranges;
                shiftedLine.inlineObjects = shiftedInlineObjects;
                decorateScopedLayoutMetadata({ lines: [shiftedLine], segments: shiftedSegments, inlineObjects: shiftedInlineObjects, caretStops: shiftedStops }, Object.assign({}, ctx, { pageIndex: pageIndexValue }));
                scoped.lines.push(shiftedLine);
                scoped.segments = scoped.segments.concat(shiftedSegments);
                scoped.inlineObjects = scoped.inlineObjects.concat(shiftedInlineObjects);
                scoped.caretStops = scoped.caretStops.concat(shiftedStops);
                scoped.baselines.push(sortObject({ blockId: block.id, lineId: shiftedLine.id, y: shiftedLine.baseline, offset: shiftedLine.baselineOffset, pageIndex: pageIndexValue, region: ctx.region, headerFooterId: ctx.headerFooterId || null, tableId: ctx.tableId || null, cellId: ctx.cellId || null, columnIndex: ctx.columnIndex ?? null }));
                scoped.rect.y = Math.min(scoped.rect.y, shiftedLine.rect.y);
                scoped.rect.height = Math.max(scoped.rect.height, shiftedLine.rect.y + shiftedLine.rect.height - scoped.rect.y);
            });
            if (!scoped.lines.length) {
                scoped = Object.assign(scoped, decorateScopedLayoutMetadata(initial, Object.assign({}, ctx, { pageIndex: pageIndexValue })));
            }
            scoped.rect.height = Math.max(1, scoped.rect.height || paragraphRectFromLines({ x: scopeFrame.x, y, width: scopeFrame.width }, scoped.lines).height);
            return sortObject(decorateScopedLayoutMetadata(scoped, Object.assign({}, ctx, { pageIndex: pageIndexValue })));
        }

        function layoutTableBlock(block, tableOptions) {
            const topts = normalizeParagraphLayoutOptions(Object.assign({}, defaults, tableOptions || {}));
            const rows = asArray(block && block.content && block.content.rows);
            const colCount = tableColumnCount(block);
            const tableStyle = (block.content && block.content.style) || {};
            const tableWidth = Math.min(Math.max(80, Number(tableStyle.width || tableStyle.Width || topts.width) || topts.width), topts.width);
            const x = topts.x;
            let y = topts.y;
            const defaultColumnWidth = tableWidth / colCount;
            const columnWidths = new Array(colCount).fill(defaultColumnWidth);
            rows.forEach(function (row) {
                let column = 0;
                asArray(row.cells).forEach(function (cell) {
                    const span = Math.max(1, Number(cell.colSpan || 1));
                    const requested = Number(cell.width || cell.Width || 0);
                    if (requested > 0 && span === 1) columnWidths[column] = requested;
                    column += span;
                });
            });
            const totalRequested = columnWidths.reduce(function (s, v) { return s + v; }, 0) || tableWidth;
            if (Math.abs(totalRequested - tableWidth) > 0.5) {
                for (let i = 0; i < columnWidths.length; i++) columnWidths[i] = columnWidths[i] * tableWidth / totalRequested;
            }
            const cells = []; const rowLayouts = []; const tableLines = []; const tableSegments = []; const tableCarets = []; const tableObjects = []; const tableExclusions = [];
            const tablePageIndex = Number(topts.pageIndex ?? topts.PageIndex ?? ((topts.page && topts.page.pageIndex) ?? 0)) || 0;
            let rowY = y;
            // R.5.9 — rowSpan carry: columns covered by a vertical merge from a previous row, so
            // the next row's cells skip those grid positions (keeps columns aligned).
            const rowSpanCarry = {};
            const rowSpanCells = []; // { cell, fromRowIndex, rowSpan } — height extended in a post-pass
            rows.forEach(function (row, rowIndex) {
                let columnIndex = 0;
                const rowCells = [];
                let rowHeight = Math.max(24, Number(row.height || row.Height || 0) || 0);
                asArray(row.cells).forEach(function (cell) {
                    const colSpan = Math.max(1, Number(cell.colSpan || 1));
                    const rowSpan = Math.max(1, Number(cell.rowSpan || 1));
                    while ((rowSpanCarry[columnIndex] != null ? rowSpanCarry[columnIndex] : -1) >= rowIndex) columnIndex += 1; // skip columns a rowSpan covers
                    const cellX = x + columnWidths.slice(0, columnIndex).reduce(function (s, v) { return s + v; }, 0);
                    const cellWidth = columnWidths.slice(columnIndex, columnIndex + colSpan).reduce(function (s, v) { return s + v; }, 0);
                    const padding = Number((cell.style ? (cell.style.padding ?? cell.style.Padding) : null) ?? 6) || 0;
                    const contentFrame = { x: cellX + padding, y: rowY + padding, width: Math.max(12, cellWidth - padding * 2), height: 0 };
                    const blockLayouts = [];
                    let contentY = contentFrame.y;
                    const cellScope = anchoredLayoutScope();
                    const cellContext = {
                        region: 'TableCell', tableId: block.id, cellId: cell.id, columnIndex,
                        headerFooterId: topts.headerFooterId || topts.HeaderFooterId || null,
                        pageIndex: tablePageIndex, pageFrame: topts.page || null,
                    };
                    const scopedFrame = { x: contentFrame.x, y: contentFrame.y, width: contentFrame.width, height: Math.max(Number(cell.height || cell.Height || 0) || 0, Number((topts.page && topts.page.height) || 0) || 900) };
                    asArray(cell.blocks).forEach(function (childBlock) {
                        if (childBlock && childBlock.type === 'paragraph') {
                            addAnchoredDrawingRunsToLayoutScope(childBlock, cellScope, scopedFrame, cellContext, contentY, blockLayouts);
                        }
                        const childLayout = childBlock && childBlock.type === 'paragraph'
                            ? layoutParagraphInScopedFrame(childBlock, scopedFrame, contentY, Object.assign({}, topts, { x: contentFrame.x, y: contentY, width: contentFrame.width, lineGap: 0, pageIndex: tablePageIndex }), { lineGap: 0, minReadableWidth: topts.minReadableWidth || 48 }, cellScope.exclusions, cellContext)
                            : layoutObjectBlock(childBlock, { page: topts.page, x: contentFrame.x, y: contentY, width: contentFrame.width }, ++layoutVersion);
                        childLayout.tableId = block.id; childLayout.cellId = cell.id; childLayout.rowIndex = rowIndex; childLayout.columnIndex = columnIndex; childLayout.region = 'TableCell'; childLayout.headerFooterId = cellContext.headerFooterId || null;
                        asArray(childLayout.lines).forEach(function (line) { line.tableId = block.id; line.cellId = cell.id; line.columnIndex = columnIndex; line.region = 'TableCell'; line.headerFooterId = cellContext.headerFooterId || null; tableLines.push(line); });
                        asArray(childLayout.segments).forEach(function (seg) { seg.tableId = block.id; seg.cellId = cell.id; seg.columnIndex = columnIndex; seg.region = 'TableCell'; seg.headerFooterId = cellContext.headerFooterId || null; tableSegments.push(seg); });
                        asArray(childLayout.caretStops).forEach(function (stop) { stop.tableId = block.id; stop.cellId = cell.id; stop.columnIndex = columnIndex; stop.region = 'TableCell'; stop.headerFooterId = cellContext.headerFooterId || null; tableCarets.push(stop); });
                        blockLayouts.push(childLayout);
                        contentY = childLayout.rect.y + childLayout.rect.height + 2;
                    });
                    const contentHeight = Math.max(18, contentY - contentFrame.y);
                    const cellHeight = Math.max(Number(cell.height || cell.Height || 0) || 0, contentHeight + padding * 2, 28);
                    rowHeight = Math.max(rowHeight, cellHeight);
                    const localCellExclusions = cellScope.exclusions.filter(function (exclusion) { return textExclusionMatchesScope(exclusion, createTextExclusionScopeDescriptor(cellContext)); });
                    const cellLayout = { tableId: block.id, rowId: row.id, cellId: cell.id, rowIndex, columnIndex, rowSpan, colSpan, rect: { x: cellX, y: rowY, width: cellWidth, height: cellHeight }, contentFrame: { x: contentFrame.x, y: contentFrame.y, width: contentFrame.width, height: Math.max(1, cellHeight - padding * 2) }, style: clone(cell.style || {}), objects: cellScope.objects.slice(), exclusions: localCellExclusions, blockLayouts };
                    tableObjects.push(...cellScope.objects);
                    tableExclusions.push(...cellScope.exclusions);
                    rowCells.push(cellLayout); cells.push(cellLayout);
                    if (rowSpan > 1) {
                        for (let cc = columnIndex; cc < columnIndex + colSpan; cc++) rowSpanCarry[cc] = rowIndex + rowSpan - 1; // covered through this row
                        rowSpanCells.push({ cell: cellLayout, rowIndex: rowIndex, rowSpan: rowSpan });
                    }
                    columnIndex += colSpan;
                });
                rowCells.forEach(function (cell) {
                    cell.rect.height = rowHeight;
                    cell.contentFrame.height = Math.max(1, rowHeight - 2 * (Number((cell.style ? (cell.style.padding ?? cell.style.Padding) : null) ?? 6) || 0));
                });
                rowLayouts.push({ rowId: row.id, rowIndex, y: rowY, height: rowHeight, cells: rowCells.map(function (c) { return c.cellId; }) });
                rowY += rowHeight;
            });
            // R.5.9 — extend rowSpan>1 cells to span the merged rows' combined height.
            rowSpanCells.forEach(function (entry) {
                let h = 0;
                for (let ri = entry.rowIndex; ri < entry.rowIndex + entry.rowSpan && ri < rowLayouts.length; ri++) h += Number(rowLayouts[ri].height) || 0;
                if (h > 0) { entry.cell.rect.height = h; entry.cell.contentFrame.height = Math.max(1, h - 2 * (Number((entry.cell.style ? (entry.cell.style.padding ?? entry.cell.style.Padding) : null) ?? 6) || 0)); }
            });
            const rect = { x, y: topts.y, width: tableWidth, height: Math.max(28, rowY - topts.y) };
            return sortObject({ ok: true, id: 'layout-' + block.id, layoutVersion: ++layoutVersion, blockId: block.id, type: 'table', pageIndex: tablePageIndex, rect, rows: rowLayouts, columns: columnWidths.map(function (width, index) { return { index, x: x + columnWidths.slice(0, index).reduce(function (s, v) { return s + v; }, 0), width }; }), cells, lines: tableLines, segments: tableSegments, caretStops: tableCarets, objects: tableObjects, exclusions: tableExclusions, scope: createLayoutScope(LayoutScopeKinds.PageRegion, { blockId: block.id, affectedScopeIds: [block.id], reason: 'layoutTable' }), fallback: false, debug: { source: 'table-layout-tree', invalidatedScopes: [block.id], textInsideCells: true } });
        }

        function layoutDocument(model, docOptions) {
            const dopts = Object.assign({}, defaults, docOptions || {});
            const pageMetrics = normalizePageLayoutSettings(dopts, model);

            // R.5.23d — multi-section page geometry. Each `model.sections` entry marks a "Next
            // Page" section break that starts on a fresh page with its own page settings
            // (size / orientation / margins). Gated: no sections ⇒ the original single-metrics
            // path runs unchanged (mixed-height stacking only kicks in when sections exist).
            const sectionDefs = asArray(model && (model.sections || model.Sections)).map(function (s) {
                return {
                    startBlockId: asText((s && (s.startBlockId || s.StartBlockId || s.fromBlockId || s.FromBlockId)) || ''),
                    metrics: normalizePageLayoutSettings(Object.assign({}, dopts, (s && (s.pageSettings || s.PageSettings)) || {}), model),
                };
            }).filter(function (s) { return s.startBlockId; });
            const hasSections = sectionDefs.length > 0;
            const sectionStartByBlock = {};
            sectionDefs.forEach(function (s, i) { sectionStartByBlock[s.startBlockId] = i; });
            let activeMetrics = pageMetrics;

            const placePageAtY = function (page, y) {
                const dy = y - page.rect.y;
                if (!dy) return page;
                page.rect.y += dy; page.marginBox.y += dy;
                page.headerFrame.y += dy; page.bodyFrame.y += dy; page.footerFrame.y += dy;
                return page;
            };
            // Create the page at `index`. With sections active, pages use the currently-active
            // section metrics and stack by ACTUAL prior-page heights (mixed sizes are safe).
            const makePage = function (index) {
                if (!hasSections) return createPageLayout(index, pageMetrics);
                const page = createPageLayout(index, activeMetrics);
                const prev = pages[index - 1];
                const desiredY = prev ? (prev.rect.y + prev.rect.height + activeMetrics.pageGap) : activeMetrics.pageOrigin.y;
                return placePageAtY(page, desiredY);
            };

            const pages = [];
            pages.push(makePage(0)); // `pages` must exist first (makePage reads pages[index-1])
            let currentPageIndex = 0;
            let currentY = pages[0].bodyFrame.y;
            const blockLayouts = []; const objects = []; let caretStops = []; const headerFooterRegions = [];
            const bodyBottom = function (page) { return page.bodyFrame.y + page.bodyFrame.height; };
            const currentPage = function () { return pages[currentPageIndex]; };
            const ensurePage = function (index) { while (pages.length <= index) pages.push(makePage(pages.length)); return pages[index]; };
            const moveToNextPage = function () { currentPageIndex++; currentY = ensurePage(currentPageIndex).bodyFrame.y; };
            const blockGap = Number(dopts.blockGap ?? dopts.BlockGap ?? pageMetrics.blockGap) || 0;
            const anchoredDrawingObjectIds = new Set();

            const addBlockToPage = function (layout) {
                const page = ensurePage(layout.pageIndex || 0);
                if (page.blockIds.indexOf(layout.blockId) < 0) page.blockIds.push(layout.blockId);
                blockLayouts.push(layout);
                caretStops = caretStops.concat(asArray(layout.caretStops));
                asArray(layout.inlineObjects).forEach(function (object) { const c = clone(object); c.pageIndex = layout.pageIndex || 0; c.layer = 'text'; objects.push(c); });
                asArray(layout.objects).forEach(function (object) { const c = clone(object); c.pageIndex = c.pageIndex ?? layout.pageIndex ?? 0; objects.push(c); });
                asArray(layout.exclusions).forEach(function (exclusion) {
                    const scoped = exclusion || {};
                    if (normalizeAnchorRegionName(scoped.region || scoped.Region || 'Body') !== 'Body') return;
                    const pageIndexValue = Number(scoped.pageIndex ?? scoped.PageIndex ?? layout.pageIndex ?? 0) || 0;
                    const targetPage = ensurePage(pageIndexValue);
                    const scopeKey = asText(scoped.scopeKey || scoped.ScopeKey || '');
                    const objectId = asText(scoped.objectId || scoped.ObjectId || '');
                    const exists = targetPage.exclusions.some(function (existing) { return scopeKey && asText((existing && (existing.scopeKey || existing.ScopeKey)) || '') === scopeKey && objectId && asText((existing && (existing.objectId || existing.ObjectId)) || '') === objectId; });
                    if (!exists) targetPage.exclusions.push(clone(scoped));
                });
            };

            const addAnchoredDrawingRunsForBlock = function (block, fragments, fallbackY) {
                collectAnchoredDrawingRuns(block, { region: 'Body', pageIndex: currentPageIndex }).forEach(function (entry) {
                    const objectId = (entry.object && entry.object.objectId) || (entry.run && (entry.run.objectId || entry.run.id)) || '';
                    if (!objectId || anchoredDrawingObjectIds.has(objectId)) return;
                    const fallbackPage = currentPage();
                    const reference = resolveAnchoredDrawingReference(entry.object, blockLayouts, fragments || [], { blockId: (block && block.id) || '', pageIndex: fallbackPage.pageIndex, bodyFrame: fallbackPage.bodyFrame, y: fallbackY });
                    const page = ensurePage(reference.pageIndex || fallbackPage.pageIndex || 0);
                    const placed = createAnchoredDrawingLayoutObject(block, entry, reference, page);
                    resolveObjectOverlapGeometry(objects.filter(function (item) { return Number((item && item.pageIndex) || 0) === Number(placed.pageIndex || 0); }), placed, page.bodyFrame);
                    objects.push(clone(placed));
                    anchoredDrawingObjectIds.add(objectId);
                    if (page.blockIds.indexOf(block.id) < 0) page.blockIds.push(block.id);
                    const exclusion = createTextExclusion(placed, page.bodyFrame);
                    if (exclusion) page.exclusions.push(exclusion);
                });
            };

            function layoutParagraphAcrossPages(block, page, y, layoutOptions, metrics) {
                function resolvePagedAvailableIntervals(atY, lineHeight, requestedMinWidth) {
                    let lineY = Number(atY || y) || y;
                    const height = Math.max(1, Number(lineHeight || 18) || 18);
                    const minWidth = requestedMinWidth || metrics.minReadableWidth;
                    let activePage = ensurePage(page.pageIndex);
                    while (lineY > activePage.bodyFrame.y && lineY + height > bodyBottom(activePage)) { activePage = ensurePage(activePage.pageIndex + 1); if (lineY < activePage.bodyFrame.y) lineY = activePage.bodyFrame.y; }
                    if (lineY < activePage.bodyFrame.y) lineY = activePage.bodyFrame.y;
                    let available = getAvailableIntervals(lineY, height, activePage.bodyFrame, activePage.exclusions, minWidth, { pageIndex: activePage.pageIndex, region: 'Body' });
                    if (available.movedToY > lineY + 0.01) {
                        lineY = available.movedToY;
                        if (lineY + height > bodyBottom(activePage)) { activePage = ensurePage(activePage.pageIndex + 1); lineY = activePage.bodyFrame.y; available = getAvailableIntervals(lineY, height, activePage.bodyFrame, activePage.exclusions, minWidth, { pageIndex: activePage.pageIndex, region: 'Body' }); }
                    }
                    const intervals = asArray(available.intervals).map(function (i) { return Object.assign({}, i, { pageIndex: activePage.pageIndex }); });
                    const movedIntervals = asArray(available.movedIntervals).map(function (i) { return Object.assign({}, i, { pageIndex: activePage.pageIndex }); });
                    return Object.assign({}, available, { intervals, availableIntervals: intervals, movedIntervals, movedToY: lineY, pageIndex: activePage.pageIndex });
                }
                const initial = layoutParagraph(block, Object.assign({}, layoutOptions, { page: page.bodyFrame, x: page.bodyFrame.x, y, width: page.bodyFrame.width, lineGap: metrics.lineGap, minReadableWidth: metrics.minReadableWidth, resolveAvailableIntervals: resolvePagedAvailableIntervals }));
                const fragmentsByPage = new Map();
                function linePageIndex(line) {
                    const interval = asArray(line && line.availableIntervals)[0] || null;
                    if (interval && interval.pageIndex !== null && interval.pageIndex !== undefined) return Number(interval.pageIndex || 0) || 0;
                    if (line && line.pageIndex !== null && line.pageIndex !== undefined) return Number(line.pageIndex || 0) || 0;
                    const lineY = Number((line && line.rect && line.rect.y) || y) || y;
                    let index = page.pageIndex; let candidate = ensurePage(index);
                    while (lineY > candidate.bodyFrame.y && lineY >= bodyBottom(candidate)) { index++; candidate = ensurePage(index); }
                    return candidate.pageIndex;
                }
                function getFragment(fragmentPage, lineY) {
                    if (!fragmentsByPage.has(fragmentPage.pageIndex)) {
                        fragmentsByPage.set(fragmentPage.pageIndex, { ok: true, id: 'layout-' + block.id + '-page-' + fragmentPage.pageIndex, layoutVersion: ++layoutVersion, blockId: block.id, type: 'paragraph', pageIndex: fragmentPage.pageIndex, fragmentIndex: fragmentsByPage.size, rect: { x: fragmentPage.bodyFrame.x, y: lineY, width: fragmentPage.bodyFrame.width, height: 0 }, lines: [], segments: [], inlineObjects: [], caretStops: [], baselines: [], scope: createLayoutScope(LayoutScopeKinds.PageRegion, { blockId: block.id, pageIndex: fragmentPage.pageIndex, affectedScopeIds: [block.id], reason: 'layoutParagraphPage' }), fallback: initial.fallback === true, debug: { source: 'paragraph-layout-fragment', invalidatedScopes: [block.id] } });
                    }
                    return fragmentsByPage.get(fragmentPage.pageIndex);
                }
                asArray(initial.lines).forEach(function (line) {
                    const activePage = ensurePage(linePageIndex(line));
                    const fragment = getFragment(activePage, Number((line.rect && line.rect.y) || activePage.bodyFrame.y) || activePage.bodyFrame.y);
                    const shiftedLine = shiftLayoutLine(line, 0, activePage.pageIndex);
                    const segmentIds = new Set(asArray(line.segments).map(function (s) { return s.id; }));
                    const shiftedSegments = asArray(initial.segments).filter(function (s) { return s.lineId === line.id || segmentIds.has(s.id); }).map(function (s) { return shiftLayoutSegment(s, 0, activePage.pageIndex); });
                    const shiftedStops = asArray(initial.caretStops).filter(function (stop) { return stop.lineId === line.id; }).map(function (stop) { return shiftCaretStop(stop, 0, activePage.pageIndex); });
                    const shiftedInlineObjects = shiftedSegments.filter(function (s) { return s.inlineObject === true || s.kind === 'drawing'; }).map(function (s) { return createInlineObjectLayoutFromSegment(block, s, shiftedLine); });
                    shiftedLine.segments = shiftedSegments;
                    shiftedLine.ranges = asArray(shiftedLine.ranges).map(function (range, rangeIndex) { const iv = Number(range.index ?? rangeIndex); return sortObject(Object.assign({}, range, { pageIndex: activePage.pageIndex, segments: shiftedSegments.filter(function (s) { return Number(s.rangeIndex || 0) === iv; }) })); });
                    shiftedLine.textRanges = shiftedLine.ranges;
                    shiftedLine.inlineObjects = shiftedInlineObjects;
                    fragment.lines.push(shiftedLine);
                    fragment.segments = fragment.segments.concat(shiftedSegments);
                    fragment.inlineObjects = fragment.inlineObjects.concat(shiftedInlineObjects);
                    fragment.caretStops = fragment.caretStops.concat(shiftedStops);
                    fragment.baselines.push(sortObject({ blockId: block.id, lineId: shiftedLine.id, y: shiftedLine.baseline, offset: shiftedLine.baselineOffset, pageIndex: activePage.pageIndex }));
                    fragment.rect.y = Math.min(fragment.rect.y, shiftedLine.rect.y);
                    fragment.rect.height = Math.max(fragment.rect.height, shiftedLine.rect.y + shiftedLine.rect.height - fragment.rect.y);
                });
                return Array.from(fragmentsByPage.values()).map(function (fragment) { fragment.rect.height = Math.max(1, fragment.rect.height); return sortObject(fragment); });
            }

            function renderHeaderFooterLayouts(sourceModel, totalPages) {
                const result = [];
                pages.forEach(function (pageItem) {
                    asArray(sourceModel && sourceModel.headers).forEach(function (region) { result.push(layoutHeaderFooterRegion(region, 'Header', pageItem, totalPages)); });
                    asArray(sourceModel && sourceModel.footers).forEach(function (region) { result.push(layoutHeaderFooterRegion(region, 'Footer', pageItem, totalPages)); });
                });
                return result;
            }

            function layoutHeaderFooterRegion(region, regionName, pageItem, totalPages) {
                const frame = regionName === 'Header' ? pageItem.headerFrame : pageItem.footerFrame;
                let yInRegion = frame.y;
                const regionBlockLayouts = []; let regionCaretStops = [];
                const regionScope = anchoredLayoutScope();
                const regionContext = { region: regionName, headerFooterId: (region && region.id) || null, pageIndex: pageItem.pageIndex };
                asArray(region && region.blocks).forEach(function (block) {
                    const resolvedBlock = cloneBlockWithResolvedFields(block, pageItem.pageNumber, totalPages);
                    if (resolvedBlock.type === 'paragraph') addAnchoredDrawingRunsToLayoutScope(resolvedBlock, regionScope, frame, regionContext, yInRegion, regionBlockLayouts);
                    const layout = resolvedBlock.type === 'paragraph'
                        ? layoutParagraphInScopedFrame(resolvedBlock, frame, yInRegion, Object.assign({}, dopts, { page: frame, x: frame.x, y: yInRegion, width: frame.width, pageIndex: pageItem.pageIndex }), pageMetrics, regionScope.exclusions, regionContext)
                        : layoutObjectBlock(resolvedBlock, { page: frame, x: frame.x, y: yInRegion, width: frame.width }, ++layoutVersion);
                    layout.region = regionName; layout.headerFooterId = region.id; layout.pageIndex = pageItem.pageIndex;
                    asArray(layout.lines).forEach(function (line) { line.region = regionName; line.headerFooterId = region.id; line.pageIndex = pageItem.pageIndex; });
                    asArray(layout.segments).forEach(function (seg) { seg.region = regionName; seg.headerFooterId = region.id; seg.pageIndex = pageItem.pageIndex; });
                    asArray(layout.caretStops).forEach(function (stop) { stop.region = regionName; stop.headerFooterId = region.id; stop.pageIndex = pageItem.pageIndex; });
                    regionBlockLayouts.push(layout);
                    regionCaretStops = regionCaretStops.concat(asArray(layout.caretStops));
                    yInRegion = layout.rect.y + layout.rect.height + Math.min(4, blockGap);
                });
                caretStops = caretStops.concat(regionCaretStops);
                return sortObject({ id: region.id + '-page-' + pageItem.pageIndex, headerFooterId: region.id, region: regionName, pageIndex: pageItem.pageIndex, pageNumber: pageItem.pageNumber, totalPages, frame, blocks: regionBlockLayouts, caretStops: regionCaretStops, objects: regionScope.objects, exclusions: regionScope.exclusions });
            }

            // R.5.17 — first-paint budget: lay out at most `maxBlocks` blocks, leaving the rest for
            // a follow-up full pass. Counted at the START so the per-type early-returns stay intact.
            const maxBlocks = Number(dopts.maxBlocks) > 0 ? Number(dopts.maxBlocks) : Infinity;
            let laidOutBlockCount = 0;
            let layoutComplete = true;
            asArray(model && model.body && model.body.blocks).forEach(function (block) {
                if (laidOutBlockCount >= maxBlocks) { layoutComplete = false; return; }
                laidOutBlockCount += 1;
                // R.5.23d — a block that starts a new section forces a fresh page with that
                // section's geometry (skipped for the very first laid-out block).
                if (hasSections && laidOutBlockCount > 1 && Object.prototype.hasOwnProperty.call(sectionStartByBlock, block.id)) {
                    activeMetrics = sectionDefs[sectionStartByBlock[block.id]].metrics;
                    moveToNextPage();
                }
                if (block.type === 'paragraph') {
                    addAnchoredDrawingRunsForBlock(block, [], currentY);
                    const fragments = layoutParagraphAcrossPages(block, currentPage(), currentY, dopts, activeMetrics);
                    fragments.forEach(addBlockToPage);
                    if (fragments.length) { const last = fragments[fragments.length - 1]; currentPageIndex = last.pageIndex || 0; currentY = last.rect.y + last.rect.height + blockGap; }
                    return;
                }
                if (block.type === 'pageBreak') { addBlockToPage(createPageBreakLayout(block, currentPage(), ++layoutVersion)); moveToNextPage(); return; }
                if (block.type === 'table') {
                    let tableLayout = layoutTableBlock(block, { page: currentPage().bodyFrame, pageIndex: currentPageIndex, x: currentPage().bodyFrame.x, y: currentY, width: currentPage().bodyFrame.width, minReadableWidth: pageMetrics.minReadableWidth });
                    if (currentY > currentPage().bodyFrame.y && currentY + tableLayout.rect.height > bodyBottom(currentPage())) { moveToNextPage(); tableLayout = layoutTableBlock(block, { page: currentPage().bodyFrame, pageIndex: currentPageIndex, x: currentPage().bodyFrame.x, y: currentY, width: currentPage().bodyFrame.width, minReadableWidth: pageMetrics.minReadableWidth }); }
                    tableLayout.pageIndex = currentPageIndex;
                    asArray(tableLayout.caretStops).forEach(function (stop) { stop.pageIndex = currentPageIndex; });
                    addBlockToPage(tableLayout);
                    currentY = tableLayout.rect.y + tableLayout.rect.height + blockGap;
                    return;
                }
                const isImage = block && block.type === 'image';
                const anchoredObject = isImage ? normalizeImageObject(block, { anchorBlockId: blockLayouts.length ? blockLayouts[blockLayouts.length - 1].blockId : '' }) : null;
                const consumesFlow = !anchoredObject || anchoredObject.wrapMode === 'Inline' || anchoredObject.wrapMode === 'TopBottom';
                const objectHeight = isImage ? (anchoredObject.height + (anchoredObject.caption ? Math.max(16, Math.min(48, anchoredObject.caption.length * 0.6)) : 0)) : 80;
                if (currentY > currentPage().bodyFrame.y && currentY + objectHeight > bodyBottom(currentPage())) moveToNextPage();
                const layout = layoutObjectBlock(block, { page: currentPage().bodyFrame, x: isImage ? currentPage().bodyFrame.x + Number((anchoredObject.horizontalPosition && anchoredObject.horizontalPosition.offset) || 0) : currentPage().bodyFrame.x, y: isImage ? currentY + Number((anchoredObject.verticalPosition && anchoredObject.verticalPosition.offset) || 0) : currentY, width: currentPage().bodyFrame.width }, ++layoutVersion);
                layout.pageIndex = currentPageIndex;
                if (isImage) {
                    layout.rect.width = anchoredObject.width; layout.rect.height = objectHeight; layout.object = clone(anchoredObject); layout.objectId = anchoredObject.objectId; layout.wrapMode = anchoredObject.wrapMode; layout.wrapMargin = anchoredObject.wrapMargin; layout.zIndex = anchoredObject.zIndex;
                    anchoredObject.rect = clone(layout.rect); anchoredObject.pageIndex = currentPageIndex;
                    resolveObjectOverlapGeometry(objects.filter(function (item) { return Number((item && item.pageIndex) || 0) === Number(currentPageIndex || 0); }), anchoredObject, currentPage().bodyFrame);
                    layout.rect = clone(anchoredObject.rect);
                    asArray(layout.caretStops).forEach(function (stop) { stop.rect.x = Number(stop.offset || 0) === 0 ? layout.rect.x : layout.rect.x + layout.rect.width; stop.rect.y = layout.rect.y; stop.rect.height = layout.rect.height; });
                    objects.push(clone(anchoredObject));
                }
                asArray(layout.caretStops).forEach(function (stop) { stop.pageIndex = currentPageIndex; });
                addBlockToPage(layout);
                if (isImage) { const exclusion = createTextExclusion(anchoredObject, currentPage().bodyFrame); if (exclusion) currentPage().exclusions.push(exclusion); }
                if (consumesFlow) currentY = layout.rect.y + layout.rect.height + blockGap;
            });

            renderHeaderFooterLayouts(model, pages.length).forEach(function (regionLayout) {
                headerFooterRegions.push(regionLayout);
                asArray(regionLayout && regionLayout.objects).forEach(function (object) { objects.push(clone(object)); });
            });

            return sortObject({ ok: true, layoutVersion: ++layoutVersion, pageMetrics, pages: pages.map(function (page) { return Object.assign({}, page, { totalPages: pages.length, blockIds: unique(page.blockIds) }); }), blocks: blockLayouts, objects, caretStops, lineIntervals: collectLayoutLineIntervals({ blocks: blockLayouts }), headerFooterRegions, staleFollowingBlockIds: [], complete: layoutComplete, laidOutBlockCount: laidOutBlockCount, debug: { source: 'paragraph-layout-document', invalidatedScopes: unique(blockLayouts.map(function (b) { return b.blockId; })), currentYOwnedByLayout: true, explicitParagraphSpacing: true, keepWithNextPrepared: true } });
        }

        function blockHasFloatingDrawingRuns(block) {
            if (!block || block.type !== 'paragraph') return false;
            return asArray(block.content && block.content.runs).some(function (run, inlineIndex) {
                if (!run || !(run.kind === 'drawing')) return false;
                const object = normalizeImageObject(run, { blockId: block.id || '', inlineIndex });
                return object && object.isInline !== true;
            });
        }

        function layoutBlockHasSimpleTextIntervals(blockLayout) {
            if (!blockLayout || blockLayout.type !== 'paragraph') return false;
            return asArray(blockLayout.lines).every(function (line) { return asArray(line && line.availableIntervals).length <= 1; });
        }

        function exclusionScopeMatchesLayoutScope(exclusion, scope) {
            const candidate = readTextExclusionScope(exclusion || {});
            const region = normalizeAnchorRegionName((scope && (scope.region || scope.Region)) || 'Body');
            if (normalizeAnchorRegionName(candidate.region) !== region) return false;
            if (asText(candidate.headerFooterId || '') !== asText((scope && (scope.headerFooterId || scope.HeaderFooterId)) || '')) return false;
            if (asText(candidate.tableId || '') !== asText((scope && (scope.tableId || scope.TableId)) || '')) return false;
            if (asText(candidate.cellId || '') !== asText((scope && (scope.cellId || scope.CellId)) || '')) return false;
            return true;
        }

        function layoutHasOverlappingExclusion(layout, rect, scope) {
            const target = rectFromGeometry(rect || {});
            if (target.width <= 0 || target.height <= 0) return false;
            let exclusions = [];
            asArray(layout && layout.pages).forEach(function (page) { exclusions = exclusions.concat(asArray(page && page.exclusions)); });
            asArray(layout && layout.headerFooterRegions).forEach(function (region) { exclusions = exclusions.concat(asArray(region && region.exclusions)); });
            return exclusions.some(function (exclusion) { if (!exclusionScopeMatchesLayoutScope(exclusion, scope)) return false; return rectIntersectsGeometry(target, rectFromGeometry(exclusion && (exclusion.rect || exclusion.Rect))); });
        }

        function shiftLayoutBlockForIncrementalReflow(blockLayout, deltaY) {
            const delta = Number(deltaY || 0) || 0;
            if (!blockLayout || Math.abs(delta) < 0.0001) return blockLayout;
            blockLayout.rect = shiftRectY(blockLayout.rect, delta);
            asArray(blockLayout.lines).forEach(function (line) { line.rect = shiftRectY(line.rect, delta); line.baseline = Number(line.baseline || 0) + delta; asArray(line.availableIntervals).forEach(function (interval) { interval.y = Number(interval.y || 0) + delta; }); asArray(line.ranges).forEach(function (range) { range.y = Number(range.y || 0) + delta; if (range.interval) range.interval.y = Number(range.interval.y || 0) + delta; }); });
            asArray(blockLayout.segments).forEach(function (seg) { seg.rect = shiftRectY(seg.rect, delta); if (seg.objectRect) seg.objectRect = shiftRectY(seg.objectRect, delta); });
            asArray(blockLayout.inlineObjects).forEach(function (object) { object.rect = shiftRectY(object.rect, delta); });
            asArray(blockLayout.caretStops).forEach(function (stop) { stop.rect = shiftRectY(stop.rect, delta); });
            asArray(blockLayout.baselines).forEach(function (baseline) { baseline.y = Number(baseline.y || 0) + delta; });
            return blockLayout;
        }

        function tryLayoutAfterOperationIncremental(model, operation, previousLayout, incOptions, scope, selection) {
            if (!previousLayout || !operation || !scope) return null;
            const type = operation.type || operation.Type || '';
            const SINGLE_PARA_OPS = ['InsertText', 'DeleteRange', 'ApplyMark', 'RemoveMark', 'SetParagraphAttribute'];
            if (SINGLE_PARA_OPS.indexOf(type) < 0) return null;
            if (scope.kind !== LayoutScopeKinds.ActiveParagraph) return null;
            if (normalizeAnchorRegionName(scope.region || 'Body') !== 'Body') return null;
            const activeBlockId = scope.blockId || firstScopeBlockId(scope);
            const block = findBlock(model, activeBlockId);
            const previousBlock = findLayoutBlock(previousLayout, activeBlockId);
            if (!block || block.type !== 'paragraph' || !previousBlock || previousBlock.type !== 'paragraph') return null;
            if (blockHasFloatingDrawingRuns(block)) return null;
            if (!layoutBlockHasSimpleTextIntervals(previousBlock)) return null;
            if (layoutHasOverlappingExclusion(previousLayout, previousBlock.rect, scope)) return null;
            const iopts = incOptions || {};
            const metrics = previousLayout.pageMetrics || {};
            const local = layoutParagraph(block, Object.assign({}, iopts, { page: previousBlock.rect, x: previousBlock.rect.x, y: previousBlock.rect.y, width: previousBlock.rect.width, lineGap: Number(iopts.lineGap ?? metrics.lineGap ?? 0) || 0, minReadableWidth: Math.max(1, Number(iopts.minReadableWidth ?? metrics.minReadableWidth ?? 48) || 48) }));
            local.pageIndex = previousBlock.pageIndex ?? 0;
            local.region = previousBlock.region || 'Body';
            local.headerFooterId = previousBlock.headerFooterId || null;
            local.tableId = previousBlock.tableId || null;
            local.cellId = previousBlock.cellId || null;
            local.columnIndex = previousBlock.columnIndex ?? null;
            asArray(local.lines).forEach(function (line) { line.pageIndex = local.pageIndex; line.region = local.region; });
            asArray(local.segments).forEach(function (seg) { seg.pageIndex = local.pageIndex; seg.region = local.region; });
            asArray(local.caretStops).forEach(function (stop) { stop.pageIndex = local.pageIndex; stop.region = local.region; });
            const heightDelta = local.rect.height - previousBlock.rect.height;
            let seenActive = false; const staleFollowing = [];
            const nextBlocks = asArray(previousLayout.blocks).map(function (layoutBlock) {
                if (layoutBlock.blockId === activeBlockId) { seenActive = true; return clone(local); }
                const c = clone(layoutBlock);
                if (seenActive && Math.abs(heightDelta) > 0.0001 && c.region === (previousBlock.region || c.region) && Number(c.pageIndex || 0) === Number(previousBlock.pageIndex || 0)) { shiftLayoutBlockForIncrementalReflow(c, heightDelta); c.stale = true; c.safeOffsetY = heightDelta; staleFollowing.push(c.blockId); }
                return c;
            });
            const next = clone(previousLayout);
            next.layoutVersion = ++layoutVersion; next.blocks = nextBlocks; next.caretStops = [];
            nextBlocks.forEach(function (lb) { next.caretStops = next.caretStops.concat(asArray(lb.caretStops)); });
            next.lineIntervals = collectLayoutLineIntervals({ blocks: nextBlocks });
            next.activeParagraphLayout = true; next.activeBlockId = activeBlockId; next.staleFollowingBlockIds = staleFollowing; next.selection = selection;
            next.debug = Object.assign({}, next.debug || {}, { source: 'paragraph-layout-document-incremental', minimalScope: scope, incrementalReflow: true, skippedPageExclusionRebuild: true, invalidatedScopes: [activeBlockId], staleFollowingBlockIds: staleFollowing, heightDelta, reflowBoundary: { kind: 'activeParagraph', blockId: activeBlockId, region: 'Body', pageIndex: Number(previousBlock.pageIndex || 0) || 0, reason: type } });
            return sortObject(next);
        }

        function layoutAfterOperation(model, operation, previousLayout, opOptions) {
            const scope = inferLayoutScopeFromOperation(operation);
            const oopts = opOptions || {};
            const selection = clone(oopts.selection || oopts.Selection || null);
            const incremental = tryLayoutAfterOperationIncremental(model, operation, previousLayout, oopts, scope, selection);
            if (incremental) return incremental;
            const next = layoutDocument(model, oopts);
            const activeBlockId = scope.blockId || firstScopeBlockId(scope);
            const previousBlock = findLayoutBlock(previousLayout, activeBlockId);
            const nextBlock = findLayoutBlock(next, activeBlockId);
            const heightDelta = previousBlock && nextBlock ? nextBlock.rect.height - previousBlock.rect.height : 0;
            const staleFollowing = [];
            if (heightDelta > 0 && nextBlock) {
                let shiftStart = false;
                next.blocks.forEach(function (b) {
                    if (b.blockId === activeBlockId) { shiftStart = true; return; }
                    if (!shiftStart) return;
                    b.stale = true; b.safeOffsetY = Math.max(0, heightDelta);
                    b.rect.y += b.safeOffsetY;
                    asArray(b.lines).forEach(function (line) { line.rect.y += b.safeOffsetY; line.baseline += b.safeOffsetY; });
                    asArray(b.segments).forEach(function (seg) { seg.rect.y += b.safeOffsetY; });
                    asArray(b.caretStops).forEach(function (stop) { stop.rect.y += b.safeOffsetY; });
                    staleFollowing.push(b.blockId);
                });
            }
            const invalidated = unique((scope.affectedScopeIds || []).concat(activeBlockId ? [activeBlockId] : []));
            next.activeParagraphLayout = scope.kind === LayoutScopeKinds.ActiveParagraph || scope.kind === LayoutScopeKinds.WholeBlock;
            next.activeBlockId = activeBlockId || null; next.staleFollowingBlockIds = staleFollowing; next.selection = selection;
            next.debug = Object.assign({}, next.debug || {}, { minimalScope: scope, invalidatedScopes: invalidated, staleFollowingBlockIds: staleFollowing, heightDelta });
            return sortObject(next);
        }

        function renderParagraphLayout(root, layout) {
            if (!root || !layout) return null;
            const doc = globalThis.document;
            if (!doc) return null;
            const container = doc.createElement('div');
            container.className = 'tm-paragraph-layout';
            container.setAttribute('data-layout-block-id', layout.id || ('layout-' + layout.blockId));
            container.setAttribute('data-block-id', layout.blockId || '');
            container.style.position = 'absolute';
            container.style.left = layout.rect.x + 'px';
            container.style.top = layout.rect.y + 'px';
            container.style.width = layout.rect.width + 'px';
            container.style.height = layout.rect.height + 'px';
            container.style.whiteSpace = 'pre';
            container.style.overflow = 'visible';
            asArray(layout.segments).forEach(function (segment) {
                const span = doc.createElement('span');
                span.setAttribute('data-layout-segment-id', segment.id);
                span.setAttribute('data-block-id', layout.blockId || '');
                span.setAttribute('data-run-id', segment.runId || '');
                span.setAttribute('data-layout-height', segment.rect.height);
                span.textContent = segment.text || '';
                span.style.position = 'absolute';
                span.style.left = (segment.rect.x - layout.rect.x) + 'px';
                span.style.top = (segment.rect.y - layout.rect.y) + 'px';
                span.style.width = segment.rect.width + 'px';
                span.style.height = segment.rect.height + 'px';
                span.style.lineHeight = segment.rect.height + 'px';
                span.style.whiteSpace = 'pre';
                span.style.overflow = 'hidden';
                span.style.display = 'block';
                applySegmentStyleToElement(span, segment.style || {}, segment.decorations || []);
                container.appendChild(span);
            });
            root.innerHTML = '';
            root.appendChild(container);
            return container;
        }

        return {
            createLayoutScope,
            inferLayoutScopeFromOperation,
            computeMinimalScopeForOperation: inferLayoutScopeFromOperation,
            layoutParagraph,
            layoutDocument,
            layoutAfterOperation,
            renderParagraphLayout,
            getMeasurementStats: function () { return service.getStats(); },
        };
    };
}
