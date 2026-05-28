// Phase D — render/page-finder.mjs
// `pageIndexFromPoint(root, x, y)` — given an editor root and a viewport-relative
// point, returns the index of the page that contains the point (or, if none does,
// the nearest page by Euclidean distance). Returns `null` when no pages exist.
//
// Pure of closure state — only requires `root.querySelectorAll` + each page's
// `getBoundingClientRect()`. Accepts the same rect shapes as the layout pipeline
// via `rectFromGeometry` (camel-case `{x,y,width,height}` shape).

import { rectFromGeometry } from '../objects/geometry.mjs';

const PAGE_SELECTOR = '.tm-wysiwyg-page[data-page-index]';
const INSIDE_BONUS = -100000;

export function pageIndexFromPoint(root, x, y) {
    if (!root || typeof root.querySelectorAll !== 'function') return null;
    let best = null;
    Array.from(root.querySelectorAll(PAGE_SELECTOR)).forEach(function (page) {
        if (!page || typeof page.getBoundingClientRect !== 'function') return;
        const rect = rectFromGeometry(page.getBoundingClientRect());
        const insideX = x >= rect.x && x <= rect.x + rect.width;
        const insideY = y >= rect.y && y <= rect.y + rect.height;
        const dx = insideX ? 0 : Math.min(
            Math.abs(x - rect.x), Math.abs(x - (rect.x + rect.width)));
        const dy = insideY ? 0 : Math.min(
            Math.abs(y - rect.y), Math.abs(y - (rect.y + rect.height)));
        const score = (insideX && insideY ? INSIDE_BONUS : 0) + Math.sqrt(dx * dx + dy * dy);
        if (!best || score < best.score) {
            best = {
                pageIndex: Number(page.getAttribute('data-page-index') || 0) || 0,
                score: score,
            };
        }
    });
    return best ? best.pageIndex : null;
}
