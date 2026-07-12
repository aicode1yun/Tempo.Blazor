// Scroll-spy for TmScrollSpyNav: the active section is the last one whose top has scrolled past the
// offset. A passive scroll listener is used (deterministic + cheap) rather than IntersectionObserver.
let dotnet = null;
let ids = [];
let offset = 120;
let last = null;
let handler = null;

function compute() {
    let current = ids.length ? ids[0] : null;
    for (const id of ids) {
        const el = document.getElementById(id);
        if (el && el.getBoundingClientRect().top <= offset) {
            current = id;
        }
    }
    if (current && current !== last) {
        last = current;
        dotnet.invokeMethodAsync('SetActiveFromScroll', current);
    }
}

export function observe(dotnetRef, sectionIds, scrollOffset) {
    dotnet = dotnetRef;
    ids = sectionIds || [];
    offset = scrollOffset || 120;
    last = null;
    if (handler) {
        window.removeEventListener('scroll', handler);
    }
    handler = () => compute();
    window.addEventListener('scroll', handler, { passive: true });
    // Initial sync (defer a frame so layout is settled).
    requestAnimationFrame(() => compute());
}

export function unobserve() {
    if (handler) {
        window.removeEventListener('scroll', handler);
        handler = null;
    }
    dotnet = null;
    ids = [];
    last = null;
}

export function scrollTo(id) {
    const el = document.getElementById(id);
    if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
}

export function dispose() {
    unobserve();
}
