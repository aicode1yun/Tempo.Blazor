// Phase D — objects/image-target-helpers.mjs
// `createImageTargetHelpers({normalizeImageObject, imageObjectToLayout,
//   sortObject, unique, asText, affectedParagraphsAroundObject})` →
//   `{cloneImageLayoutForTarget, imageTargetOperationTarget,
//     affectedParagraphsForImageTarget, imageTargetCaption}`.
//
// Shared lookups used by the image runtime command path:
// • cloneImageLayoutForTarget(target) — exports a writable image layout from the
//   model target (drawing only); null for non-drawing kinds.
// • imageTargetOperationTarget(target) — canonical `{blockId, objectId, offset:0,
//   region, headerFooterId, tableId, cellId, columnIndex}` for the operation's
//   `target` field; prefers anchor* scope fields from the normalised image object.
// • affectedParagraphsForImageTarget(model, target, layout) — `[targetBlock,
//   anchorBlock, anchorObjectBlock]` plus paragraphs around the target block, dedup.
// • imageTargetCaption(target) — caption text for drawing targets; '' otherwise.

const REQUIRED = [
    'normalizeImageObject', 'imageObjectToLayout', 'sortObject', 'unique',
    'asText', 'affectedParagraphsAroundObject',
];

export function createImageTargetHelpers(deps) {
    const opts = deps || {};
    for (const key of REQUIRED) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createImageTargetHelpers requires options.${key} (function)`);
        }
    }
    const {
        normalizeImageObject, imageObjectToLayout, sortObject, unique,
        asText, affectedParagraphsAroundObject,
    } = opts;

    function cloneImageLayoutForTarget(target) {
        if (!target) return null;
        if (target.kind === 'drawing') {
            const object = normalizeImageObject(target.run || {}, {
                blockId: target.blockId,
                inlineIndex: target.inlineIndex,
                region: (target.object && (target.object.anchorRegion || target.object.region))
                    || target.region || 'Body',
                headerFooterId: (target.object && (target.object.anchorHeaderFooterId
                    || target.object.headerFooterId))
                    || target.headerFooterId || null,
                tableId: (target.object && (target.object.anchorTableId || target.object.tableId))
                    || target.tableId || null,
                cellId: (target.object && (target.object.anchorCellId || target.object.cellId))
                    || target.cellId || null,
                columnIndex: target.object
                    ? (target.object.anchorColumnIndex ?? target.object.columnIndex
                        ?? target.columnIndex ?? null)
                    : (target.columnIndex ?? null),
            });
            return imageObjectToLayout(object);
        }
        return null;
    }

    function imageTargetOperationTarget(target) {
        return sortObject({
            blockId: (target && target.blockId) || '',
            objectId: (target && target.objectId) || '',
            offset: 0,
            region: (target && target.object && (target.object.anchorRegion || target.object.region))
                || (target && target.region) || null,
            headerFooterId: (target && target.object && (target.object.anchorHeaderFooterId
                || target.object.headerFooterId))
                || (target && target.headerFooterId) || null,
            tableId: (target && target.object && (target.object.anchorTableId || target.object.tableId))
                || (target && target.tableId) || null,
            cellId: (target && target.object && (target.object.anchorCellId || target.object.cellId))
                || (target && target.cellId) || null,
            columnIndex: target && target.object
                ? (target.object.anchorColumnIndex ?? target.object.columnIndex
                    ?? target.columnIndex ?? null)
                : (target ? (target.columnIndex ?? null) : null),
        });
    }

    function affectedParagraphsForImageTarget(model, target, layout) {
        const anchor = (layout && (layout.Anchor || layout.anchor)) || {};
        return unique([
            target && target.blockId,
            anchor.BlockId || anchor.blockId,
            target && target.object && target.object.anchorBlockId,
        ].filter(Boolean).concat(
            affectedParagraphsAroundObject(model, (target && target.blockId) || '')));
    }

    function imageTargetCaption(target) {
        if (!target) return '';
        if (target.kind === 'drawing') {
            return asText((target.run && (target.run.caption ?? target.run.Caption)) || '');
        }
        return '';
    }

    return Object.freeze({
        cloneImageLayoutForTarget,
        imageTargetOperationTarget,
        affectedParagraphsForImageTarget,
        imageTargetCaption,
    });
}
