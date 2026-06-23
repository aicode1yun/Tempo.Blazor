// Phase D — render/markers-for-block.mjs
// `createMarkersForBlock({refreshRuntimeMarkerStore, asArray})` →
//   `{commentMarkersForBlock, revisionMarkersForBlock, searchMarkersForBlock}`.
// Each helper returns the markers whose range starts or ends in the given block:
//   • commentMarkersForBlock — `markerStore.byType('comment')`; lazily refreshes
//     the store when the instance has none yet.
//   • revisionMarkersForBlock — `markerStore.all` filtered to `type` starting
//     with `'revision'`; same lazy refresh.
//   • searchMarkersForBlock — `inst.searchMarkers` (also matches `marker.blockId`).

export function createMarkersForBlock(options) {
    const opts = options || {};
    if (typeof opts.refreshRuntimeMarkerStore !== 'function') {
        throw new TypeError(
            'createMarkersForBlock requires options.refreshRuntimeMarkerStore (function)');
    }
    if (typeof opts.asArray !== 'function') {
        throw new TypeError(
            'createMarkersForBlock requires options.asArray (function)');
    }
    const { refreshRuntimeMarkerStore, asArray } = opts;

    function commentMarkersForBlock(inst, blockId) {
        if (!inst || !inst.markerStore) refreshRuntimeMarkerStore(inst);
        return asArray(inst && inst.markerStore && inst.markerStore.byType('comment'))
            .filter(function (marker) {
                const range = marker.range || {};
                return range.startBlockId === blockId || range.endBlockId === blockId;
            });
    }

    function revisionMarkersForBlock(inst, blockId) {
        if (!inst || !inst.markerStore) refreshRuntimeMarkerStore(inst);
        return asArray(inst && inst.markerStore && inst.markerStore.all)
            .filter(function (marker) {
                const range = marker.range || {};
                return String(marker.type || '').indexOf('revision') === 0
                    && (range.startBlockId === blockId || range.endBlockId === blockId);
            });
    }

    function searchMarkersForBlock(inst, blockId) {
        return asArray(inst && inst.searchMarkers).filter(function (marker) {
            const range = (marker && marker.range) || {};
            return range.startBlockId === blockId
                || range.endBlockId === blockId
                || marker.blockId === blockId;
        });
    }

    return Object.freeze({
        commentMarkersForBlock,
        revisionMarkersForBlock,
        searchMarkersForBlock,
    });
}
