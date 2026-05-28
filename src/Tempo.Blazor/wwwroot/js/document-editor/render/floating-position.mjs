// Phase D — render/floating-position.mjs
// `computeFloatingPosition(anchor, floating, options)` — place a floating UI element
// (toolbar, popover, autocomplete menu) near an anchor rect, flipping above when the
// space below isn't enough and clamping to the viewport or scroll container.
//
// Pure function — no DOM access. Callers pass rects measured against the viewport
// or scroll container; the function returns `{ left, top, width, height, placement }`.

// Options:
//   gutter (default 8)              — minimum spacing between anchor + floating + viewport edge
//   placement ('top'|'bottom')      — preferred placement (default 'bottom')
//   anchorIsContainerRelative (bool)— if true, anchor rect is relative to a scroll container
//   scrollContainerRect             — the scroll container's viewport rect
//   scrollLeft / scrollTop          — current scroll offset (used when anchor is container-relative)
//   constrainToScrollContainer (bool) — clamp output to scrollContainerRect rather than viewport
//   viewportLeft/Top/Width/Height   — fallback viewport when not constraining to container

export function computeFloatingPosition(anchor, floating, options) {
    const opts = options || {};
    const gutter = Number(opts.gutter || 8) || 8;
    const rect = Object.assign({}, anchor || {});

    if (opts.anchorIsContainerRelative && opts.scrollContainerRect) {
        rect.left = Number(rect.left || 0)
            - Number(opts.scrollLeft || 0)
            + Number(opts.scrollContainerRect.left || 0);
        rect.top = Number(rect.top || 0)
            - Number(opts.scrollTop || 0)
            + Number(opts.scrollContainerRect.top || 0);
    }

    const width = Number((floating && floating.width) || 0) || 0;
    const height = Number((floating && floating.height) || 0) || 0;

    const viewport = opts.constrainToScrollContainer && opts.scrollContainerRect
        ? {
            left: Number(opts.scrollContainerRect.left || 0),
            top: Number(opts.scrollContainerRect.top || 0),
            right: Number(opts.scrollContainerRect.left || 0)
                + Number(opts.scrollContainerRect.width || 0),
            bottom: Number(opts.scrollContainerRect.top || 0)
                + Number(opts.scrollContainerRect.height || 0),
        }
        : {
            left: Number(opts.viewportLeft || 0) || 0,
            top: Number(opts.viewportTop || 0) || 0,
            right: Number(opts.viewportWidth || 0) || 0,
            bottom: Number(opts.viewportHeight || 0) || 0,
        };

    let placement = opts.placement === 'top' ? 'top' : 'bottom';
    let left = Number(rect.left || 0) + Number(rect.width || 0) / 2 - width / 2;
    let top = placement === 'top'
        ? Number(rect.top || 0) - height - gutter
        : Number(rect.top || 0) + Number(rect.height || 0) + gutter;

    // Flip to top if bottom placement would overflow viewport
    if (placement === 'bottom' && top + height > viewport.bottom - gutter) {
        placement = 'top';
        top = Number(rect.top || 0) - height - gutter;
    }

    // Clamp to viewport (with gutter margin)
    left = Math.max(viewport.left + gutter, Math.min(left, viewport.right - gutter - width));
    top = Math.max(viewport.top + gutter, Math.min(top, viewport.bottom - gutter - height));

    return {
        left: Math.round(left),
        top: Math.round(top),
        width,
        height,
        placement,
    };
}
