// Floating layer for the TmUserPicker results list.
//
// The list is `position: fixed`, which takes it out of every ancestor's `overflow` — a modal body or a
// scrolling form column can no longer clip it or scroll it away from its input. Fixed positioning has no
// idea where its input is, so the list is measured against it here, on every scroll (captured, so inner
// scrollers count too) and on resize.
//
// CAVEAT this code has to work around, not one it can remove: an ancestor with a transform, a filter, a
// perspective, `contain`, or a `will-change` naming any of those becomes the containing block for a fixed
// descendant. `.tm-modal` is exactly that — it animates in with `transform: scale(...)`. Inside one,
// `top`/`left` are relative to that box and the list is bounded by it rather than by the viewport, so the
// usable space is measured against that box and the list flips above the input when it would not fit
// below.
//
// A module imported from the same URL is shared by every picker on the page, so all per-list state lives
// in `tracked` and the scroll/resize listeners are bound once for all of them.
//
// `tracked` is keyed by a STRING the component owns, not by the element. Blazor resolves an
// ElementReference through a document query, and by the time a closing list is released its element is
// already detached — the query returns null, so an element-keyed map could never be cleaned up.

const GAP = 4;

// Below this, "there is room below" stops being true in any useful sense and flipping wins. It only
// picks the SIDE: a floor cannot be clamped into max-height, because forcing a height larger than the
// room available does not create room — it pushes the list out of the containing block, where the modal
// clips it. That is the very symptom this layer exists to remove.
const MIN_USABLE_HEIGHT = 96;

// Mirrors max-height on .tm-user-picker__results. Going floating must not silently drop the cap: the
// script overwrites max-height on every placement, so without this the list would be as tall as the
// viewport allows, which the non-floating variant never was.
const MAX_HEIGHT = 240;

const tracked = new Map();
let listenersBound = false;
let frame = 0;

function establishesContainingBlock(cs) {
    return (
        (cs.transform && cs.transform !== 'none') ||
        (cs.perspective && cs.perspective !== 'none') ||
        (cs.filter && cs.filter !== 'none') ||
        (cs.backdropFilter && cs.backdropFilter !== 'none') ||
        /transform|perspective|filter/.test(cs.willChange || '') ||
        /paint|layout|strict|content/.test(cs.contain || '')
    );
}

// The box a fixed descendant is positioned against: its PADDING box, hence the border correction.
// Null means the viewport.
function containingBlockOf(element) {
    let node = element.parentElement;
    while (node) {
        const cs = getComputedStyle(node);
        if (establishesContainingBlock(cs)) {
            const rect = node.getBoundingClientRect();
            return {
                top: rect.top + (parseFloat(cs.borderTopWidth) || 0),
                left: rect.left + (parseFloat(cs.borderLeftWidth) || 0),
                bottom: rect.bottom - (parseFloat(cs.borderBottomWidth) || 0),
            };
        }

        node = node.parentElement;
    }

    return null;
}

function place(entry) {
    const { anchorElement, menuElement, key } = entry;
    if (!anchorElement.isConnected || !menuElement.isConnected) {
        release(key);
        return;
    }

    const anchor = anchorElement.getBoundingClientRect();
    const block = containingBlockOf(menuElement);
    const originTop = block ? block.top : 0;
    const originLeft = block ? block.left : 0;
    const limitTop = block ? Math.max(block.top, 0) : 0;
    const limitBottom = block ? Math.min(block.bottom, window.innerHeight) : window.innerHeight;

    const roomBelow = limitBottom - anchor.bottom - GAP;
    const roomAbove = anchor.top - limitTop - GAP;
    const below = roomBelow >= MIN_USABLE_HEIGHT || roomBelow >= roomAbove;
    const room = Math.min(Math.max(below ? roomBelow : roomAbove, 0), MAX_HEIGHT);

    menuElement.style.width = `${anchor.width}px`;
    menuElement.style.left = `${anchor.left - originLeft}px`;
    menuElement.style.maxHeight = `${room}px`;

    if (below) {
        menuElement.style.top = `${anchor.bottom - originTop + GAP}px`;
    } else {
        // Growing upwards needs the height up front, and scrollHeight is the unclamped one.
        const height = Math.min(menuElement.scrollHeight, room);
        menuElement.style.top = `${anchor.top - originTop - GAP - height}px`;
    }
}

function placeAll() {
    frame = 0;
    for (const entry of [...tracked.values()]) {
        place(entry);
    }
}

function schedule() {
    if (frame) {
        return;
    }

    frame = requestAnimationFrame(placeAll);
}

function bindListeners() {
    if (listenersBound) {
        return;
    }

    // Capture phase: a scroll inside `.tm-modal-body` does not bubble to the window.
    window.addEventListener('scroll', schedule, { passive: true, capture: true });
    window.addEventListener('resize', schedule, { passive: true });
    listenersBound = true;
}

function unbindListeners() {
    if (!listenersBound || tracked.size > 0) {
        return;
    }

    window.removeEventListener('scroll', schedule, { capture: true });
    window.removeEventListener('resize', schedule);
    listenersBound = false;
}

export function anchor(anchorElement, menuElement, key) {
    if (!anchorElement || !menuElement || !key) {
        return;
    }

    const entry = { anchorElement, menuElement, key };
    tracked.set(key, entry);
    bindListeners();
    place(entry);
}

export function release(key) {
    if (!key) {
        return;
    }

    tracked.delete(key);
    unbindListeners();
}
