// Phase D — render/find-nearest-body-paragraph.mjs
// `findNearestBodyParagraphBlockIdFromPoint(root, x, y)` — picks the closest
// editable body paragraph block under a screen point. Skips object-anchor blocks
// and zero-height blocks. Scoring: inside-the-rect blocks beat outside-rect ones
// by 100000, then Euclidean distance to the nearest edge tie-breaks. Returns the
// chosen block's `data-block-id`, or `''` when the root has no candidates.

export function findNearestBodyParagraphBlockIdFromPoint(root, x, y) {
    if (!root || !root.querySelectorAll) return '';
    const blocks = Array.from(root.querySelectorAll(
        '.tm-wysiwyg-page__layer--body-text .tm-wysiwyg-block[data-block-id]'))
        .filter(function (block) {
            return !block.classList.contains('tm-wysiwyg-object-anchor-block')
                && !block.hasAttribute('data-object-anchor-id')
                && block.getBoundingClientRect().height > 0;
        });
    let best = null;
    blocks.forEach(function (block) {
        const rect = block.getBoundingClientRect();
        const insideX = x >= rect.left && x <= rect.right;
        const insideY = y >= rect.top && y <= rect.bottom;
        const dx = insideX ? 0 : Math.min(Math.abs(x - rect.left), Math.abs(x - rect.right));
        const dy = insideY ? 0 : Math.min(Math.abs(y - rect.top), Math.abs(y - rect.bottom));
        const score = (insideX && insideY ? -100000 : 0) + Math.sqrt(dx * dx + dy * dy);
        if (!best || score < best.score) {
            best = { blockId: block.getAttribute('data-block-id') || '', score };
        }
    });
    return (best && best.blockId) || '';
}
