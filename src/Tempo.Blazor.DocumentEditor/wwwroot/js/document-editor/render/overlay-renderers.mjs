// Phase D — render/overlay-renderers.mjs
// DOM-mutating overlay renderers used by the atomic renderer's frame pipeline.
//
// `createOverlayRenderers({document, markOverlayNonText, asArray})` factory →
//   `{renderSelectionOverlay, renderRevisionOverlay, renderCommentMarkers,
//     restoreLogicalSelection}`.
// • `renderSelectionOverlay(snapshot)` — `<div>` with selection metadata marker
//   when the snapshot's `selection.blockId` is present.
// • `renderRevisionOverlay(snapshot)` — one `<span>` per `snapshot.model.revisions`
//   item with the canonical `data-revision-id`/`-type` attributes.
// • `renderCommentMarkers(snapshot)` — same pattern for `snapshot.model.comments`.
// • `restoreLogicalSelection(root, selection)` — writes the JSON-encoded selection
//   to `root.dataset.logicalSelection` (via `setAttribute`), used by the renderer
//   to round-trip the logical selection across atomic swaps.
//
// All overlay nodes are flagged as `aria-hidden` + `data-text-probe-ignore` via
// `markOverlayNonText`.

export function createOverlayRenderers(options) {
    const opts = options || {};
    if (!opts.document || typeof opts.document.createElement !== 'function') {
        throw new TypeError(
            'createOverlayRenderers requires options.document (with createElement)');
    }
    if (typeof opts.markOverlayNonText !== 'function') {
        throw new TypeError(
            'createOverlayRenderers requires options.markOverlayNonText (function)');
    }
    if (typeof opts.asArray !== 'function') {
        throw new TypeError(
            'createOverlayRenderers requires options.asArray (function)');
    }
    if (typeof opts.sortObject !== 'function') {
        throw new TypeError(
            'createOverlayRenderers requires options.sortObject (function)');
    }
    const { document: doc, markOverlayNonText, asArray, sortObject } = opts;

    function baseOverlay(name) {
        const overlay = markOverlayNonText(doc.createElement('div'));
        overlay.setAttribute('data-render-overlay', name);
        overlay.style.position = 'absolute';
        overlay.style.inset = '0';
        overlay.style.pointerEvents = 'none';
        return overlay;
    }

    function renderSelectionOverlay(snapshot) {
        const overlay = baseOverlay('selection');
        const selection = snapshot && snapshot.selection;
        if (selection && selection.blockId) {
            const marker = markOverlayNonText(doc.createElement('span'));
            marker.setAttribute('data-selection-block-id', selection.blockId);
            marker.setAttribute('data-selection-offset', selection.offset || 0);
            overlay.appendChild(marker);
        }
        return overlay;
    }

    function renderRevisionOverlay(snapshot) {
        const overlay = baseOverlay('revision');
        overlay.className = 'tm-render-revision-overlay';
        asArray(snapshot && snapshot.model && snapshot.model.revisions)
            .forEach(function (revision) {
                const id = revision.id || revision.Id;
                if (!id) return;
                const marker = markOverlayNonText(doc.createElement('span'));
                const type = revision.type || revision.Type || '';
                marker.className = 'tm-render-revision-marker revision-overlay';
                marker.setAttribute('data-testid', 'document-revision-marker');
                marker.setAttribute('data-revision-id', id);
                marker.setAttribute('data-revision-type', type);
                marker.textContent = '';
                overlay.appendChild(marker);
            });
        return overlay;
    }

    function renderCommentMarkers(snapshot) {
        const overlay = baseOverlay('comments');
        asArray(snapshot && snapshot.model && snapshot.model.comments)
            .forEach(function (comment) {
                const id = comment.id || comment.Id;
                if (!id) return;
                const marker = markOverlayNonText(doc.createElement('span'));
                marker.className = 'tm-render-comment-marker';
                marker.setAttribute('data-testid', 'document-comment-marker');
                marker.setAttribute('data-comment-id', id);
                marker.textContent = '';
                overlay.appendChild(marker);
            });
        return overlay;
    }

    function restoreLogicalSelection(root, selection) {
        if (!root) return;
        root.setAttribute(
            'data-logical-selection',
            JSON.stringify(sortObject(selection || {})));
    }

    return Object.freeze({
        renderSelectionOverlay,
        renderRevisionOverlay,
        renderCommentMarkers,
        restoreLogicalSelection,
    });
}
