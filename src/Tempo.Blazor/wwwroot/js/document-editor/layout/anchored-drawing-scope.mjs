// Phase D — layout/anchored-drawing-scope.mjs
// `createAnchoredDrawingLayoutScope()` — fresh scope `{objects, exclusions,
//   anchoredIds: Set}`. Callers accumulate placements + text-exclusion rects here
//   while laying out a page region.
// `createAnchoredDrawingScopeAggregator({createBlockIndexContext,
//   collectAnchoredDrawingRuns, readObjectLayoutInCell,
//   resolveAnchoredDrawingReference, createAnchoredDrawingLayoutObject,
//   resolveObjectOverlapGeometry, createTextExclusion,
//   normalizeTextExclusionColumnIndex, rectFromGeometry, sortObject})` factory →
//   `addAnchoredDrawingRunsToLayoutScope(block, targetScope, frame, context,
//     fallbackY, laidOutBlocks)` — walks anchored drawings in `block`, dedupes by
//   `objectId`, places each one in the page rect (for `TableCell` + `layoutInCell=false`
//   the page frame from `context.pageFrame` wins), pushes a placed object record +
//   text-exclusion (when wrap mode produces one).

export function createAnchoredDrawingLayoutScope() {
    return { objects: [], exclusions: [], anchoredIds: new Set() };
}

const REQUIRED = [
    'createBlockIndexContext',
    'collectAnchoredDrawingRuns',
    'readObjectLayoutInCell',
    'resolveAnchoredDrawingReference',
    'createAnchoredDrawingLayoutObject',
    'resolveObjectOverlapGeometry',
    'createTextExclusion',
    'normalizeTextExclusionColumnIndex',
    'rectFromGeometry',
    'sortObject',
];

export function createAnchoredDrawingScopeAggregator(deps) {
    const opts = deps || {};
    for (const key of REQUIRED) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createAnchoredDrawingScopeAggregator requires options.${key} (function)`);
        }
    }
    const {
        createBlockIndexContext,
        collectAnchoredDrawingRuns,
        readObjectLayoutInCell,
        resolveAnchoredDrawingReference,
        createAnchoredDrawingLayoutObject,
        resolveObjectOverlapGeometry,
        createTextExclusion,
        normalizeTextExclusionColumnIndex,
        rectFromGeometry,
        sortObject,
    } = opts;

    return function addAnchoredDrawingRunsToLayoutScope(
        block, targetScope, frame, context, fallbackY, laidOutBlocks) {
        const scope = targetScope || createAnchoredDrawingLayoutScope();
        const ctx = createBlockIndexContext(context);
        const scopeFrame = rectFromGeometry(frame || { x: 0, y: 0, width: 640, height: 900 });
        scopeFrame.height = Math.max(1, Number(scopeFrame.height || 0) || 900);
        collectAnchoredDrawingRuns(block, ctx).forEach(function (entry) {
            const objectId = (entry.object && entry.object.objectId)
                || (entry.run && (entry.run.objectId || entry.run.id))
                || '';
            if (!objectId || scope.anchoredIds.has(objectId)) return;
            const usesPageScope = ctx.region === 'TableCell'
                && readObjectLayoutInCell(entry.object) === false
                && !!ctx.pageFrame;
            const placementFrame = usesPageScope
                ? rectFromGeometry(ctx.pageFrame)
                : scopeFrame;
            let reference = resolveAnchoredDrawingReference(
                entry.object, laidOutBlocks || [], [], {
                    blockId: (block && block.id) || '',
                    pageIndex: Number(ctx.pageIndex ?? 0) || 0,
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
                pageIndex: Number(ctx.pageIndex ?? reference.pageIndex ?? 0) || 0,
                rect: placementFrame,
                bodyFrame: placementFrame,
            });
            placed.region = ctx.region || placed.region || 'Body';
            placed.headerFooterId = ctx.headerFooterId || placed.headerFooterId || null;
            placed.tableId = ctx.tableId || placed.tableId || null;
            placed.cellId = ctx.cellId || placed.cellId || null;
            placed.columnIndex = normalizeTextExclusionColumnIndex(
                ctx.columnIndex ?? placed.columnIndex);
            placed.anchorRegion = placed.anchorRegion || placed.region;
            placed.anchorHeaderFooterId = placed.anchorHeaderFooterId
                || placed.headerFooterId || '';
            placed.anchorTableId = placed.anchorTableId || placed.tableId || '';
            placed.anchorCellId = placed.anchorCellId || placed.cellId || '';
            placed.anchorColumnIndex = normalizeTextExclusionColumnIndex(
                placed.anchorColumnIndex ?? placed.columnIndex);
            placed.layoutInCell = readObjectLayoutInCell(placed);
            resolveObjectOverlapGeometry(scope.objects, placed, placementFrame);
            scope.objects.push(sortObject(placed));
            scope.anchoredIds.add(objectId);
            const exclusion = createTextExclusion(placed, placementFrame);
            if (exclusion) scope.exclusions.push(exclusion);
        });
        return scope;
    };
}
