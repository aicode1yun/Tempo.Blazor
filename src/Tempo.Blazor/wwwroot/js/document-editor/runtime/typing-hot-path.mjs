// Phase D — runtime/typing-hot-path.mjs
// "Typing hot path" classifier — true when the editor is currently inside a typing
// burst, which gates several optimisation paths (e.g. defer non-critical layout,
// suppress collapsed-selection-change notifications, batch boundary patches).
//
// The classifier is intentionally permissive: the window expands during composition
// (`TypingBatchMs` option, default 500ms) plus a 32ms grace.

import { asArray } from '../core/helpers.mjs';
import { strictPerformanceNow } from './strict-performance-helpers.mjs';

export function typingHotPathWindowMs(inst) {
    const options = inst && inst.options || {};
    const raw = Number(options.TypingBatchMs || options.typingBatchMs || 500) || 500;
    return Math.max(100, raw);
}

export function isTypingHotPath(inst, now) {
    if (!inst) return false;
    const current = Number(now || strictPerformanceNow()) || strictPerformanceNow();
    const windowMs = typingHotPathWindowMs(inst);
    const lastApply = Number(inst.lastInputDomApplyAt || 0);
    return asArray(inst.pendingTypingBoundaryPatches).length > 0
        || Number(inst.suppressCollapsedSelectionChangeUntil || 0) >= current
        || (lastApply > 0 && current - lastApply <= windowMs + 32);
}
