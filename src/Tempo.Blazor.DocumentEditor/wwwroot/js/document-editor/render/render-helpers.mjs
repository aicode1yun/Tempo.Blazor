// Phase D — render/render-helpers.mjs
// Pure helpers used by the atomic renderer.
//
// `domRectToRect(rect)` — normalises a DOMRect-like (or any rect with
//   `x|left` / `y|top` / `width` / `height`) into `{x, y, width, height}`
//   defaulting missing fields to 0.
// `rectsOverlap(a, b)` — strict overlap test for two `{x,y,width,height}` rects.
// `hasRevisionRun(block)` — true when any inline run on the block has a non-empty
//   `revisionId`; null-safe.
// `scopeIncludesBlock(scope, blockId, scopeKinds)` — true when the layout scope
//   covers `blockId`. WholeDocument/PageRegion (or no kind) match by
//   `affectedScopeIds` (empty list ⇒ matches anything, presence of `'document'`
//   ⇒ matches anything). Other scope kinds match when `scope.blockId === blockId`
//   or `affectedScopeIds` is empty / contains the block id.
// `markOverlayNonText(node)` — sets `aria-hidden` and `data-text-probe-ignore` on
//   a DOM node so accessibility tools + text-probe code skip it. No-op when the
//   node is null or lacks `setAttribute`.

import { asArray } from '../core/helpers.mjs';

export function domRectToRect(rect) {
    return {
        x: rect.x || rect.left || 0,
        y: rect.y || rect.top || 0,
        width: rect.width || 0,
        height: rect.height || 0,
    };
}

export function rectsOverlap(a, b) {
    return a.x < b.x + b.width
        && a.x + a.width > b.x
        && a.y < b.y + b.height
        && a.y + a.height > b.y;
}

export function rectsOverlapWithTolerance(a, b, tolerance) {
    const t = Number(tolerance || 0);
    return a.x < b.x + b.width - t
        && a.x + a.width > b.x + t
        && a.y < b.y + b.height - t
        && a.y + a.height > b.y + t;
}

export function hasRevisionRun(block) {
    return asArray(block && block.content && block.content.runs).some(function (run) {
        return !!run.revisionId;
    });
}

export function scopeIncludesBlock(scope, blockId, scopeKinds) {
    const kinds = scopeKinds || {};
    if (!scope
        || !scope.kind
        || scope.kind === kinds.WholeDocument
        || scope.kind === kinds.PageRegion) {
        const ids = asArray(scope && (scope.affectedScopeIds || scope.AffectedScopeIds));
        return ids.length === 0 || ids.indexOf(blockId) >= 0 || ids.indexOf('document') >= 0;
    }
    if (scope.blockId === blockId || scope.BlockId === blockId) return true;
    const affected = asArray(scope.affectedScopeIds || scope.AffectedScopeIds);
    return affected.length === 0 || affected.indexOf(blockId) >= 0;
}

export function markOverlayNonText(node) {
    if (!node || typeof node.setAttribute !== 'function') return node;
    node.setAttribute('aria-hidden', 'true');
    node.setAttribute('data-text-probe-ignore', 'true');
    return node;
}
