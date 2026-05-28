// Phase D — render/floating-viewport.mjs
// Resolves the floating-popover viewport bounds, subtracting the toolbar/ribbon
// (top) and the side panel (right). Used to keep popovers like the mini-toolbar
// inside the visible editor area instead of overlapping editor chrome.
//
// Pure of closure state — accepts `win` and `doc` via the options bag so the
// factory can be unit-tested with stubbed DOM. Defaults to `globalThis.window`
// and `globalThis.document` when omitted.

const DEFAULT_GUTTER = 8;
const TOOLBAR_SELECTOR = '[data-testid="document-toolbar"], .tm-document-editor__ribbon';
const SIDE_PANEL_SELECTOR = '[data-testid="document-side-panel"]';
const MIN_WIDTH_AVOIDING_SIDE_PANEL = 320;

function isHidden(style) {
    return style && (style.visibility === 'hidden' || style.display === 'none');
}

export function floatingViewportBoundsAvoidingChrome(options) {
    const opts = options || {};
    const win = opts.win || (typeof globalThis !== 'undefined' ? globalThis.window : null) || {};
    const doc = opts.doc || (typeof globalThis !== 'undefined' ? globalThis.document : null) || {};
    const gutter = Number(opts.gutter ?? DEFAULT_GUTTER) || DEFAULT_GUTTER;

    let width = win.innerWidth
        || (doc.documentElement && doc.documentElement.clientWidth) || 0;
    let height = win.innerHeight
        || (doc.documentElement && doc.documentElement.clientHeight) || 0;
    const left = 0;
    let top = 0;

    if (typeof doc.querySelector === 'function') {
        const toolbar = doc.querySelector(TOOLBAR_SELECTOR);
        if (toolbar && typeof toolbar.getBoundingClientRect === 'function') {
            const toolbarRect = toolbar.getBoundingClientRect();
            const toolbarStyle = typeof win.getComputedStyle === 'function'
                ? win.getComputedStyle(toolbar)
                : null;
            if (toolbarRect.width > 1
                && toolbarRect.height > 1
                && toolbarRect.bottom > 0
                && !isHidden(toolbarStyle)) {
                top = Math.max(top, Math.min(height - 80, toolbarRect.bottom + gutter));
            }
        }
        const panel = doc.querySelector(SIDE_PANEL_SELECTOR);
        if (panel && typeof panel.getBoundingClientRect === 'function') {
            const rect = panel.getBoundingClientRect();
            const style = typeof win.getComputedStyle === 'function'
                ? win.getComputedStyle(panel)
                : null;
            if (rect.width > 1 && rect.height > 1 && !isHidden(style)) {
                width = Math.max(MIN_WIDTH_AVOIDING_SIDE_PANEL, Math.min(width, rect.left - 8));
            }
        }
    }
    return {
        left: left,
        top: top,
        right: width,
        bottom: height,
        width: Math.max(0, width - left),
        height: Math.max(0, height - top),
    };
}

export function floatingViewportWidthAvoidingSidePanel(options) {
    return floatingViewportBoundsAvoidingChrome(options).right;
}
