// Scroll-spy for TmScrollSpyNav: the active section is the last one whose top has scrolled past the
// offset. A passive scroll listener is used (deterministic + cheap) rather than IntersectionObserver.
//
// The listener goes on whatever actually scrolls. Listening on `window` only works when the document
// itself scrolls; an application shell that puts its content in an `overflow-y: auto` column raises the
// scroll event on that column and never on the window, so a window listener would fire exactly never.
// `scrollRootSelector` names that column, and the offset is then measured from the top of the container
// instead of the top of the viewport.
let dotnet = null;
let ids = [];
let offset = 120;
let selectFirstByDefault = true;
let last = null;
let handler = null;
let target = null;
let rootElement = null;

function rootTop() {
    // Section rects are viewport-relative, so with a scrolling container the comparison has to be moved
    // into the container's own coordinates. Without a container the viewport top (0) is already right.
    return rootElement ? rootElement.getBoundingClientRect().top : 0;
}

function compute() {
    const top = rootTop();
    // With selectFirstByDefault the first section owns the space above every heading, which is what a
    // reader sitting at the top of the page expects. Without it nothing is current until a section's top
    // has genuinely passed the offset.
    let current = selectFirstByDefault && ids.length ? ids[0] : null;
    for (const id of ids) {
        const el = document.getElementById(id);
        if (el && el.getBoundingClientRect().top - top <= offset) {
            current = id;
        }
    }
    if (current && current !== last) {
        last = current;
        dotnet.invokeMethodAsync('SetActiveFromScroll', current);
    }
}

function detach() {
    if (handler && target) {
        target.removeEventListener('scroll', handler);
    }
    handler = null;
    target = null;
    rootElement = null;
}

export function observe(dotnetRef, sectionIds, scrollOffset, scrollRootSelector, autoSelectFirstItem) {
    dotnet = dotnetRef;
    ids = sectionIds || [];
    offset = scrollOffset || 120;
    selectFirstByDefault = autoSelectFirstItem !== false;
    last = null;

    detach();

    // A selector that matches nothing falls back to the window rather than going silent: a mistyped
    // selector then behaves like the released component instead of disabling scroll-spy without a word.
    rootElement = scrollRootSelector ? document.querySelector(scrollRootSelector) : null;
    target = rootElement || window;

    handler = () => compute();
    target.addEventListener('scroll', handler, { passive: true });
    // Initial sync (defer a frame so layout is settled).
    requestAnimationFrame(() => compute());
}

export function unobserve() {
    detach();
    dotnet = null;
    ids = [];
    last = null;
    selectFirstByDefault = true;
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
