// Phase D — objects/anchored-drawing-layout.mjs
// `createAnchoredDrawingResolvers({findLayoutBlockById, findReferenceLineForOffset,
// drawingLayerForWrapMode, wrapModeCreatesTextExclusion, readObjectLayoutInCell,
// normalizeTextExclusionColumnIndex, resolveAnchoredDrawingRect})` factory →
//   `resolveAnchoredDrawingReference(object, layoutBlocks, fragments, fallback)`
//     — locates the reference rect for an anchored drawing (paragraph line or full
//     paragraph), falling back to a synthetic 18-tall body-frame band when the
//     anchor is unresolved. Returns scope metadata + `usedFallback` + `fallbackReason`.
//   `createAnchoredDrawingLayoutObject(block, entry, reference, page)`
//     — materialises a layout object for a paragraph-anchored drawing: positions it
//     via `resolveAnchoredDrawingRect`, fills in scope/region/page-index, and tags
//     wrap-mode-derived layer + `createsTextExclusion`.

import { asText, clone, sortObject } from '../core/helpers.mjs';

export function createAnchoredDrawingResolvers(options) {
    const opts = options || {};
    for (const dep of [
        'findLayoutBlockById',
        'findReferenceLineForOffset',
        'drawingLayerForWrapMode',
        'wrapModeCreatesTextExclusion',
        'readObjectLayoutInCell',
        'normalizeTextExclusionColumnIndex',
        'resolveAnchoredDrawingRect',
    ]) {
        if (typeof opts[dep] !== 'function') {
            throw new TypeError(
                `createAnchoredDrawingResolvers requires options.${dep} (function)`);
        }
    }
    const {
        findLayoutBlockById,
        findReferenceLineForOffset,
        drawingLayerForWrapMode,
        wrapModeCreatesTextExclusion,
        readObjectLayoutInCell,
        normalizeTextExclusionColumnIndex,
        resolveAnchoredDrawingRect,
    } = opts;

    function resolveAnchoredDrawingReference(object, layoutBlocks, fragments, fallback) {
        const source = object || {};
        const fallbackInfo = fallback || {};
        const anchorBlockId = asText(
            source.anchorBlockId || source.blockId || fallbackInfo.blockId || '');
        const candidates = [].concat(fragments || []).concat(layoutBlocks || []);
        const layoutBlock = findLayoutBlockById(candidates, anchorBlockId)
            || findLayoutBlockById(candidates, fallbackInfo.blockId)
            || null;
        let pageIndex = Number(fallbackInfo.pageIndex || 0) || 0;
        const bodyFrame = fallbackInfo.bodyFrame
            || { x: 0, y: 0, width: 640, height: 900 };
        let rect = {
            x: bodyFrame.x,
            y: Number(fallbackInfo.y ?? bodyFrame.y ?? 0) || 0,
            width: bodyFrame.width,
            height: 18,
        };
        let line = null;
        let usedFallback = true;
        if (layoutBlock) {
            pageIndex = Number(layoutBlock.pageIndex ?? pageIndex) || 0;
            line = findReferenceLineForOffset(layoutBlock, source.anchorOffset);
            rect = clone((line && line.rect) || layoutBlock.rect || rect);
            usedFallback = false;
        }
        if (source.fixedOnPage === true) {
            rect = {
                x: bodyFrame.x,
                y: bodyFrame.y,
                width: bodyFrame.width,
                height: Math.max(1, Number(bodyFrame.height || rect.height || 18) || 18),
            };
        }
        return sortObject({
            blockId: anchorBlockId || fallbackInfo.blockId || '',
            lineId: (line && line.id) || null,
            pageIndex,
            region: source.anchorRegion || 'Body',
            headerFooterId: source.anchorHeaderFooterId
                || fallbackInfo.headerFooterId || null,
            tableId: source.anchorTableId || fallbackInfo.tableId || null,
            cellId: source.anchorCellId || fallbackInfo.cellId || null,
            columnIndex: normalizeTextExclusionColumnIndex(
                source.anchorColumnIndex ?? source.columnIndex
                ?? fallbackInfo.columnIndex),
            rect,
            usedFallback,
            fallbackReason: usedFallback ? 'paragraph-start' : '',
        });
    }

    function createAnchoredDrawingLayoutObject(block, entry, reference, page) {
        const object = clone((entry && entry.object) || {});
        const rect = resolveAnchoredDrawingRect(object, reference, page);
        let inlineIndex = object.anchorInlineIndex;
        if (inlineIndex === undefined || inlineIndex === null) {
            inlineIndex = entry && entry.inlineIndex;
        }
        let objectPageIndex = reference && reference.pageIndex;
        if (objectPageIndex === undefined || objectPageIndex === null) {
            objectPageIndex = page && page.pageIndex;
        }
        object.blockId = (block && block.id) || object.blockId || '';
        object.anchorBlockId = (reference && reference.blockId)
            || object.anchorBlockId || object.blockId || '';
        object.anchorInlineIndex = Number(inlineIndex ?? -1);
        object.anchorOffset = Number(object.anchorOffset || 0) || 0;
        object.inlineObject = false;
        object.createsTextExclusion = wrapModeCreatesTextExclusion(object.wrapMode);
        object.isInline = false;
        object.isAnchored = object.layoutKind !== 'Fixed';
        object.pageIndex = Number(objectPageIndex ?? 0) || 0;
        object.region = (reference && reference.region) || object.anchorRegion || 'Body';
        object.headerFooterId = (reference && reference.headerFooterId)
            || object.anchorHeaderFooterId || object.headerFooterId || null;
        object.tableId = (reference && reference.tableId)
            || object.anchorTableId || object.tableId || null;
        object.cellId = (reference && reference.cellId)
            || object.anchorCellId || object.cellId || null;
        object.columnIndex = normalizeTextExclusionColumnIndex(
            (reference ? reference.columnIndex : null)
            ?? object.anchorColumnIndex ?? object.columnIndex);
        object.anchorColumnIndex = normalizeTextExclusionColumnIndex(
            object.anchorColumnIndex ?? object.columnIndex);
        object.layoutInCell = readObjectLayoutInCell(object);
        object.layer = drawingLayerForWrapMode(object.wrapMode);
        object.rect = rect;
        object.referenceRect = (reference && reference.rect)
            ? clone(reference.rect)
            : null;
        object.anchorFallback = reference && reference.usedFallback === true;
        object.anchorFallbackReason = (reference && reference.fallbackReason) || '';
        return sortObject(object);
    }

    return Object.freeze({
        resolveAnchoredDrawingReference,
        createAnchoredDrawingLayoutObject,
    });
}
