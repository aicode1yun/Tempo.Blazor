// Phase D — core/marker-store.mjs
// `createMarkerStoreFactory({asArray, clone})` → `createMarkerStore(initialMarkers)`
// — an in-memory store over inline markers (comments/revisions/queries) sorted by
// descending priority. Exposes:
//   .all                 — current markers (kept in sync)
//   .byType(type)        — markers of a type
//   .byBlock(blockId)    — markers whose range starts in a block
//   .overlapping(range)  — markers overlapping a block-local range
//   .transformText(blockId, offset, length, isDelete) — shifts marker offsets after
//       a text insert/delete and returns a clone of the updated list
//   .renderClasses()     — `{id, className, testId}` for each marker
//   .remove(id)          — drops a marker; returns whether anything changed

export function createMarkerStoreFactory(options) {
    const opts = options || {};
    for (const key of ['asArray', 'clone']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createMarkerStoreFactory requires options.${key} (function)`);
        }
    }
    const { asArray, clone } = opts;

    return function createMarkerStore(initialMarkers) {
        let markers = asArray(initialMarkers).map(clone).sort(function (a, b) {
            return Number(b.priority || 0) - Number(a.priority || 0);
        });
        function byType(type) {
            return markers.filter(function (marker) { return marker.type === type; });
        }
        function byBlock(blockId) {
            return markers.filter(function (marker) {
                return marker.range && marker.range.startBlockId === blockId;
            });
        }
        function overlapping(range) {
            return markers.filter(function (marker) {
                const r = marker.range || {};
                return r.startBlockId === range.startBlockId && r.endBlockId === range.endBlockId
                    && Number(r.startOffset || 0) < Number(range.endOffset || 0)
                    && Number(r.endOffset || 0) > Number(range.startOffset || 0);
            });
        }
        function transformText(blockId, offset, length, isDelete) {
            markers = markers.map(function (marker) {
                const cloned = clone(marker);
                const range = cloned.range || {};
                if (range.startBlockId !== blockId) return cloned;
                const delta = Number(length || 0) * (isDelete ? -1 : 1);
                if (offset <= range.startOffset) {
                    range.startOffset = Math.max(0, Number(range.startOffset || 0) + delta);
                }
                if (offset <= range.endOffset) {
                    range.endOffset = Math.max(range.startOffset, Number(range.endOffset || 0) + delta);
                }
                return cloned;
            }).sort(function (a, b) { return Number(b.priority || 0) - Number(a.priority || 0); });
            store.all = markers;
            return markers.map(clone);
        }
        function renderClasses() {
            return markers.map(function (marker) {
                const type = String(marker.type || '')
                    .replace(/[A-Z]/g, function (m) { return '-' + m.toLowerCase(); })
                    .toLowerCase();
                let className = 'tm-wysiwyg-marker tm-wysiwyg-marker--' + type;
                if (marker.type === 'comment' && marker.status) {
                    className += ' tm-document-inline--comment-anchor--' + marker.status;
                }
                if (marker.type === 'revisionDeletion') {
                    className += ' tm-wysiwyg-marker--revision-delete';
                }
                return {
                    id: marker.id,
                    className,
                    testId: marker.type === 'tagQuery'
                        ? 'document-tag-query-marker'
                        : (marker.type === 'slashQuery' ? 'document-slash-query-marker' : ''),
                };
            });
        }
        const store = {
            all: markers,
            byType,
            byBlock,
            overlapping,
            transformText,
            renderClasses,
            remove(id) {
                const before = markers.length;
                markers = markers.filter(function (marker) { return marker.id !== id; });
                store.all = markers;
                return markers.length !== before;
            },
        };
        return store;
    };
}
