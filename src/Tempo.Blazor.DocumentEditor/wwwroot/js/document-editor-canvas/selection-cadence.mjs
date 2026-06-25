// Decides the JS->.NET notification cadence for a selection change emitted by the canvas engine.
//
// Two cadences exist (see interop.mjs notifySelectionChanged):
//   * DELIBERATE selections (a placed range or object selection, or any non-collapsed selection) are
//     low-frequency, drive the floating mini toolbar + toolbar pressed-state, and are the anchor for pointer
//     features the user triggers next (ctrl+click a link, right-click a misspelling). They must reach .NET
//     promptly; debouncing them regressed those interactions (Phase9 link, Phase13 spell menu).
//   * A COLLAPSED caret fires on every keystroke while typing and on every arrow-key move. Notifying .NET
//     per key blocks the single WASM thread between keystrokes and batches the glyphs, so it is debounced.
//
// Pure + dependency-free so it can be unit-tested in isolation (no DOM / engine import).
export function isDeliberateSelectionNotification(payload) {
    if (!payload || typeof payload !== 'object') {
        return false;
    }

    // A mini toolbar that wants to be visible is, by construction, a placed range or object selection.
    if (payload.isVisible === true) {
        return true;
    }

    // Any non-collapsed selection is a deliberate range even if it could not be placed on screen
    // (reason: 'canvas-selection-unplaced'); the user still expects the toolbar to reflect it.
    if (payload.selection && payload.selection.isCollapsed === false) {
        return true;
    }

    return false;
}
