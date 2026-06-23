// Phase D — layout/caret-affinity.mjs
// `inferCaretIntervalAffinity(interval, blockers, fallback)` — when a caret lands
// in a line where inline blockers (drawings/images) push text aside, decides whether
// the caret should attach to the run BEFORE or AFTER the blocker.
//
// Pure of closure state — uses `hitRectFromAny` to coerce any rect shape.

import { asArray } from '../core/helpers.mjs';
import { hitRectFromAny } from './hit-rect.mjs';

export function inferCaretIntervalAffinity(interval, blockers, fallback) {
    const rect = hitRectFromAny(interval || {});
    const left = rect.x;
    const right = rect.x + rect.width;
    let beforeObject = false;
    let afterObject = false;
    asArray(blockers).forEach(function (blocker) {
        const blockerRect = blocker && (blocker.rect || blocker.Rect)
            ? hitRectFromAny(blocker.rect || blocker.Rect)
            : hitRectFromAny(blocker || {});
        const blockerLeft = blockerRect.x;
        const blockerRight = blockerRect.x + blockerRect.width;
        if (right <= blockerLeft + 0.5) beforeObject = true;
        if (left >= blockerRight - 0.5) afterObject = true;
    });
    if (beforeObject && !afterObject) return 'before';
    if (afterObject && !beforeObject) return 'after';
    return fallback === 'before' ? 'before' : 'after';
}
