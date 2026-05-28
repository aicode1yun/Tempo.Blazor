// Phase D — render/rect-helpers.mjs
// Pure rectangle helpers that accept "any" shape (Pascal-case, camel-case, DOMRect-style
// with left/top). They're the glue between Blazor-side rectangles (Pascal) and
// browser rectangles (left/top/right/bottom) and feed UI hit-testing.

export function rectFromAny(rect) {
    const r = rect || {};
    return {
        X: Number(r.X ?? r.x ?? r.left ?? 0) || 0,
        Y: Number(r.Y ?? r.y ?? r.top ?? 0) || 0,
        Width: Number(r.Width ?? r.width ?? 0) || 0,
        Height: Number(r.Height ?? r.height ?? 0) || 0,
    };
}

export function rectContains(rect, x, y) {
    const r = rectFromAny(rect);
    return x >= r.X && x <= r.X + r.Width && y >= r.Y && y <= r.Y + r.Height;
}
