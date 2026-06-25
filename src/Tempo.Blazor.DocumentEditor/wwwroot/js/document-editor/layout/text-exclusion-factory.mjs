// Phase D — layout/text-exclusion-factory.mjs
// `createTextExclusion(objectLayout, bodyFrame)` — builds a single text-exclusion record
// for one floating object (image/drawing). Returns null when the wrap mode does not
// produce a text exclusion or when the resulting rect is degenerate after clipping
// against the body frame. For wrap mode `TopBottom` the exclusion grows to the body
// width; for `Tight`/`Through` it follows the projected wrap-contour polygon.

import { clone, sortObject } from '../core/helpers.mjs';
import { normalizeWrapModeName } from '../objects/wrap-modes.mjs';
import { wrapModeCreatesTextExclusion } from '../objects/wrap-mode-value.mjs';
import {
    normalizeAnchorRegionName,
    readObjectLayoutInCell,
} from '../objects/anchor-region.mjs';
import { readObjectWrapSide } from '../objects/layout-helpers.mjs';
import {
    rectFromGeometry,
    rectOverlapsHorizontallyGeometry,
    intersectGeometryRect,
    geometryBoundsOfPoints,
    readObjectDistance,
    createObjectFootprintRect,
    createObjectWrapRect,
    projectWrapContourPointsForGeometry,
} from '../objects/geometry.mjs';
import {
    normalizeTextExclusionPageIndex,
    createTextExclusionScopeKey,
} from './text-exclusion.mjs';
import { normalizeTextExclusionColumnIndex } from '../core/normalize-target.mjs';

export function createTextExclusion(objectLayout, bodyFrame) {
    const object = objectLayout || {};
    const wrap = object.wrap || object.Wrap || {};
    const mode = normalizeWrapModeName(
        object.wrapMode ?? object.WrapMode ?? wrap.mode ?? wrap.Mode);
    if (!wrapModeCreatesTextExclusion(mode)) return null;
    const body = bodyFrame ? rectFromGeometry(bodyFrame) : null;
    const horizontalPosition = object.horizontalPosition || object.HorizontalPosition || {};
    const verticalPosition = object.verticalPosition || object.VerticalPosition || {};
    const rect = rectFromGeometry(object.rect || object.Rect || {
        x: Number((bodyFrame && (bodyFrame.x ?? bodyFrame.X)) || 0)
            + Number(horizontalPosition.offset ?? horizontalPosition.Offset ?? 0),
        y: Number((bodyFrame && (bodyFrame.y ?? bodyFrame.Y)) || 0)
            + Number(verticalPosition.offset ?? verticalPosition.Offset ?? 0),
        width: Number(object.width ?? object.Width ?? 1) || 1,
        height: Number(object.height ?? object.Height ?? 1) || 1,
    });
    const wrapRect = createObjectWrapRect(object, rect);
    const pageIndex = normalizeTextExclusionPageIndex(object);
    const region = normalizeAnchorRegionName(
        object.region || object.Region
        || object.anchorRegion || object.AnchorRegion || 'Body');
    const headerFooterId = object.headerFooterId || object.HeaderFooterId
        || object.anchorHeaderFooterId || object.AnchorHeaderFooterId || null;
    const tableId = object.tableId || object.TableId
        || object.anchorTableId || object.AnchorTableId || null;
    const cellId = object.cellId || object.CellId
        || object.anchorCellId || object.AnchorCellId || null;
    const columnIndex = normalizeTextExclusionColumnIndex(
        object.columnIndex ?? object.ColumnIndex
        ?? object.anchorColumnIndex ?? object.AnchorColumnIndex);
    const layoutInCell = readObjectLayoutInCell(object);
    let scopeRegion = region;
    let scopeHeaderFooterId = headerFooterId;
    let scopeTableId = tableId;
    let scopeCellId = cellId;
    let scopeColumnIndex = columnIndex;
    if (region === 'TableCell' && layoutInCell === false) {
        scopeRegion = 'Body';
        scopeHeaderFooterId = null;
        scopeTableId = null;
        scopeCellId = null;
        scopeColumnIndex = null;
    }
    const wrapSide = readObjectWrapSide(object);
    const distanceLeft = readObjectDistance(object, 'distanceLeft', 'DistanceLeft');
    const distanceRight = readObjectDistance(object, 'distanceRight', 'DistanceRight');
    const distanceTop = readObjectDistance(object, 'distanceTop', 'DistanceTop');
    const distanceBottom = readObjectDistance(object, 'distanceBottom', 'DistanceBottom');
    let polygon = [];
    const kind = mode === 'TopBottom'
        ? 'fullWidth'
        : (mode === 'Tight' ? 'contour' : (mode === 'Through' ? 'editableContour' : 'rectangular'));
    let candidate = wrapRect;
    if (mode === 'TopBottom' && body) {
        if (!rectOverlapsHorizontallyGeometry(createObjectFootprintRect(object, rect), body)) {
            return null;
        }
        candidate = {
            x: body.x,
            y: wrapRect.y,
            width: body.width,
            height: wrapRect.height,
        };
    } else if (mode === 'Tight' || mode === 'Through') {
        polygon = projectWrapContourPointsForGeometry(object, wrapRect, body);
        candidate = geometryBoundsOfPoints(polygon);
    }
    const exclusionRect = body ? intersectGeometryRect(candidate, body) : candidate;
    if (!exclusionRect || exclusionRect.width <= 0 || exclusionRect.height <= 0) {
        return null;
    }
    return sortObject({
        objectId: object.objectId || object.ObjectId
            || object.blockId || object.BlockId || '',
        blockId: object.blockId || object.BlockId || '',
        pageIndex,
        region: scopeRegion,
        anchorRegion: region,
        scopeKey: createTextExclusionScopeKey(
            pageIndex, scopeRegion, scopeHeaderFooterId,
            scopeTableId, scopeCellId, scopeColumnIndex),
        headerFooterId: scopeHeaderFooterId,
        tableId: scopeTableId,
        cellId: scopeCellId,
        columnIndex: scopeColumnIndex,
        layoutInCell,
        wrapMode: mode,
        wrapSide,
        kind,
        rect: exclusionRect,
        sourceRect: clone(rect),
        wrapRect,
        polygon,
        distanceLeft,
        distanceRight,
        distanceTop,
        distanceBottom,
        captionIncluded: !!object.caption,
        allowOverlap: object.allowOverlap === true,
        zIndex: Number(object.zIndex || 0) || 0,
    });
}
