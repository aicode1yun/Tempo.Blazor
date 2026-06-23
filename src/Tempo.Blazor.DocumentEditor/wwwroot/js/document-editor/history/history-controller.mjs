// Phase D — history/history-controller.mjs
// `createHistoryControllerFactory(deps)` → `createHistoryController(model, options)` →
//   the undo/redo + commit orchestration hub. Builds transactions, maintains the
//   undo/redo stacks (with typing coalescing), runs layout + render after each commit,
//   and canonicalises the selection through the post-fixer.
//
// The layout engine, atomic renderer, transaction factory, operation helpers,
// apply-operation and selection-token are injected (they come from the engine's other
// factories — including the still-monolithic paragraph engine / renderer, hence the
// `createParagraphLayoutEngine` / `createAtomicRenderer` deps). Pure helpers are
// imported directly.
//
// `createHistoryRestoreOperation` / `createHistoryEntryFromTransaction` /
// `canCoalesceHistoryTyping` / `coalesceHistoryEntry` are the history-entry helpers,
// closed over the injected operation helpers.

import { asArray, asText, clone, sortObject, unique } from '../core/helpers.mjs';
import { createSelectionSnapshot } from '../core/selection-snapshot.mjs';
import { firstModelSelection } from '../core/first-block.mjs';
import { createDefaultSchemaRegistry } from '../core/schema.mjs';
import { createRenderSnapshot } from '../render/render-snapshot.mjs';
import { shouldCoalesceTyping, coalesceTypingOperation } from '../input/typing-coalescer.mjs';
import { supportsOperationHistory, transactionAffectsDocument } from './operations.mjs';
import { OperationTypes, TransactionTypes } from './operation-types.mjs';

const REQUIRED_DEPS = [
    'createTransaction', 'applyOperation', 'createOperation', 'attachOperationMethods',
    'createUndoHistoryOperations', 'createRedoHistoryOperations', 'withStableSelectionToken',
    'createParagraphLayoutEngine', 'createAtomicRenderer', 'createSelectionPostFixer',
];

export function createHistoryControllerFactory(options) {
    const opts = options || {};
    for (const key of REQUIRED_DEPS) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createHistoryControllerFactory requires options.${key} (function)`);
        }
    }
    const {
        createTransaction, applyOperation, createOperation, attachOperationMethods,
        createUndoHistoryOperations, createRedoHistoryOperations, withStableSelectionToken,
        createParagraphLayoutEngine, createAtomicRenderer, createSelectionPostFixer,
    } = opts;

    function createHistoryRestoreOperation(snapshot, selection, source, affectedScopeIds, previousSnapshot, previousSelection) {
        return createOperation(OperationTypes.RestoreSnapshot, {
            snapshot: clone(snapshot || null),
            previousSnapshot: clone(previousSnapshot || null),
            selection: clone(selection || null),
            previousSelection: clone(previousSelection || null),
            affectedScopeIds: asArray(affectedScopeIds).length ? asArray(affectedScopeIds) : ['document'],
        }, { source: source || 'history' });
    }

    function createHistoryEntryFromTransaction(transaction) {
        const instanceId = transaction.instanceId || transaction.InstanceId || '';
        const operations = transaction.operations.map(function (operation) { return attachOperationMethods(operation).toJSON(); });
        const useOperationHistory = operations.length > 0 && operations.every(supportsOperationHistory);
        let beforeSnapshot = clone(transaction.beforeModelSnapshot || null);
        let afterSnapshot = clone(transaction.afterModelSnapshot || null);
        if (useOperationHistory && transaction.lightweightSnapshots === true) {
            beforeSnapshot = null;
            afterSnapshot = null;
        }
        const beforeSelection = transaction.beforeSelection
            ? (useOperationHistory && !beforeSnapshot ? createSelectionSnapshot(transaction.beforeSelection) : withStableSelectionToken(instanceId, transaction.beforeSelection, beforeSnapshot || null))
            : createSelectionSnapshot(null);
        const afterSelection = transaction.afterSelection
            ? (useOperationHistory && !afterSnapshot && !beforeSnapshot ? createSelectionSnapshot(transaction.afterSelection) : withStableSelectionToken(instanceId, transaction.afterSelection, afterSnapshot || beforeSnapshot || null))
            : createSelectionSnapshot(beforeSelection);
        const scopes = asArray(transaction.invalidatedScopes).length ? asArray(transaction.invalidatedScopes) : ['document'];
        return {
            id: transaction.id,
            transaction: transaction.toJSON(),
            operations,
            inverseOperations: useOperationHistory
                ? createUndoHistoryOperations(operations)
                : [createHistoryRestoreOperation(beforeSnapshot, beforeSelection, 'undo', scopes, afterSnapshot, afterSelection).toJSON()],
            redoOperations: useOperationHistory
                ? createRedoHistoryOperations(operations)
                : [createHistoryRestoreOperation(afterSnapshot, afterSelection, 'redo', scopes, beforeSnapshot, beforeSelection).toJSON()],
            beforeModelSnapshot: beforeSnapshot,
            afterModelSnapshot: afterSnapshot,
            beforeSelection,
            afterSelection,
            invalidatedScopes: scopes,
            createdAt: Date.now(),
        };
    }

    function canCoalesceHistoryTyping(previousEntry, transaction, timeoutMs) {
        if (!previousEntry || !transaction || transaction.type !== TransactionTypes.Typing) return false;
        if (!previousEntry.transaction || previousEntry.transaction.type !== TransactionTypes.Typing) return false;
        if (asArray(previousEntry.operations).length !== 1 || asArray(transaction.operations).length !== 1) return false;
        return shouldCoalesceTyping(
            attachOperationMethods(previousEntry.operations[0]),
            attachOperationMethods(transaction.operations[0]),
            transaction.operations[0].timestamp,
            timeoutMs || 1000);
    }

    function coalesceHistoryEntry(previousEntry, transaction) {
        const mergedOperation = coalesceTypingOperation(attachOperationMethods(previousEntry.operations[0]), attachOperationMethods(transaction.operations[0]));
        previousEntry.operations = [mergedOperation.toJSON()];
        previousEntry.afterModelSnapshot = clone(transaction.afterModelSnapshot || null);
        previousEntry.afterSelection = createSelectionSnapshot(transaction.afterSelection || previousEntry.afterSelection);
        previousEntry.transaction.afterSelection = previousEntry.afterSelection;
        previousEntry.transaction.invalidatedScopes = unique(asArray(previousEntry.transaction.invalidatedScopes).concat(asArray(transaction.invalidatedScopes)));
        previousEntry.transaction.operationCount = 1;
        previousEntry.transaction.coalesced = true;
        if (supportsOperationHistory(mergedOperation)) {
            previousEntry.redoOperations = createRedoHistoryOperations(previousEntry.operations);
            previousEntry.inverseOperations = createUndoHistoryOperations(previousEntry.operations);
        } else {
            previousEntry.redoOperations = [createHistoryRestoreOperation(
                previousEntry.afterModelSnapshot, previousEntry.afterSelection, 'redo',
                previousEntry.transaction.invalidatedScopes,
                previousEntry.beforeModelSnapshot, previousEntry.beforeSelection).toJSON()];
            previousEntry.inverseOperations = [createHistoryRestoreOperation(
                previousEntry.beforeModelSnapshot, previousEntry.beforeSelection, 'undo',
                previousEntry.transaction.invalidatedScopes,
                previousEntry.afterModelSnapshot, previousEntry.afterSelection).toJSON()];
        }
        return previousEntry;
    }

    return function createHistoryController(model, controllerOptions) {
        const copts = controllerOptions || {};
        const schema = copts.schema || copts.Schema || createDefaultSchemaRegistry();
        const paragraphEngine = copts.paragraphLayoutEngine || copts.ParagraphLayoutEngine || createParagraphLayoutEngine(null, copts.layoutOptions || copts.LayoutOptions || {});
        const renderer = copts.renderer || copts.Renderer || createAtomicRenderer();
        const root = copts.root || copts.Root || null;
        let selection = createSelectionSnapshot(copts.selection || copts.Selection || firstModelSelection(model));
        let undoStack = [];
        let redoStack = [];
        const transactions = [];
        let layout = paragraphEngine.layoutDocument(model, { selection });
        let renderVersion = 0;
        let epoch = 0;
        let lastDiffer = null;
        let lastTransaction = null;

        let pendingRenderArgs = null;
        let pendingRenderHandle = null;
        let pendingRenderFlushFn = null;
        const rafBatchEnabled = (copts.renderBatching || copts.RenderBatching) === 'raf';
        function scheduleRenderFlush(reason, affectedScopes) {
            const scopes = pendingRenderArgs ? pendingRenderArgs.scopes : new Set();
            asArray(affectedScopes).forEach(function (s) { if (s) scopes.add(s); });
            if (scopes.size === 0) scopes.add('document');
            pendingRenderArgs = { reason: reason || 'history', scopes };
            if (pendingRenderHandle !== null) return;
            if (typeof requestAnimationFrame === 'function') {
                pendingRenderHandle = requestAnimationFrame(pendingRenderFlushFn);
            } else {
                pendingRenderHandle = setTimeout(pendingRenderFlushFn, 0);
            }
        }
        pendingRenderFlushFn = function () {
            pendingRenderHandle = null;
            if (!pendingRenderArgs || !root) { pendingRenderArgs = null; return; }
            const args = pendingRenderArgs;
            pendingRenderArgs = null;
            renderer.render(root, createRenderSnapshot(model, layout, selection, { affectedScopes: Array.from(args.scopes) }), { reason: args.reason });
            renderVersion++;
        };
        function flushPendingRender() {
            if (pendingRenderHandle !== null) {
                if (typeof cancelAnimationFrame === 'function' && typeof pendingRenderHandle === 'number') {
                    try { cancelAnimationFrame(pendingRenderHandle); } catch { /* ignore */ }
                } else {
                    try { clearTimeout(pendingRenderHandle); } catch { /* ignore */ }
                }
                pendingRenderHandle = null;
            }
            pendingRenderFlushFn();
        }
        function hasPendingRender() { return pendingRenderArgs !== null; }

        function renderAtomic(reason, affectedScopes) {
            layout = paragraphEngine.layoutDocument(model, { selection, affectedScopes: affectedScopes || ['document'] });
            if (root) {
                if (rafBatchEnabled) {
                    scheduleRenderFlush(reason, affectedScopes);
                } else {
                    renderer.render(root, createRenderSnapshot(model, layout, selection, { affectedScopes: affectedScopes || ['document'] }), { reason: reason || 'history' });
                    renderVersion++;
                }
            } else {
                renderVersion++;
            }
            return layout;
        }

        function pushHistory(transaction) {
            const entry = createHistoryEntryFromTransaction(transaction);
            const previous = undoStack[undoStack.length - 1] || null;
            if (canCoalesceHistoryTyping(previous, transaction, copts.typingCoalescingMs || copts.TypingCoalescingMs || 1000)) {
                return coalesceHistoryEntry(previous, transaction);
            }
            undoStack.push(entry);
            return entry;
        }

        function commitOperations(operations, meta) {
            const body = meta || {};
            const list = asArray(operations).map(function (operation) { return attachOperationMethods(operation); });
            const operationSelection = list.length === 1 ? (list[0].selection || list[0].Selection || null) : null;
            const transaction = createTransaction(model, {
                type: body.transactionType || body.TransactionType || (list.length === 1 && list[0].type === OperationTypes.InsertText ? TransactionTypes.Typing : TransactionTypes.Default),
                label: body.label || body.Label || 'Document change',
                beforeSelection: body.beforeSelection || body.BeforeSelection || operationSelection || selection,
            });
            for (let i = 0; i < list.length; i++) {
                const result = transaction.apply(list[i]);
                if (!result.ok) {
                    return Object.assign({ ok: false, transaction: transaction.toJSON(), operationIndex: i }, result);
                }
            }
            const committed = transaction.commit();
            selection = createSelectionPostFixer(schema).fix(model, transaction.afterSelection || selection);
            transaction.afterSelection = clone(selection);
            transaction.afterModelSnapshot = clone(model);
            lastDiffer = committed.differ;
            lastTransaction = transaction.toJSON();
            transactions.push(transaction.toJSON());
            let entry = null;
            if (transactionAffectsDocument(transaction)) {
                entry = pushHistory(transaction);
                redoStack = [];
                epoch++;
            }
            renderAtomic(transaction.type, transaction.invalidatedScopes);
            return sortObject({
                ok: true,
                transaction: transaction.toJSON(),
                historyEntry: entry,
                selection,
                layout,
                differ: committed.differ,
                undoDepth: undoStack.length,
                redoDepth: redoStack.length,
                renderVersion,
            });
        }

        function applyHistory(undo) {
            const sourceStack = undo ? undoStack : redoStack;
            const targetStack = undo ? redoStack : undoStack;
            const entry = sourceStack.pop();
            if (!entry) return sortObject({ ok: false, empty: true, transactionType: undo ? TransactionTypes.Undo : TransactionTypes.Redo });
            const historySource = undo ? TransactionTypes.Undo : TransactionTypes.Redo;
            const operations = (undo ? entry.inverseOperations : entry.redoOperations).map(function (operation) {
                const nextOperation = attachOperationMethods(clone(operation));
                nextOperation.source = historySource;
                return nextOperation;
            });
            const historyTransaction = createTransaction(model, {
                type: undo ? TransactionTypes.Undo : TransactionTypes.Redo,
                label: undo ? 'Undo' : 'Redo',
                beforeSelection: selection,
            });
            for (let i = operations.length - 1; i >= 0; i--) {
                const result = historyTransaction.apply(operations[i]);
                if (!result.ok) {
                    return Object.assign({ ok: false, transaction: historyTransaction.toJSON(), operationIndex: i }, result);
                }
            }
            const committed = historyTransaction.commit();
            selection = createSelectionPostFixer(schema).fix(model, undo ? entry.beforeSelection : entry.afterSelection);
            historyTransaction.afterSelection = clone(selection);
            historyTransaction.afterModelSnapshot = clone(model);
            targetStack.push(entry);
            transactions.push(historyTransaction.toJSON());
            lastDiffer = committed.differ;
            lastTransaction = historyTransaction.toJSON();
            epoch++;
            renderAtomic(historyTransaction.type, entry.invalidatedScopes);
            return sortObject({
                ok: true,
                transaction: historyTransaction.toJSON(),
                historyEntry: entry,
                appliedOperations: operations.map(function (operation) { return operation.toJSON ? operation.toJSON() : clone(operation); }),
                selection,
                layout,
                differ: committed.differ,
                undoDepth: undoStack.length,
                redoDepth: redoStack.length,
                renderVersion,
            });
        }

        function debug() {
            return sortObject({
                epoch,
                transactionCount: transactions.length,
                undoDepth: undoStack.length,
                redoDepth: redoStack.length,
                selection,
                layoutInvalidatedScopes: (layout && layout.invalidatedScopeIds) || [],
                renderVersion,
                lastDiffer,
                lastTransaction,
                nextUndo: undoStack.length ? undoStack[undoStack.length - 1].transaction : null,
                nextRedo: redoStack.length ? redoStack[redoStack.length - 1].transaction : null,
            });
        }

        renderAtomic('initial', ['document']);

        return {
            commitOperations,
            commitOperation: function (operation, meta) { return commitOperations([operation], meta || {}); },
            undo: function () { return applyHistory(true); },
            redo: function () { return applyHistory(false); },
            clearRedo: function () { redoStack = []; return debug(); },
            getSelection: function () { return createSelectionSnapshot(selection); },
            setSelection: function (nextSelection) { selection = createSelectionSnapshot(nextSelection || selection); return selection; },
            getLayout: function () { return clone(layout); },
            getUndoStack: function () { return clone(undoStack); },
            getRedoStack: function () { return clone(redoStack); },
            getTransactions: function () { return clone(transactions); },
            flushPendingRender,
            hasPendingRender,
            isRafBatchingEnabled: function () { return rafBatchEnabled; },
            debug,
        };
    };
}
