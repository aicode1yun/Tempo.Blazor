// Phase D — objects/image-insert.mjs
// Model-only pipeline for inserting a floating/inline drawing run into a paragraph.
//
// `readImageInsertDimension(image, layout, key, fallback)` — resolves a Width/Height
//   from the precedence chain transform → layout → size → image → naturalSize,
//   clamped to ≥ 1.
// `splitInlineListForDrawingInsert(block, offset, options)` — splits the paragraph's
//   runs at `offset` into `{before, after, inlineIndex, offset, textLength}`,
//   preserving drawing runs and re-merging adjacent text fragments.
// `createInlineDrawingLayoutForInsert(image, blockId, offset, inlineIndex, context)`
//   — builds the canonical (Pascal+camel mirrored) Layout for a newly inserted
//   image anchored at the given block/offset.
// `createDrawingRunFromImageInsert(block, split, op, context)` — assembles the
//   normalized drawing run from an InsertImage operation payload.
// `insertDrawingRunAtTextOffset(block, offset, drawingRun, options)` — splices a
//   drawing run into the paragraph at a text offset and returns placement info.

import { asArray, asText, clone, read, stableId, sortObject } from '../core/helpers.mjs';
import { blockText } from '../core/text-helpers.mjs';
import { normalizeTextExclusionColumnIndex } from '../core/normalize-target.mjs';
import {
    isDrawingRunSource,
    normalizeDrawingRun,
    normalizeTextRunForMerge,
    mergeAdjacentTextRuns,
    plainRuns,
} from '../core/inline-runs.mjs';
import { anchorRegionToValue } from './anchor-region.mjs';
import { wrapModeToValue } from './wrap-mode-value.mjs';
import { syncImageLayoutCase } from './sync-image-layout.mjs';

export function readImageInsertDimension(image, layout, key, fallback) {
    const transform = (layout && (layout.Transform || layout.transform)) || {};
    const size = (image && (image.Size || image.size)) || {};
    const naturalSize = (image && (image.NaturalSize || image.naturalSize)) || {};
    const layoutWidth = layout ? (layout.Width ?? layout.width) : undefined;
    const layoutHeight = layout ? (layout.Height ?? layout.height) : undefined;
    const imageWidth = image ? (image.Width ?? image.width) : undefined;
    const imageHeight = image ? (image.Height ?? image.height) : undefined;
    const value = key === 'Width'
        ? (transform.Width ?? transform.width ?? layoutWidth ?? size.Width ?? size.width ?? imageWidth ?? naturalSize.Width ?? naturalSize.width)
        : (transform.Height ?? transform.height ?? layoutHeight ?? size.Height ?? size.height ?? imageHeight ?? naturalSize.Height ?? naturalSize.height);
    return Math.max(1, Number(value ?? fallback) || fallback);
}

export function splitInlineListForDrawingInsert(block, offset, options) {
    if (!block.content) block.content = { type: 'paragraph', runs: [] };
    if (!Array.isArray(block.content.runs) || block.content.runs.length === 0) {
        block.content.runs = plainRuns('', (block.id || 'block') + '-empty');
    }
    const opts = options || {};
    const textLength = blockText(block).length;
    const targetOffset = Math.max(0, Math.min(textLength, Number(offset || 0) || 0));
    const affinity = opts.affinity === 'before' ? 'before' : 'after';
    let before = [];
    let after = [];
    let cursor = 0;
    let inserted = false;

    function pushRun(collection, run, index) {
        if (run && (run.kind === 'drawing' || isDrawingRunSource(run))) {
            collection.push(normalizeDrawingRun(run, run.id || run.objectId || ((block.id || 'block') + '-drawing-' + index)));
        } else {
            collection.push(normalizeTextRunForMerge(run || {}));
        }
    }

    asArray(block.content.runs).forEach(function (run, index) {
        const isDrawing = run && (run.kind === 'drawing' || isDrawingRunSource(run));
        if (isDrawing) {
            if (!inserted && cursor === targetOffset && affinity === 'before') {
                after.push(normalizeDrawingRun(run, run.id || run.objectId || ((block.id || 'block') + '-drawing-' + index)));
                return;
            }
            pushRun(inserted || cursor <= targetOffset ? before : after, run, index);
            return;
        }

        const runText = asText(run && run.text);
        const runStart = cursor;
        const runEnd = cursor + runText.length;
        cursor = runEnd;
        if (!inserted && targetOffset >= runStart && targetOffset <= runEnd) {
            const local = Math.max(0, Math.min(runText.length, targetOffset - runStart));
            if (local > 0) {
                const beforeRun = clone(run);
                beforeRun.id = asText(run && run.id || stableId('inline', block.id + '-run-' + index)) + '-drawing-before';
                beforeRun.text = runText.slice(0, local);
                before.push(normalizeTextRunForMerge(beforeRun));
            }
            if (local < runText.length) {
                const afterRun = clone(run);
                afterRun.id = asText(run && run.id || stableId('inline', block.id + '-run-' + index)) + '-drawing-after';
                afterRun.text = runText.slice(local);
                after.push(normalizeTextRunForMerge(afterRun));
            }
            inserted = true;
            return;
        }
        pushRun(inserted ? after : before, run, index);
    });

    if (!inserted) {
        before = mergeAdjacentTextRuns(before).filter(function (run) { return !(run.kind === 'text' && asText(run.text).length === 0); });
        after = mergeAdjacentTextRuns(after).filter(function (run) { return !(run.kind === 'text' && asText(run.text).length === 0); });
    } else {
        before = before.length > 0 ? mergeAdjacentTextRuns(before) : [];
        after = after.length > 0 ? mergeAdjacentTextRuns(after) : [];
    }

    return sortObject({
        before,
        after,
        inlineIndex: before.length,
        offset: targetOffset,
        textLength,
    });
}

export function createInlineDrawingLayoutForInsert(image, blockId, offset, inlineIndex, context) {
    const regionContext = context || {};
    const sourceLayout = clone(read(image || {}, 'Layout', 'layout', read(image || {}, 'FloatingLayout', 'floatingLayout', {})) || {});
    const layout = sourceLayout && typeof sourceLayout === 'object' ? sourceLayout : {};
    const wrap = clone(layout.Wrap || layout.wrap || {});
    const transform = clone(layout.Transform || layout.transform || {});
    const anchor = clone(layout.Anchor || layout.anchor || {});
    const position = clone(layout.Position || layout.position || {});
    const stacking = clone(layout.Stacking || layout.stacking || {});
    const wrapMode = wrap.Mode ?? wrap.mode ?? layout.WrapMode ?? layout.wrapMode ?? 0;
    const width = readImageInsertDimension(image, layout, 'Width', 120);
    const height = readImageInsertDimension(image, layout, 'Height', 80);

    anchor.BlockId = blockId;
    anchor.blockId = blockId;
    anchor.Offset = Number(offset || 0) || 0;
    anchor.offset = anchor.Offset;
    anchor.InlineIndex = Number(inlineIndex ?? -1);
    anchor.inlineIndex = anchor.InlineIndex;
    anchor.Region = anchorRegionToValue(regionContext.region || regionContext.Region || anchor.Region || anchor.region || 'Body');
    anchor.region = anchor.Region;
    anchor.TableId = regionContext.tableId || regionContext.TableId || anchor.TableId || anchor.tableId || null;
    anchor.tableId = anchor.TableId;
    anchor.CellId = regionContext.cellId || regionContext.CellId || anchor.CellId || anchor.cellId || null;
    anchor.cellId = anchor.CellId;
    anchor.ColumnIndex = normalizeTextExclusionColumnIndex(regionContext.columnIndex ?? regionContext.ColumnIndex ?? anchor.ColumnIndex ?? anchor.columnIndex);
    anchor.columnIndex = anchor.ColumnIndex;
    anchor.HeaderFooterId = regionContext.headerFooterId || regionContext.HeaderFooterId || anchor.HeaderFooterId || anchor.headerFooterId || null;
    anchor.headerFooterId = anchor.HeaderFooterId;
    anchor.MoveWithText = (anchor.MoveWithText ?? anchor.moveWithText ?? true) !== false;
    anchor.moveWithText = anchor.MoveWithText;
    anchor.FixedOnPage = (anchor.FixedOnPage ?? anchor.fixedOnPage ?? false) === true;
    anchor.fixedOnPage = anchor.FixedOnPage;
    anchor.LockAnchor = (anchor.LockAnchor ?? anchor.lockAnchor ?? false) === true;
    anchor.lockAnchor = anchor.LockAnchor;

    wrap.Mode = wrapModeToValue(wrapMode);
    wrap.mode = wrap.Mode;
    wrap.DistanceLeft = Number(wrap.DistanceLeft ?? wrap.distanceLeft ?? 0) || 0;
    wrap.distanceLeft = wrap.DistanceLeft;
    wrap.DistanceRight = Number(wrap.DistanceRight ?? wrap.distanceRight ?? 0) || 0;
    wrap.distanceRight = wrap.DistanceRight;
    wrap.DistanceTop = Number(wrap.DistanceTop ?? wrap.distanceTop ?? 0) || 0;
    wrap.distanceTop = wrap.DistanceTop;
    wrap.DistanceBottom = Number(wrap.DistanceBottom ?? wrap.distanceBottom ?? 0) || 0;
    wrap.distanceBottom = wrap.DistanceBottom;

    transform.Width = width;
    transform.width = width;
    transform.Height = height;
    transform.height = height;
    transform.LockAspectRatio = (transform.LockAspectRatio ?? transform.lockAspectRatio ?? true) !== false;
    transform.lockAspectRatio = transform.LockAspectRatio;

    return syncImageLayoutCase({
        Kind: layout.Kind ?? layout.kind ?? (wrap.Mode === 0 ? 0 : (anchor.FixedOnPage ? 2 : 1)),
        Anchor: anchor,
        Position: position,
        Wrap: wrap,
        Transform: transform,
        Stacking: stacking,
    });
}

export function createDrawingRunFromImageInsert(block, split, op, context, normalizeImageInsertPayload) {
    const payload = normalizeImageInsertPayload(op);
    const image = payload.image || {};
    const objectId = payload.objectId;
    const inlineId = asText(read(op || {}, 'InlineId', 'inlineId', read(op || {}, 'RunId', 'runId', read(image, 'InlineId', 'inlineId', ''))))
        || stableId('drawing', objectId);
    const layout = createInlineDrawingLayoutForInsert(image, block.id, split.offset, split.inlineIndex, context);
    return normalizeDrawingRun({
        $type: 'drawing',
        Id: inlineId,
        ObjectId: objectId,
        Kind: read(image, 'Kind', 'kind', read(image, 'DrawingKind', 'drawingKind', 0)),
        Source: read(image, 'Source', 'source', 0),
        Url: read(image, 'Url', 'url', null),
        AssetId: read(image, 'AssetId', 'assetId', null),
        AltText: read(image, 'AltText', 'altText', ''),
        IsDecorative: read(image, 'IsDecorative', 'isDecorative', false) === true,
        Caption: read(image, 'Caption', 'caption', ''),
        Size: read(image, 'Size', 'size', {}),
        NaturalSize: read(image, 'NaturalSize', 'naturalSize', {}),
        Layout: layout,
        Style: read(image, 'Style', 'style', {}),
        LinkUrl: read(image, 'LinkUrl', 'linkUrl', null),
        Docx: read(image, 'Docx', 'docx', null),
        Metadata: payload.metadata,
    }, inlineId);
}

export function insertDrawingRunAtTextOffset(block, offset, drawingRun, options) {
    if (!block.content) block.content = { type: 'paragraph', runs: [] };
    const split = splitInlineListForDrawingInsert(block, offset, options);
    const normalizedDrawing = normalizeDrawingRun(drawingRun, drawingRun && (drawingRun.id || drawingRun.objectId) || ((block.id || 'block') + '-drawing'));
    block.content.runs = mergeAdjacentTextRuns(split.before.concat([normalizedDrawing], split.after));
    if (!block.content.runs.length) block.content.runs = plainRuns('', (block.id || 'block') + '-empty');
    let inlineIndex = block.content.runs.findIndex(function (run) {
        return run && run.kind === 'drawing' && (run.objectId === normalizedDrawing.objectId || run.id === normalizedDrawing.id);
    });
    if (inlineIndex < 0) inlineIndex = split.inlineIndex;
    const run = block.content.runs[inlineIndex] || normalizedDrawing;
    return sortObject({
        ok: true,
        blockId: block.id || '',
        objectId: run.objectId || normalizedDrawing.objectId || '',
        runId: run.id || normalizedDrawing.id || '',
        inlineIndex,
        offset: split.offset,
        split,
    });
}
