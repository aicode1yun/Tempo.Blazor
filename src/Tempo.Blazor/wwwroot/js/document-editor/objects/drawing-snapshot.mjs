// Phase D — objects/drawing-snapshot.mjs
// `createDrawingObjectSnapshotFactory({normalizeImageObject})` factory →
// `createDrawingObjectSnapshot(entry)` — turns an index entry (`{run, blockId,
// inlineIndex, inlineId, region, headerFooterId, tableId, cellId, objectId}`)
// into the canonical snapshot consumed by selection-restore + object-overlay code.

import { sortObject } from '../core/helpers.mjs';
import { normalizeDrawingKindName } from './drawing-kind.mjs';

export function createDrawingObjectSnapshotFactory(options) {
    const opts = options || {};
    if (typeof opts.normalizeImageObject !== 'function') {
        throw new TypeError(
            'createDrawingObjectSnapshotFactory requires options.normalizeImageObject (function)');
    }
    const { normalizeImageObject } = opts;

    return function createDrawingObjectSnapshot(entry) {
        if (!entry) return null;
        const run = entry.run || {};
        const layout = normalizeImageObject(run, {
            blockId: entry.blockId,
            inlineIndex: entry.inlineIndex,
            region: entry.region || 'Body',
            headerFooterId: entry.headerFooterId || null,
            tableId: entry.tableId || null,
            cellId: entry.cellId || null,
        });
        return sortObject({
            objectId: layout.objectId || entry.objectId || run.objectId || run.id || '',
            runId: entry.inlineId || run.id || null,
            blockId: entry.blockId || layout.blockId || '',
            region: layout.anchorRegion || entry.region || 'Body',
            headerFooterId: layout.anchorHeaderFooterId || entry.headerFooterId || null,
            tableId: layout.anchorTableId || entry.tableId || null,
            cellId: layout.anchorCellId || entry.cellId || null,
            inlineIndex: Number(entry.inlineIndex ?? layout.anchorInlineIndex ?? -1),
            drawingKind: normalizeDrawingKindName(
                run.drawingKind || run.DrawingKind || 'Image'),
            source: run.source ?? run.Source ?? 0,
            url: run.url ?? run.Url ?? null,
            assetId: run.assetId ?? run.AssetId ?? null,
            altText: run.altText || run.AltText || '',
            caption: run.caption || run.Caption || '',
            layoutKind: layout.layoutKind,
            isInline: layout.isInline === true,
            isAnchored: layout.isAnchored === true,
            anchorBlockId: layout.anchorBlockId || entry.blockId || '',
            anchorOffset: Number(layout.anchorOffset || 0) || 0,
            anchorInlineIndex: Number(layout.anchorInlineIndex ?? entry.inlineIndex ?? -1),
            anchorRegion: layout.anchorRegion || entry.region || 'Body',
            anchorHeaderFooterId: layout.anchorHeaderFooterId || entry.headerFooterId || '',
            anchorTableId: layout.anchorTableId || entry.tableId || '',
            anchorCellId: layout.anchorCellId || entry.cellId || '',
            wrapMode: layout.wrapMode,
            width: layout.width,
            height: layout.height,
            zIndex: layout.zIndex,
            layout: layout,
        });
    };
}
