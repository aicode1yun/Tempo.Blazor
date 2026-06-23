// Phase D — render/mini-toolbar-predicate.mjs
// `shouldShowMiniToolbarForSelectionSnapshot(selection)` — decides whether the
// floating mini-toolbar (bold/italic/etc. chip near the selection) should appear.
//
// Rules: only for non-collapsed text selections, never for object selections.
// Accepts either a raw selection snapshot or a Selection-like object — the caller
// is expected to normalise via createSelectionSnapshot before invoking.
//
// Factory-style: takes the snapshot normaliser as an injected dependency so the
// predicate can be unit-tested without bringing in the full selection module.

export function createMiniToolbarPredicate(options) {
    const opts = options || {};
    if (typeof opts.createSelectionSnapshot !== 'function') {
        throw new TypeError(
            'createMiniToolbarPredicate requires options.createSelectionSnapshot (function)');
    }
    const { createSelectionSnapshot } = opts;

    function shouldShowMiniToolbarForSelectionSnapshot(selection) {
        const snapshot = createSelectionSnapshot(selection || {});
        return snapshot.isCollapsed === false
            && snapshot.isObjectSelection !== true
            && !snapshot.activeObjectId
            && !snapshot.objectId;
    }

    return Object.freeze({ shouldShowMiniToolbarForSelectionSnapshot });
}
