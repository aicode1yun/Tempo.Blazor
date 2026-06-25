// Phase R.5.18 / R.5.22 — core-engine/operations.mjs
// The operation algebra that underpins BOTH operation-log undo (R.5.18) and realtime
// collaboration (R.5.22). An "operation" is a small, position-relative description of a
// change that can be:
//   • inverted        — invert(op) undoes op (operation-log undo),
//   • transformed     — transform(op, against) rebases op past a concurrent change (OT),
//   • applied to text — applyToText(text, op) for the convergence algebra + tests.
//
// Operation shapes (all carry an optional `blockId` so cross-block edits are independent):
//   { type:'insert', blockId, offset, text }   — insert `text` at `offset`
//   { type:'delete', blockId, offset, text }   — delete `text` (kept, for invert) at `offset`
//
// The OT here is a clean-room implementation of the classic text transform satisfying the
// TP1 convergence property: applying two concurrent ops in either order, each transformed
// against the other, yields the same text. Marks / structural ops layer on top in the
// engine; the journal records them too but text ops are what need transforming for collab.

function str(v) { return v == null ? '' : String(v); }

// invert(op) → the operation that exactly undoes `op`.
export function invertOperation(op) {
    if (!op) return null;
    if (op.type === 'insert') return { type: 'delete', blockId: op.blockId, offset: op.offset, text: op.text };
    if (op.type === 'delete') return { type: 'insert', blockId: op.blockId, offset: op.offset, text: op.text };
    if (op.type === 'addMark') return { type: 'removeMark', blockId: op.blockId, start: op.start, end: op.end, markType: (op.mark && (op.mark.type || op.mark.Type)) || op.markType };
    if (op.type === 'removeMark') return { type: 'addMark', blockId: op.blockId, start: op.start, end: op.end, mark: op.mark || { type: op.markType } };
    if (op.type === 'split') return { type: 'merge', blockId: op.blockId, withBlockId: op.newBlockId, atOffset: op.offset };
    if (op.type === 'merge') return { type: 'split', blockId: op.blockId, offset: op.atOffset, newBlockId: op.withBlockId };
    return null;
}

// applyToText(text, op) → new text for ONE op. applyOps(text, ops) folds a list.
export function applyToText(text, op) {
    const s = str(text);
    if (!op) return s;
    const at = Math.max(0, Math.min(s.length, Number(op.offset) || 0));
    if (op.type === 'insert') return s.slice(0, at) + str(op.text) + s.slice(at);
    if (op.type === 'delete') {
        const len = str(op.text).length;
        return s.slice(0, at) + s.slice(at + len);
    }
    return s; // mark/structural ops don't change plain text
}

export function applyOps(text, ops) {
    return asList(ops).reduce(function (acc, op) { return applyToText(acc, op); }, str(text));
}

// transform(op, against, priority) → an ARRAY of ops equivalent to `op` but valid AFTER
// `against` has applied (OT). Returns 0..2 ops: an insert that lands inside a deleted span
// collapses; a delete that straddles a concurrent insert SPLITS into two deletes (this split
// is what makes the transform satisfy TP1 convergence). `priority` ('left' | 'right') breaks
// ties when both ops touch the same offset. Cross-block / non-text ops pass through unchanged.
export function transformOperation(op, against, priority) {
    if (!op || !against) return op ? [op] : [];
    if (op.type !== 'insert' && op.type !== 'delete') return [op];        // non-text → unchanged
    if (against.type !== 'insert' && against.type !== 'delete') return [op];
    if (op.blockId != null && against.blockId != null && op.blockId !== against.blockId) return [op];

    const aAt = Number(op.offset) || 0;
    const bAt = Number(against.offset) || 0;
    const bLen = str(against.text).length;
    const opText = str(op.text);
    const opLen = opText.length;
    const shift = function (o, by, extra) { return Object.assign({}, o, { offset: o.offset + by }, extra || {}); };

    if (against.type === 'insert') {
        if (op.type === 'insert') {
            const moveRight = (bAt < aAt) || (bAt === aAt && priority === 'right');
            return [Object.assign({}, op, { offset: moveRight ? aAt + bLen : aAt })];
        }
        // op is delete. The concurrent insert at bAt either sits before/at the start, after
        // the end, or strictly inside the deleted range (→ split so the inserted text lives).
        const opEnd = aAt + opLen;
        if (bAt <= aAt) return [Object.assign({}, op, { offset: aAt + bLen })];
        if (bAt >= opEnd) return [Object.assign({}, op, { offset: aAt })];
        // Split: delete the part before the insert, then the part after it. The two deletes
        // are applied sequentially, so the second is in the coordinate space left AFTER the
        // first removes (bAt-aAt) chars → its offset is aAt + bLen (not bAt + bLen).
        const before = { type: 'delete', blockId: op.blockId, offset: aAt, text: opText.slice(0, bAt - aAt) };
        const after = { type: 'delete', blockId: op.blockId, offset: aAt + bLen, text: opText.slice(bAt - aAt) };
        return [before, after].filter(function (o) { return o.text.length; });
    }

    // against is a delete of bLen chars at bAt.
    const agEnd = bAt + bLen;
    if (op.type === 'insert') {
        if (aAt <= bAt) return [Object.assign({}, op, { offset: aAt })];
        if (aAt >= agEnd) return [Object.assign({}, op, { offset: aAt - bLen })];
        return [Object.assign({}, op, { offset: bAt })]; // insert fell inside the removed span
    }

    // delete vs delete — keep only the chars `against` didn't already remove.
    const opEnd = aAt + opLen;
    if (opEnd <= bAt) return [Object.assign({}, op, { offset: aAt })];
    if (aAt >= agEnd) return [Object.assign({}, op, { offset: aAt - bLen })];
    const newStart = Math.min(aAt, bAt);
    const keepBefore = opText.slice(0, Math.max(0, bAt - aAt));
    const keepAfter = opText.slice(Math.max(0, agEnd - aAt));
    const survivor = keepBefore + keepAfter;
    if (!survivor.length) return [];
    return [{ type: 'delete', blockId: op.blockId, offset: newStart, text: survivor }];
}

// Rebase a remote op past a list of un-acked local ops (server-reconciliation order):
// returns { ops } = the remote op(s) to apply locally, and { locals } = the local ops
// rebased so they can be re-sent / re-applied on top of the now-applied remote op(s).
export function transformAgainstList(remoteOp, localOps, remotePriority) {
    let remote = [remoteOp];
    const rebasedLocals = [];
    asList(localOps).forEach(function (local) {
        const nextRemote = [];
        let curLocal = [local];
        remote.forEach(function (rOp) {
            // transform this remote fragment past the local op, and the local past it.
            transformOperation(rOp, local, remotePriority).forEach(function (x) { nextRemote.push(x); });
        });
        // local rebased past ALL remote fragments (sequentially).
        remote.forEach(function (rOp) {
            const next = [];
            curLocal.forEach(function (lOp) { transformOperation(lOp, rOp, remotePriority === 'left' ? 'right' : 'left').forEach(function (x) { next.push(x); }); });
            curLocal = next;
        });
        remote = nextRemote;
        curLocal.forEach(function (l) { rebasedLocals.push(l); });
    });
    return { ops: remote, locals: rebasedLocals };
}

function asList(v) { return Array.isArray(v) ? v : (v == null ? [] : [v]); }
