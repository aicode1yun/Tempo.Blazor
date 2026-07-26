// Phase D — objects/image-object.mjs
// `normalizeImageObject` — the big image-object normalizer. Takes any image-like
// record (block or drawing run) plus context (page region, anchor) and produces the
// canonical `{ blockId, objectId, anchorBlockId, anchorOffset, layoutKind, wrapMode,
// width, height, … }` shape used by the floating-image layout engine.
//
// `imageObjectToLayout` — inverse: turn the normalized object back into a `Layout`
// payload that round-trips through the C# wire format.
//
// Pure functions — extracted now that all dependencies (wrap modes, anchor region,
// layout helpers, geometry, drawing-kind discrimination, position specs) are in
// modules.

import { asArray, asText, clone, sortObject } from '../core/helpers.mjs';
import { normalizeTextExclusionColumnIndex } from '../core/normalize-target.mjs';
import { isDrawingRunSource } from '../core/inline-runs.mjs';
import { normalizeWrapModeName, normalizeWrapSideName, wrapSideToValue } from './wrap-modes.mjs';
import {
    normalizeAnchorRegionName,
    anchorRegionToValue,
    readObjectLayoutInCell,
} from './anchor-region.mjs';
import {
    readObjectWrapSide,
    normalizePositionSpec,
    normalizeLayoutKindName,
    relativePositionToValue,
    verticalAlignmentToValue,
} from './layout-helpers.mjs';
import { wrapModeToValue } from './wrap-mode-value.mjs';
import { horizontalPositionToValue } from './horizontal-position.mjs';
import { rectFromGeometry, normalizeWrapContourPointsForGeometry } from './geometry.mjs';

export function normalizeImageObject(block, options) {
    const opts = options || {};
    const isDrawing = block && (block.kind === 'drawing'
        || block.Kind === 'drawing' || isDrawingRunSource(block));
    const nestedDrawing = block && (block.drawing || block.Drawing);
    const content = isDrawing
        ? (nestedDrawing || block || {})
        : ((block && (block.content || block.Content)) || {});
    const layout = content.layout || content.Layout || {};
    const anchor = layout.anchor || layout.Anchor || {};
    const wrap = layout.wrap || layout.Wrap || {};
    const position = layout.position || layout.Position || {};
    const transform = layout.transform || layout.Transform || {};
    const stacking = layout.stacking || layout.Stacking || {};
    const horizontal = layout.horizontalPosition || layout.HorizontalPosition || layout.Horizontal || {};
    const vertical = layout.verticalPosition || layout.VerticalPosition || layout.Vertical || {};

    const layoutInCell = readObjectLayoutInCell(content);
    const wrapMode = normalizeWrapModeName(layout.wrapMode ?? layout.WrapMode
        ?? wrap.mode ?? wrap.Mode ?? opts.wrapMode ?? opts.WrapMode);
    const wrapSide = normalizeWrapSideName(
        layout.wrapSide ?? layout.WrapSide
        ?? wrap.side ?? wrap.Side
        ?? wrap.wrapText ?? wrap.WrapText
        ?? opts.wrapSide ?? opts.WrapSide
        ?? opts.side ?? opts.Side
        ?? opts.wrapText ?? opts.WrapText);

    let horizontalAlign = position.horizontalAlignment ?? position.HorizontalAlignment
        ?? horizontal.align ?? horizontal.Align ?? null;
    if (horizontalAlign === 0) horizontalAlign = 'Left';
    if (horizontalAlign === 1) horizontalAlign = 'Center';
    if (horizontalAlign === 2) horizontalAlign = 'Right';

    let verticalAlign = position.verticalAlignment ?? position.VerticalAlignment
        ?? vertical.align ?? vertical.Align ?? null;
    if (verticalAlign === 0) verticalAlign = 'None';
    if (verticalAlign === 1) verticalAlign = 'Top';
    if (verticalAlign === 2) verticalAlign = 'Middle';
    if (verticalAlign === 3) verticalAlign = 'Bottom';

    const contentSize = content.size || content.Size || {};
    const width = Math.max(1, Number(
        transform.width ?? transform.Width
        ?? layout.width ?? layout.Width
        ?? content.width ?? content.Width
        ?? contentSize.width ?? contentSize.Width
        ?? opts.width ?? opts.Width ?? 120) || 120);
    const height = Math.max(1, Number(
        transform.height ?? transform.Height
        ?? layout.height ?? layout.Height
        ?? content.height ?? content.Height
        ?? contentSize.height ?? contentSize.Height
        ?? opts.height ?? opts.Height ?? 80) || 80);

    const wrapMargin = Math.max(0, Number(layout.wrapMargin ?? layout.WrapMargin
        ?? wrap.margin ?? wrap.Margin ?? 0) || 0);
    const rawDistanceLeft = wrap.distanceLeft ?? wrap.DistanceLeft;
    const rawDistanceRight = wrap.distanceRight ?? wrap.DistanceRight;
    const rawDistanceTop = wrap.distanceTop ?? wrap.DistanceTop;
    const rawDistanceBottom = wrap.distanceBottom ?? wrap.DistanceBottom;
    const distanceLeft = (rawDistanceLeft === undefined || rawDistanceLeft === null)
        ? wrapMargin : (Number(rawDistanceLeft) || 0);
    const distanceRight = (rawDistanceRight === undefined || rawDistanceRight === null)
        ? wrapMargin : (Number(rawDistanceRight) || 0);
    const distanceTop = (rawDistanceTop === undefined || rawDistanceTop === null)
        ? wrapMargin : (Number(rawDistanceTop) || 0);
    const distanceBottom = (rawDistanceBottom === undefined || rawDistanceBottom === null)
        ? wrapMargin : (Number(rawDistanceBottom) || 0);

    const blockId = asText(
        opts.blockId || opts.BlockId
        || content.blockId || content.BlockId
        || (!isDrawing && block && (block.id || block.Id))
        || '');

    let layoutKind = normalizeLayoutKindName(layout.kind ?? layout.Kind);
    if (layoutKind === 'Inline' && wrapMode !== 'Inline') layoutKind = 'Anchored';

    const anchorBlockId = asText(
        layout.anchorBlockId || layout.AnchorBlockId
        || anchor.blockId || anchor.BlockId
        || opts.anchorBlockId || opts.AnchorBlockId
        || (isDrawing ? blockId : ''));
    const anchorOffset = Number(layout.anchorOffset ?? layout.AnchorOffset
        ?? anchor.offset ?? anchor.Offset
        ?? opts.anchorOffset ?? opts.AnchorOffset ?? 0) || 0;

    return sortObject({
        blockId,
        objectId: asText(content.objectId || content.ObjectId
            || content.id || content.Id
            || (block && (block.id || block.Id)) || ''),
        anchorBlockId,
        anchorOffset,
        anchorInlineIndex: Number(anchor.inlineIndex ?? anchor.InlineIndex
            ?? opts.inlineIndex ?? opts.InlineIndex ?? -1),
        anchorTableId: asText(anchor.tableId || anchor.TableId
            || layout.tableId || layout.TableId
            || opts.tableId || opts.TableId || ''),
        anchorCellId: asText(anchor.cellId || anchor.CellId
            || layout.cellId || layout.CellId
            || opts.cellId || opts.CellId || ''),
        anchorHeaderFooterId: asText(anchor.headerFooterId || anchor.HeaderFooterId
            || layout.headerFooterId || layout.HeaderFooterId
            || opts.headerFooterId || opts.HeaderFooterId || ''),
        anchorColumnIndex: normalizeTextExclusionColumnIndex(
            anchor.columnIndex ?? anchor.ColumnIndex
            ?? layout.columnIndex ?? layout.ColumnIndex
            ?? opts.columnIndex ?? opts.ColumnIndex),
        layoutKind,
        isInline: layoutKind === 'Inline' || wrapMode === 'Inline',
        isAnchored: layoutKind === 'Anchored' && wrapMode !== 'Inline',
        layoutInCell,
        lockAspectRatio: (transform.lockAspectRatio ?? transform.LockAspectRatio
            ?? contentSize.lockAspectRatio ?? contentSize.LockAspectRatio ?? true) !== false,
        moveWithText: (layout.moveWithText ?? layout.MoveWithText
            ?? anchor.moveWithText ?? anchor.MoveWithText ?? true) !== false,
        fixedOnPage: (layout.fixedOnPage ?? layout.FixedOnPage
            ?? anchor.fixedOnPage ?? anchor.FixedOnPage ?? false) === true,
        lockAnchor: (layout.lockAnchor ?? layout.LockAnchor
            ?? anchor.lockAnchor ?? anchor.LockAnchor ?? false) === true,
        anchorRegion: normalizeAnchorRegionName(
            layout.anchorRegion || layout.AnchorRegion
            || anchor.region || anchor.Region
            || opts.region || opts.Region || 'Body'),
        horizontalPosition: normalizePositionSpec(Object.assign({}, horizontal, {
            align: horizontalAlign || horizontal.align || horizontal.Align || null,
            relativeTo: position.horizontalRelativeTo || position.HorizontalRelativeTo
                || horizontal.relativeTo || horizontal.RelativeTo || 'Page',
            offset: position.x ?? position.X ?? horizontal.offset ?? horizontal.Offset ?? 0,
        }), 'Left'),
        verticalPosition: normalizePositionSpec(Object.assign({}, vertical, {
            align: verticalAlign || vertical.align || vertical.Align || null,
            relativeTo: position.verticalRelativeTo || position.VerticalRelativeTo
                || vertical.relativeTo || vertical.RelativeTo || 'Paragraph',
            offset: position.y ?? position.Y ?? vertical.offset ?? vertical.Offset ?? 0,
        }), 'Top'),
        wrapMode,
        wrapSide,
        wrapMargin,
        distanceLeft,
        distanceRight,
        distanceTop,
        distanceBottom,
        wrapContourPoints: normalizeWrapContourPointsForGeometry(
            wrap.wrapContourPoints ?? wrap.WrapContourPoints
            ?? layout.wrapContourPoints ?? layout.WrapContourPoints),
        allowOverlap: (layout.allowOverlap ?? layout.AllowOverlap
            ?? stacking.allowOverlap ?? stacking.AllowOverlap ?? false) === true,
        zIndex: Number(layout.zIndex ?? layout.ZIndex
            ?? stacking.zIndex ?? stacking.ZIndex ?? 0) || 0,
        width,
        height,
        rect: (content.rect || content.Rect || layout.rect || layout.Rect
            || opts.rect || opts.Rect)
            ? rectFromGeometry(content.rect || content.Rect || layout.rect || layout.Rect
                || opts.rect || opts.Rect)
            : null,
        caption: asText(content.caption || content.Caption || ''),
        altText: asText(content.altText || content.AltText || ''),
        url: content.url || content.Url || null,
        assetId: content.assetId || content.AssetId || null,
    });
}

// Inverse — turn a normalized image object back into a Layout payload (the shape that
// the C# wire format expects). Does NOT call `syncImageLayoutCase`; the caller is
// expected to do that pass (it's a separate concern that lives with the runtime
// because it mutates camel+Pascal mirror fields).
export function imageObjectToLayout(object) {
    const source = object || {};
    const horizontal = source.horizontalPosition || {};
    const vertical = source.verticalPosition || {};
    const mode = normalizeWrapModeName(source.wrapMode || 'Inline');
    const kind = mode === 'Inline'
        ? 0
        : ((source.fixedOnPage === true || source.layoutKind === 'Fixed') ? 2 : 1);
    return {
        Kind: kind,
        Anchor: {
            BlockId: source.anchorBlockId || '',
            Offset: Number(source.anchorOffset || 0) || 0,
            InlineIndex: Number(source.anchorInlineIndex ?? -1),
            Region: anchorRegionToValue(source.anchorRegion || source.region || 'Body'),
            TableId: source.anchorTableId || source.tableId || null,
            CellId: source.anchorCellId || source.cellId || null,
            ColumnIndex: normalizeTextExclusionColumnIndex(source.anchorColumnIndex ?? source.columnIndex),
            HeaderFooterId: source.anchorHeaderFooterId || source.headerFooterId || null,
            MoveWithText: source.moveWithText !== false && source.fixedOnPage !== true,
            FixedOnPage: source.fixedOnPage === true,
            LockAnchor: source.lockAnchor === true,
        },
        Position: {
            HorizontalRelativeTo: relativePositionToValue(
                horizontal.relativeTo || horizontal.RelativeTo || 'Column'),
            VerticalRelativeTo: relativePositionToValue(
                vertical.relativeTo || vertical.RelativeTo || 'Paragraph'),
            HorizontalAlignment: horizontalPositionToValue(horizontal.align || horizontal.Align || 'Left'),
            VerticalAlignment: verticalAlignmentToValue(vertical.align || vertical.Align || 'Top'),
            X: Number(horizontal.offset ?? horizontal.Offset ?? 0) || 0,
            Y: Number(vertical.offset ?? vertical.Offset ?? 0) || 0,
        },
        Wrap: {
            Mode: wrapModeToValue(mode),
            Side: wrapSideToValue(readObjectWrapSide(source)),
            DistanceLeft: Number(source.distanceLeft ?? source.DistanceLeft ?? 0) || 0,
            DistanceRight: Number(source.distanceRight ?? source.DistanceRight ?? 0) || 0,
            DistanceTop: Number(source.distanceTop ?? source.DistanceTop ?? 0) || 0,
            DistanceBottom: Number(source.distanceBottom ?? source.DistanceBottom ?? 0) || 0,
            WrapContourPoints: asArray(source.wrapContourPoints || source.WrapContourPoints).length
                ? normalizeWrapContourPointsForGeometry(source.wrapContourPoints || source.WrapContourPoints)
                    .map(point => ({ X: point.x, Y: point.y }))
                : [],
        },
        Transform: {
            Width: Math.max(1, Number(source.width || 120) || 120),
            Height: Math.max(1, Number(source.height || 80) || 80),
            LockAspectRatio: source.lockAspectRatio !== false,
        },
        Stacking: {
            AllowOverlap: source.allowOverlap === true,
            ZIndex: Number(source.zIndex || 0) || 0,
        },
    };
}
