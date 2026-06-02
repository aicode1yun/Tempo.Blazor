// Phase D — render/find-layout-object-for-render.mjs
// `findLayoutObjectForRender(layout, objectId, blockId)` — looks up the
// placed object in a *authoritative* layout (one carrying page metrics or a
// `paragraph-layout-*` debug.source tag). Header/footer regions are searched
// too. Matches by `objectId` first; otherwise by `(anchorBlockId, objectId)`
// pair so layouts that intentionally render the same object multiple times
// can still be disambiguated. Non-authoritative layouts (e.g. the legacy
// snapshot) return null so the renderer falls back to its own positioning.

import { asArray, asText } from '../core/helpers.mjs';

export function findLayoutObjectForRender(layout, objectId, blockId) {
    const source = asText(
        (layout && layout.debug && (layout.debug.source || layout.debug.Source)) || '');
    const authoritative = !!(layout
        && (layout.pageMetrics
            || layout.PageMetrics
            || source.indexOf('paragraph-layout') === 0));
    if (!authoritative) return null;
    const id = asText(objectId || '');
    const anchorBlockId = asText(blockId || '');
    let objects = asArray(layout && layout.objects);
    asArray(layout && layout.headerFooterRegions).forEach(function (region) {
        objects = objects.concat(asArray(region && region.objects));
    });
    return objects.find(function (item) {
        if (!item) return false;
        const candidateObjectId = asText(
            item.objectId || item.ObjectId || item.id || item.Id || '');
        if (id && candidateObjectId === id) return true;
        const candidateBlockId = asText(
            item.blockId || item.BlockId
            || item.anchorBlockId || item.AnchorBlockId || '');
        return !!anchorBlockId
            && candidateBlockId === anchorBlockId
            && candidateObjectId === id;
    }) || null;
}
