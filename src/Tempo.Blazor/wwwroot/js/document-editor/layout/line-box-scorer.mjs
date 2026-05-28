// Phase D — layout/line-box-scorer.mjs
// `scoreNearestTextPositionLineBox(line, x, y, pointPageIndex)` — heuristic that
// ranks candidate line boxes during nearest-text-position lookup.
//
// Scoring rules (lower wins):
//   - Inside the line rect: bonus -10000.
//   - On wrong page (pointPageIndex specified and differs): penalty +100000.
//   - Otherwise: Euclidean distance to nearest edge.

const INSIDE_BONUS = -10000;
const WRONG_PAGE_PENALTY = 100000;

export function scoreNearestTextPositionLineBox(line, x, y, pointPageIndex) {
    const rect = line && line.rect || {};
    const left = Number(rect.x || 0);
    const top = Number(rect.y || 0);
    const right = left + Number(rect.width || 0);
    const bottom = top + Number(rect.height || 0);
    const insideX = x >= left && x <= right;
    const insideY = y >= top && y <= bottom;
    const dx = insideX ? 0 : Math.min(Math.abs(x - left), Math.abs(x - right));
    const dy = insideY ? 0 : Math.min(Math.abs(y - top), Math.abs(y - bottom));
    const pagePenalty = pointPageIndex === null || pointPageIndex === undefined
        || Number(line.pageIndex || 0) === Number(pointPageIndex)
        ? 0
        : WRONG_PAGE_PENALTY;
    return pagePenalty
        + (insideX && insideY ? INSIDE_BONUS : 0)
        + Math.sqrt(dx * dx + dy * dy);
}
