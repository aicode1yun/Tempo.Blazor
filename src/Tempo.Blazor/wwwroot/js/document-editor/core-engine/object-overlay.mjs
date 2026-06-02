// Phase R.4.6 — core-engine/object-overlay.mjs
// App-drawn floating-image overlay for the model-owned surface. The paragraph engine
// already lays out anchored/floating drawings (they appear in `layout.objects` with a
// resolved rect + wrapMode, and text exclusions make body text wrap around them). This
// module paints those objects as positioned <figure> elements with a real <img>, an
// optional selection box, and 8 resize handles — the same overlay approach used for the
// caret/selection (R.4.3), so the atomic renderer stays untouched.
//
//   createObjectElement({ doc, object, rect, selected }) → <figure> (page-local coords)
//   objectHitTest(objects, x, y) → objectId | null   (topmost object containing the point)
//   RESIZE_HANDLES                                    → the 8 handle names

import { asArray } from '../core/helpers.mjs';

export const RESIZE_HANDLES = ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'];

const HANDLE_SIZE = 10;

function num(v) { return Number(v || 0) || 0; }

// Page-local position of a handle given the object box size.
function handlePosition(name, w, h) {
    const half = HANDLE_SIZE / 2;
    const cx = w / 2; const cy = h / 2;
    switch (name) {
        case 'nw': return { left: -half, top: -half };
        case 'n': return { left: cx - half, top: -half };
        case 'ne': return { left: w - half, top: -half };
        case 'e': return { left: w - half, top: cy - half };
        case 'se': return { left: w - half, top: h - half };
        case 's': return { left: cx - half, top: h - half };
        case 'sw': return { left: -half, top: h - half };
        case 'w': return { left: -half, top: cy - half };
        default: return { left: 0, top: 0 };
    }
}

export function createObjectElement(options) {
    const opts = options || {};
    const doc = opts.doc || globalThis.document;
    const object = opts.object || {};
    const rect = opts.rect || object.rect || {};
    const selected = opts.selected === true;
    const w = Math.max(1, num(rect.width));
    const h = Math.max(1, num(rect.height));

    const fig = doc.createElement('figure');
    fig.className = 'tm-core-object' + (selected ? ' tm-core-object--selected' : '');
    fig.setAttribute('data-testid', 'core-engine-object');
    fig.setAttribute('data-object-id', object.objectId || object.id || '');
    fig.setAttribute('data-wrap-mode', object.wrapMode || '');
    fig.setAttribute('role', 'figure');
    fig.setAttribute('aria-label', object.altText || object.caption || 'Image');
    fig.style.position = 'absolute';
    fig.style.margin = '0';
    fig.style.left = num(rect.x) + 'px';
    fig.style.top = num(rect.y) + 'px';
    fig.style.width = w + 'px';
    fig.style.height = h + 'px';
    fig.style.zIndex = String(num(object.zIndex) || 5);
    fig.style.outline = selected ? '1px solid #2563eb' : 'none';

    if (object.url) {
        const img = doc.createElement('img');
        img.setAttribute('data-testid', 'core-engine-object-img');
        img.setAttribute('src', object.url);
        img.setAttribute('alt', object.altText || '');
        img.setAttribute('draggable', 'false');
        img.style.width = '100%';
        img.style.height = '100%';
        img.style.objectFit = 'fill';
        img.style.pointerEvents = 'none';
        img.style.display = 'block';
        fig.appendChild(img);
    }

    // R.4.8 inspector — visible caption below the image.
    if (object.caption) {
        const cap = doc.createElement('figcaption');
        cap.setAttribute('data-testid', 'core-engine-object-caption');
        cap.textContent = String(object.caption);
        cap.style.position = 'absolute';
        cap.style.left = '0';
        cap.style.top = '100%';
        cap.style.width = '100%';
        cap.style.fontSize = '12px';
        cap.style.textAlign = 'center';
        cap.style.color = '#555';
        cap.style.pointerEvents = 'none';
        fig.appendChild(cap);
    }

    if (selected) {
        RESIZE_HANDLES.forEach(function (name) {
            const handle = doc.createElement('span');
            handle.className = 'tm-core-object-handle tm-core-object-handle--' + name;
            handle.setAttribute('data-resize-handle', name);
            handle.setAttribute('data-testid', 'core-engine-resize-handle-' + name);
            const pos = handlePosition(name, w, h);
            handle.style.position = 'absolute';
            handle.style.left = pos.left + 'px';
            handle.style.top = pos.top + 'px';
            handle.style.width = HANDLE_SIZE + 'px';
            handle.style.height = HANDLE_SIZE + 'px';
            handle.style.background = '#fff';
            handle.style.border = '1px solid #2563eb';
            handle.style.boxSizing = 'border-box';
            handle.style.zIndex = '30';
            fig.appendChild(handle);
        });
    }
    return fig;
}

// Topmost (highest z-index, then last) object whose rect contains the layout-space point.
export function objectHitTest(objects, x, y) {
    let best = null; let bestZ = -Infinity;
    asArray(objects).forEach(function (obj) {
        const r = obj.rect || {};
        const left = num(r.x); const top = num(r.y);
        if (x >= left && x <= left + num(r.width) && y >= top && y <= top + num(r.height)) {
            const z = num(obj.zIndex);
            if (z >= bestZ) { bestZ = z; best = obj; }
        }
    });
    return best ? (best.objectId || best.id || null) : null;
}

// New box size when dragging `handle` by (dx,dy) in layout space from a start rect.
// Keeps the opposite edge fixed (the engine re-resolves the anchor position on relayout).
export function resizeRectByHandle(startRect, handle, dx, dy, opts) {
    const o = opts || {};
    const minW = num(o.minWidth) || 32;
    const minH = num(o.minHeight) || 24;
    let w = Math.max(1, num(startRect.width));
    let h = Math.max(1, num(startRect.height));
    const name = String(handle || '');
    if (name.indexOf('e') !== -1) w = w + dx;
    if (name.indexOf('w') !== -1) w = w - dx;
    if (name.indexOf('s') !== -1) h = h + dy;
    if (name.indexOf('n') !== -1) h = h - dy;
    w = Math.max(minW, w);
    h = Math.max(minH, h);
    if (o.preserveAspect && num(startRect.width) > 0) {
        const ratio = num(startRect.height) / num(startRect.width);
        // Drive height from width for corner handles; from height for edge handles.
        if (name.length === 2) h = Math.max(minH, w * ratio);
        else if (name === 'n' || name === 's') w = Math.max(minW, h / ratio);
        else h = Math.max(minH, w * ratio);
    }
    return { width: Math.round(w), height: Math.round(h) };
}
