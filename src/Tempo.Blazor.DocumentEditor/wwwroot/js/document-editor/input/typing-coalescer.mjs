// Phase D — input/typing-coalescer.mjs
// `shouldCoalesceTyping(previous, next, now, timeoutMs)` decides whether two adjacent
// InsertText operations should merge into one undo step ("hello" + " world" → "hello
// world" in the history). `coalesceTypingOperation(previous, next)` produces the
// merged operation.
//
// Pure helpers — the typing change buffer (which uses these) is still in the legacy
// IIFE because it owns the operations array and selection state, but the merge
// decision logic itself is pure.

import { asText } from '../core/helpers.mjs';
import { normalizeTarget } from '../core/normalize-target.mjs';
import { OperationTypes } from '../history/operation-types.mjs';

const DEFAULT_COALESCE_WINDOW_MS = 1000;

// Decide whether `next` extends `previous` (same block, adjacent offset, both
// InsertText, neither contains a newline, not from paste, within `timeoutMs` of
// `previous.timestamp`).
export function shouldCoalesceTyping(previous, next, now, timeoutMs) {
    if (!previous || !next) return false;
    if (previous.type !== OperationTypes.InsertText
        || next.type !== OperationTypes.InsertText) return false;
    const previousTarget = normalizeTarget(previous.target || previous.Target);
    const nextTarget = normalizeTarget(next.target || next.Target);
    const previousText = asText(previous.text || previous.Text);
    if (previousTarget.blockId !== nextTarget.blockId) return false;
    if (previousTarget.offset + previousText.length !== nextTarget.offset) return false;
    if (/\n/.test(previousText) || /\n/.test(asText(next.text || next.Text))) return false;
    if (String(next.source || '').toLowerCase() === 'paste') return false;
    const age = Number(now || Date.now()) - Number(previous.timestamp || 0);
    return age <= Number(timeoutMs || DEFAULT_COALESCE_WINDOW_MS);
}

// Build the merged InsertText that replaces both `previous` and `next` in the buffer.
// `createOperation` is injected so callers (typing buffer, history stack) pass their
// own operation factory.
export function coalesceTypingOperation(createOperation, previous, next) {
    if (typeof createOperation !== 'function') {
        throw new TypeError('coalesceTypingOperation requires createOperation function');
    }
    return createOperation(OperationTypes.InsertText, Object.assign({}, previous, {
        text: asText(previous.text || previous.Text) + asText(next.text || next.Text),
        timestamp: next.timestamp || Date.now(),
    }), {
        source: previous.source || 'typing',
        batchId: previous.batchId || next.batchId,
    });
}

export const defaultCoalesceWindowMs = DEFAULT_COALESCE_WINDOW_MS;
