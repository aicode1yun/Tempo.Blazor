import { createTypingCoalescer } from './coalescing.mjs';

export function createCanvasHistoryController(options = {}) {
    const undoStack = [];
    const redoStack = [];
    const typingCoalescer = options.typingCoalescer || createTypingCoalescer(options);
    let revision = 0;
    let lastTransaction = null;

    function push(transaction) {
        const normalized = normalizeTransaction(transaction);
        if (!normalized) {
            return snapshot();
        }

        undoStack.push(Object.freeze(normalized));
        redoStack.length = 0;
        revision += 1;
        lastTransaction = describeTransaction(normalized);
        return snapshot();
    }

    function recordTextInput(change = {}) {
        if (change.input?.compositionPreview === true || change.result?.changed === false) {
            return snapshot();
        }

        const before = change.before || null;
        const after = change.after || {
            model: change.model,
            selection: change.selection,
            formatState: change.formatState || null,
            paragraphState: change.paragraphState || null,
        };
        if (!before?.model || !after?.model) {
            return snapshot();
        }

        const nextTransaction = normalizeTransaction({
            id: `canvas-text-${(change.input?.revision || 0)}-${Date.now()}`,
            kind: 'text-input',
            commandId: change.result?.operation || change.edit?.type || 'textInput',
            before,
            after,
            dirtyBlockIds: change.result?.dirtyBlockIds || change.input?.dirtyBlockIds || [],
            typing: typingCoalescer.buildMetadata({
                ...change,
                before,
                after,
            }),
        });

        const previous = undoStack.at(-1);
        if (typingCoalescer.canCoalesce(previous, nextTransaction)) {
            const merged = Object.freeze(normalizeTransaction(typingCoalescer.merge(previous, nextTransaction)));
            undoStack[undoStack.length - 1] = merged;
            redoStack.length = 0;
            revision += 1;
            lastTransaction = describeTransaction(merged, true);
            return snapshot();
        }

        return push(nextTransaction);
    }

    function undo() {
        if (!undoStack.length) {
            return null;
        }

        const transaction = undoStack.pop();
        redoStack.push(transaction);
        revision += 1;
        lastTransaction = describeTransaction(transaction, false, 'undo');
        return transaction;
    }

    function redo() {
        if (!redoStack.length) {
            return null;
        }

        const transaction = redoStack.pop();
        undoStack.push(transaction);
        revision += 1;
        lastTransaction = describeTransaction(transaction, false, 'redo');
        return transaction;
    }

    function clear() {
        undoStack.length = 0;
        redoStack.length = 0;
        revision += 1;
        lastTransaction = null;
        return snapshot();
    }

    function snapshot() {
        return {
            canUndo: undoStack.length > 0,
            canRedo: redoStack.length > 0,
            undoDepth: undoStack.length,
            redoDepth: redoStack.length,
            revision,
            lastTransaction,
        };
    }

    return {
        push,
        recordTextInput,
        undo,
        redo,
        clear,
        snapshot,
    };
}

function normalizeTransaction(transaction) {
    if (!transaction || typeof transaction !== 'object') {
        throw new Error('History transaction must be an object.');
    }

    if (!transaction.before?.model || !transaction.after?.model) {
        return null;
    }

    return {
        id: String(transaction.id || `canvas-transaction-${Date.now()}`),
        kind: String(transaction.kind || 'command'),
        commandId: transaction.commandId == null ? null : String(transaction.commandId),
        before: clone(transaction.before),
        after: clone(transaction.after),
        dirtyBlockIds: unique(transaction.dirtyBlockIds || []),
        typing: transaction.typing ? { ...transaction.typing } : null,
    };
}

function describeTransaction(transaction, coalesced = false, action = 'push') {
    return {
        id: transaction.id,
        kind: transaction.kind,
        commandId: transaction.commandId,
        coalesced,
        action,
    };
}

function unique(values) {
    return [...new Set(values.map(value => String(value || '')).filter(Boolean))];
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
