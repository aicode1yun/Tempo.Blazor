export function createHistoryStore() {
    const undoStack = [];
    const redoStack = [];

    function push(transaction) {
        if (!transaction || typeof transaction !== 'object') {
            throw new Error('History transaction must be an object.');
        }

        undoStack.push(Object.freeze({ ...transaction }));
        redoStack.length = 0;
        return snapshot();
    }

    function undo() {
        if (!undoStack.length) {
            return null;
        }

        const transaction = undoStack.pop();
        redoStack.push(transaction);
        return transaction;
    }

    function redo() {
        if (!redoStack.length) {
            return null;
        }

        const transaction = redoStack.pop();
        undoStack.push(transaction);
        return transaction;
    }

    function snapshot() {
        return {
            canUndo: undoStack.length > 0,
            canRedo: redoStack.length > 0,
            undoDepth: undoStack.length,
            redoDepth: redoStack.length,
        };
    }

    return {
        push,
        undo,
        redo,
        snapshot,
    };
}
