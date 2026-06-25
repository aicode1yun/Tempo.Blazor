// Phase D — objects/image-move-track.mjs
// Helpers for the image drag/resize ("move track") preview lifecycle.
//
// `captureObjectPointerPreviewNodeState(node)` — snapshots a preview node's inline
//   style (transform/width/height/minHeight) + track data attributes so they can
//   be restored if the drag is cancelled.
// `restoreObjectPointerPreviewNodeState(state)` — reverts a node to the captured
//   state, removing the predrag/active track classes and restoring/clearing the
//   `data-track-*` attributes.
// `createSerializeImageMoveTrack({clone, sortObject, asArray})` →
//   `serializeImageMoveTrack(track)` — deterministic, JSON-friendly snapshot of a
//   move-track session (mode/stage/rects/deltas/preview state/guides). Returns null
//   for a null track.
// `createReadImageMoveTrackOriginalRect({asArray, rectFromGeometry})` →
//   `readImageMoveTrackOriginalRect(session)` — original on-screen rect of the
//   dragged object: the first node's bounding rect when available, else derived
//   from the object's position offsets + size (min 1).

export function captureObjectPointerPreviewNodeState(node) {
    const style = (node && node.style) || {};
    return {
        node,
        transform: style.transform || '',
        width: style.width || '',
        height: style.height || '',
        minHeight: style.minHeight || '',
        trackState: (node && node.getAttribute && node.getAttribute('data-track-state')) || '',
        trackDx: (node && node.getAttribute && node.getAttribute('data-track-dx')) || '',
        trackDy: (node && node.getAttribute && node.getAttribute('data-track-dy')) || '',
    };
}

export function restoreObjectPointerPreviewNodeState(state) {
    const node = state && state.node;
    if (!node || !node.style) return;
    node.style.transform = state.transform || '';
    node.style.width = state.width || '';
    node.style.height = state.height || '';
    node.style.minHeight = state.minHeight || '';
    if (node.classList) {
        node.classList.remove('tm-wysiwyg-object-track--predrag');
        node.classList.remove('tm-wysiwyg-object-track--active');
    }
    if (node.setAttribute && state.trackState) node.setAttribute('data-track-state', state.trackState);
    else if (node.removeAttribute) node.removeAttribute('data-track-state');
    if (node.setAttribute && state.trackDx) node.setAttribute('data-track-dx', state.trackDx);
    else if (node.removeAttribute) node.removeAttribute('data-track-dx');
    if (node.setAttribute && state.trackDy) node.setAttribute('data-track-dy', state.trackDy);
    else if (node.removeAttribute) node.removeAttribute('data-track-dy');
}

export function createSerializeImageMoveTrack(options) {
    const opts = options || {};
    for (const key of ['clone', 'sortObject']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createSerializeImageMoveTrack requires options.${key} (function)`);
        }
    }
    const { clone, sortObject } = opts;
    return function serializeImageMoveTrack(track) {
        if (!track) return null;
        return sortObject({
            objectId: track.objectId || '',
            blockId: track.blockId || '',
            mode: track.mode || 'drag',
            stage: track.stage || 'predrag',
            active: track.active === true,
            cancelled: track.cancelled === true,
            committed: track.committed === true,
            handleName: track.handleName || '',
            handleIndex: Number(track.handleIndex ?? -1),
            threshold: Number(track.threshold || 0) || 0,
            originalRect: clone(track.originalRect || null),
            originalLayout: clone(track.originalLayout || null),
            originalTransform: clone(track.originalTransform || null),
            fixedPoint: clone(track.fixedPoint || null),
            aspectRatio: Number(track.aspectRatio || 0) || 0,
            lockAspectRatio: track.lockAspectRatio !== false,
            resizeBounds: clone(track.resizeBounds || null),
            pointerStart: clone(track.pointerStart || null),
            currentDelta: clone(track.currentDelta || { x: 0, y: 0 }),
            appliedDelta: clone(track.appliedDelta || { x: 0, y: 0 }),
            previewWidth: track.previewWidth === undefined ? null : Number(track.previewWidth || 0),
            previewHeight: track.previewHeight === undefined ? null : Number(track.previewHeight || 0),
            previewPreserveAspectRatio: track.previewPreserveAspectRatio === true,
            previewRect: clone(track.previewRect || null),
            previewWrapRect: clone(track.previewWrapRect || null),
            previewExclusion: clone(track.previewExclusion || null),
            previewIntervals: clone(track.previewIntervals || null),
            resizeBadgeText: track.resizeBadgeText || '',
            guides: clone(track.guides || []),
        });
    };
}

export function createReadImageMoveTrackOriginalRect(options) {
    const opts = options || {};
    for (const key of ['asArray', 'rectFromGeometry']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createReadImageMoveTrackOriginalRect requires options.${key} (function)`);
        }
    }
    const { asArray, rectFromGeometry } = opts;
    return function readImageMoveTrackOriginalRect(session) {
        const node = session && asArray(session.nodes)[0];
        if (node && typeof node.getBoundingClientRect === 'function') {
            return rectFromGeometry(node.getBoundingClientRect());
        }
        const object = (session && session.object) || {};
        return {
            x: Number((object.horizontalPosition && object.horizontalPosition.offset) || 0) || 0,
            y: Number((object.verticalPosition && object.verticalPosition.offset) || 0) || 0,
            width: Math.max(1, Number(object.width || 120) || 120),
            height: Math.max(1, Number(object.height || 80) || 80),
        };
    };
}
