// Phase D — input/typing-change-buffer.mjs
// `createTypingChangeBufferFactory({attachOperationMethods, shouldCoalesceTyping,
//   coalesceTypingOperation, clone, sortObject})` →
//   `createTypingChangeBuffer(options?)`
// Accumulates typing operations into a list that the history controller can
// flush as a single coalesced edit. While the user keeps typing within the
// `timeoutMs` window (default 1000 ms), `push(operation)` coalesces consecutive
// inserts into the last buffer entry. Selection changes or non-typing commands
// (Enter/Paste/Delete) reset the buffer so the next edit becomes a new history
// entry.
//
// Returned object:
//   push(operation) — append + coalesce
//   resetForSelectionChange(selection) — drop ops, remember selection
//   resetForCommand(name) — drop ops, return the name (sugar for callers)
//   resetForEnter() / resetForPaste() / resetForDelete() — shortcuts
//   snapshot() — `{operationCount, operations, lastSelection}` for inspection

export function createTypingChangeBufferFactory(deps) {
    const opts = deps || {};
    for (const key of ['attachOperationMethods', 'shouldCoalesceTyping',
        'coalesceTypingOperation', 'clone', 'sortObject']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createTypingChangeBufferFactory requires options.${key} (function)`);
        }
    }
    const {
        attachOperationMethods,
        shouldCoalesceTyping,
        coalesceTypingOperation,
        clone,
        sortObject,
    } = opts;

    return function createTypingChangeBuffer(options) {
        const bufferOpts = options || {};
        const timeoutMs = Number(bufferOpts.timeoutMs || bufferOpts.TimeoutMs || 1000) || 1000;
        let operations = [];
        let lastSelection = null;

        function push(operation) {
            const op = attachOperationMethods(operation);
            const previous = operations[operations.length - 1];
            if (previous && shouldCoalesceTyping(
                attachOperationMethods(previous), op, op.timestamp, timeoutMs)) {
                operations[operations.length - 1] =
                    coalesceTypingOperation(attachOperationMethods(previous), op).toJSON();
            } else {
                operations.push(op.toJSON ? op.toJSON() : clone(op));
            }
        }

        function resetForSelectionChange(selection) {
            operations = [];
            lastSelection = clone(selection || null);
        }

        function resetForCommand(commandName) {
            operations = [];
            return commandName;
        }

        function snapshot() {
            return sortObject({
                operationCount: operations.length,
                operations,
                lastSelection,
            });
        }

        return {
            push,
            resetForSelectionChange,
            resetForCommand,
            resetForEnter() { resetForCommand('enter'); },
            resetForPaste() { resetForCommand('paste'); },
            resetForDelete() { resetForCommand('delete'); },
            snapshot,
        };
    };
}
