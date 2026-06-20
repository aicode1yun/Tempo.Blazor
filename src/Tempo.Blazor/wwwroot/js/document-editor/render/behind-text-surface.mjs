// Phase D — render/behind-text-surface.mjs
// `createTargetIsBehindTextOverlaySurface({ELEMENT_NODE})` →
//   `targetIsBehindTextOverlaySurface(target)` — true when a pointer target lands
//   on a behind-text object overlay/guides surface (so the editor lets the click
//   fall through to the text under the floating object). Interactive chrome inside
//   the overlay (resize/rotation handles, layout bubble, form controls, buttons)
//   short-circuits to false so those keep handling their own clicks.
// `ELEMENT_NODE` is injected (defaults to 1) to avoid a hard dependency on the
// global `Node` in non-DOM test realms.

export function createTargetIsBehindTextOverlaySurface(options) {
    const opts = options || {};
    const ELEMENT_NODE = typeof opts.ELEMENT_NODE === 'number' ? opts.ELEMENT_NODE : 1;

    return function targetIsBehindTextOverlaySurface(target) {
        const element = target
            && (target.nodeType === ELEMENT_NODE ? target : target.parentElement);
        if (!element || !element.closest) return false;
        if (element.closest('[data-resize-handle], .tm-wysiwyg-object-rotation-handle, .tm-wysiwyg-layout-bubble, button, input, select, textarea')) {
            return false;
        }
        return !!element.closest('.tm-wysiwyg-object-selection-overlay[data-object-layer="behind-text"], .tm-wysiwyg-object-guides-overlay[data-object-layer="behind-text"]');
    };
}
