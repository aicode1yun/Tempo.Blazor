// Phase R.4.6i / R.5.18 — core-engine/undo-stack.mjs
// Undo/redo for the model-owned surface. TWO entry kinds coexist:
//
//   • 'snapshot' — a full pre-edit clone of { model, caret, anchor, selection,
//     selectedObjectId }. Correct for EVERY edit type (marks, paragraph props, images,
//     tables, structural merges/splits) regardless of complexity. Default for anything that
//     isn't a plain text run.
//
//   • 'ops' (R.5.18 operation-log undo) — for high-frequency PLAIN TEXT edits (typing,
//     backspace, delete) it stores the operation + its inverse instead of cloning the whole
//     document. A run coalesces into ONE entry: its `redo` accumulates forward ops, its `undo`
//     accumulates inverses in reverse order. Undoing a "Hello" typing run replays five small
//     delete ops rather than restoring a full-model snapshot — O(edit) memory, not O(document).
//
// Coalescing only merges within the SAME kind + key; switching kind/key (or a caret move →
// breakCoalescing) starts a fresh undo step. Any record invalidates the redo branch.
//
//   createUndoStack({ clone, limit? }) → {
//     record(state, key?)            // push a snapshot entry (call BEFORE mutating)
//     recordOps(entry, key?)         // push/merge an ops entry: { undo:[op…], redo:[op…], before, after }
//     undo(currentState) → entry|null   // entry.kind drives how the caller applies it
//     redo(currentState) → entry|null
//     breakCoalescing(), canUndo(), canRedo(), clear(), depth()
//   }

function shallow(value) { return value ? Object.assign({}, value) : null; }

export function createUndoStack(options) {
    const opts = options || {};
    const limit = Number(opts.limit) > 0 ? Number(opts.limit) : 200;
    const cloneModel = typeof opts.clone === 'function'
        ? opts.clone
        : function (v) { return JSON.parse(JSON.stringify(v)); };

    let undoStack = [];
    let redoStack = [];
    let lastKey = null;

    function snapshot(state) {
        const s = state || {};
        return {
            model: s.model ? cloneModel(s.model) : null,
            caret: shallow(s.caret),
            anchor: shallow(s.anchor),
            selection: shallow(s.selection),
            selectedObjectId: s.selectedObjectId || null,
        };
    }
    // Snapshot entries spread their fields (+ a `kind` tag) so callers can read entry.model
    // directly (backward compatible) while still discriminating ops vs snapshot by `kind`.
    function snapEntry(state) { return Object.assign({ kind: 'snapshot' }, snapshot(state)); }
    function top(stack) { return stack.length ? stack[stack.length - 1] : null; }
    function trim() { while (undoStack.length > limit) undoStack.shift(); }

    return {
        // Push the PRE-edit snapshot. Coalesces (skips) only when the previous entry is also a
        // snapshot with the same key — a run keeps just its starting state.
        record(state, coalesceKey) {
            const key = coalesceKey || null;
            const t = top(undoStack);
            if (key && key === lastKey && t && t.kind === 'snapshot') {
                redoStack = [];
                return false;
            }
            undoStack.push(snapEntry(state));
            trim();
            redoStack = [];
            lastKey = key;
            return true;
        },

        // R.5.18 — push (or merge) an operation-log entry. `entry` = { undo:[…], redo:[…],
        // before, after }. Merges into the previous ops entry when the key matches (a typing
        // run): redo grows at the end, undo grows at the FRONT (inverses replay in reverse).
        recordOps(entry, coalesceKey) {
            const key = coalesceKey || null;
            const t = top(undoStack);
            if (key && key === lastKey && t && t.kind === 'ops') {
                t.redo = t.redo.concat(entry.redo || []);
                t.undo = (entry.undo || []).concat(t.undo);
                t.after = entry.after || t.after;
                redoStack = [];
                return false;
            }
            undoStack.push({
                kind: 'ops',
                undo: (entry.undo || []).slice(),
                redo: (entry.redo || []).slice(),
                before: shallow(entry.before),
                after: shallow(entry.after),
            });
            trim();
            redoStack = [];
            lastKey = key;
            return true;
        },

        undo(currentState) {
            if (!undoStack.length) return null;
            const entry = undoStack.pop();
            lastKey = null;
            if (entry.kind === 'ops') {
                redoStack.push(entry); // the entry knows both directions
                return entry;          // caller applies entry.undo + restores entry.before
            }
            redoStack.push(snapEntry(currentState));
            return entry;              // caller restores entry.snapshot
        },
        redo(currentState) {
            if (!redoStack.length) return null;
            const entry = redoStack.pop();
            lastKey = null;
            if (entry.kind === 'ops') {
                undoStack.push(entry);
                return entry;          // caller applies entry.redo + restores entry.after
            }
            undoStack.push(snapEntry(currentState));
            return entry;
        },
        breakCoalescing() { lastKey = null; },
        canUndo() { return undoStack.length > 0; },
        canRedo() { return redoStack.length > 0; },
        clear() { undoStack = []; redoStack = []; lastKey = null; },
        depth() { return { undo: undoStack.length, redo: redoStack.length }; },
    };
}
