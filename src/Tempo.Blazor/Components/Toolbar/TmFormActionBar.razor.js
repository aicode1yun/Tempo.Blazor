// Passive scroll listener that reveals/hides TmFormActionBar past a scroll threshold. Toggles the
// `tm-form-action-bar--visible` class directly on the element (no .NET round-trip per scroll frame).
const DEFAULT_THRESHOLD = 80;

let element = null;
let threshold = DEFAULT_THRESHOLD;
let handler = null;
let ticking = false;

function applyVisibility() {
    if (!element) {
        return;
    }

    const visible = (window.scrollY || window.pageYOffset || 0) > threshold;
    element.classList.toggle('tm-form-action-bar--visible', visible);
    ticking = false;
}

function onScroll() {
    if (ticking) {
        return;
    }

    ticking = true;
    window.requestAnimationFrame(applyVisibility);
}

export function register(el, scrollThreshold) {
    element = el;
    threshold = typeof scrollThreshold === 'number' ? scrollThreshold : DEFAULT_THRESHOLD;

    if (handler) {
        window.removeEventListener('scroll', handler);
    }

    handler = onScroll;
    window.addEventListener('scroll', handler, { passive: true });

    // Evaluate the initial scroll position (e.g. a reload mid-page) on the next frame, once layout settles.
    window.requestAnimationFrame(applyVisibility);
}

export function dispose() {
    if (handler) {
        window.removeEventListener('scroll', handler);
        handler = null;
    }

    element = null;
    ticking = false;
}
