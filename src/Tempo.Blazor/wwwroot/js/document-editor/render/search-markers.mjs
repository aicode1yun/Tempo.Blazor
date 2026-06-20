// Phase D — render/search-markers.mjs
// `normalizeSearchMarkersForRender(blockIds, offsets, lengths)` — canonical
// search-marker shape consumed by the renderer's overlay layer. Accepts two input
// forms:
//   1. Parallel arrays: `blockIds[i]` is a string and `offsets[i]`/`lengths[i]`
//      carry the range.
//   2. Object array: `blockIds[i]` is `{blockId, offset, length, id?, active?}`
//      (Pascal+camel variants accepted). Pascal alternates `startBlockId/start
//      Offset/textLength/markerId/Active` are also recognised.
// Dropped: rows whose `blockId` is empty or whose `start === end`. Each survivor
// becomes `{id, targetId, type:'search', blockId, active, range}`.

import { asArray, asText } from '../core/helpers.mjs';

export function normalizeSearchMarkersForRender(blockIds, offsets, lengths) {
    const list = asArray(blockIds);
    const offsetList = asArray(offsets);
    const lengthList = asArray(lengths);
    return list.map(function (source, index) {
        const objectSource = source && typeof source === 'object' ? source : null;
        const blockId = objectSource
            ? asText(source.blockId || source.BlockId
                || source.startBlockId || source.StartBlockId || '')
            : asText(source || '');
        const offset = objectSource
            ? Number(source.offset ?? source.Offset
                ?? source.start ?? source.Start
                ?? source.startOffset ?? source.StartOffset ?? 0) || 0
            : Number(offsetList[index] || 0) || 0;
        const length = objectSource
            ? Number(source.length ?? source.Length
                ?? source.textLength ?? source.TextLength ?? 0) || 0
            : Number(lengthList[index] || 0) || 0;
        const id = objectSource
            ? asText(source.id || source.Id
                || source.markerId || source.MarkerId
                || ('search-' + index))
            : ('search-' + index);
        const start = Math.max(0, offset);
        const end = Math.max(start, start + Math.max(0, length));
        if (!blockId || end <= start) return null;
        return {
            id,
            targetId: id,
            type: 'search',
            blockId,
            active: objectSource ? (source.active === true || source.Active === true) : false,
            range: {
                startBlockId: blockId,
                endBlockId: blockId,
                startOffset: start,
                endOffset: end,
            },
        };
    }).filter(Boolean);
}
