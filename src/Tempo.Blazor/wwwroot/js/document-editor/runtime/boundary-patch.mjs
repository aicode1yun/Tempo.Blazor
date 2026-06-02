// Phase D — runtime/boundary-patch.mjs
// `createBoundaryPatchModule({ attachOperationMethods, exportToCSharpJson,
//   invokeBoundaryMethod, recordTimeline, ensurePerformanceStats,
//   flushRuntimeRevisionsChanged })` → boundary-patch engine.
//
// The boundary patch is the serialised "diff + document snapshot" that the JS engine
// sends to the C# Blazor host after each model-mutating operation. Key concerns:
//
//  - Typing / Undo / Redo / pure-formatting operations are *deferred*: patches are
//    batched and merged in a 500 ms typing-flush or 16 ms deferred-flush timer.
//  - Non-typing operations flush any pending batch and dispatch synchronously.
//  - `hydrateBoundaryPatchSnapshot` attaches the full C# document JSON to a patch
//    that was created lightweight (deferSnapshot = true).
//
// Pure imports: createInitialDirtyState, getOperationId, isFormattingVisualOperation,
// isTypingLikeTransactionType, TransactionTypes, transactionAffectedBlockIds,
// createSelectionSnapshot, firstModelSelection, sortObject, asArray, clone.
// Injected (instance-state): invokeBoundaryMethod, recordTimeline,
// ensurePerformanceStats, flushRuntimeRevisionsChanged, attachOperationMethods,
// exportToCSharpJson.

import { asArray, clone, sortObject } from '../core/helpers.mjs';
import { createSelectionSnapshot } from '../core/selection-snapshot.mjs';
import { firstModelSelection } from '../core/first-block.mjs';
import { createInitialDirtyState, getOperationId } from '../history/dirty-state.mjs';
import { transactionAffectedBlockIds } from '../history/operation-affected.mjs';
import { isFormattingVisualOperation } from '../history/operation-classifiers.mjs';
import { isTypingLikeTransactionType, TransactionTypes } from '../history/operation-types.mjs';

const REQUIRED = [
    'attachOperationMethods', 'exportToCSharpJson',
    'invokeBoundaryMethod', 'recordTimeline',
    'ensurePerformanceStats', 'flushRuntimeRevisionsChanged',
];

export function createBoundaryPatchModule(options) {
    const opts = options || {};
    for (const key of REQUIRED) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createBoundaryPatchModule requires options.${key} (function)`);
        }
    }
    const {
        attachOperationMethods, exportToCSharpJson,
        invokeBoundaryMethod, recordTimeline,
        ensurePerformanceStats, flushRuntimeRevisionsChanged,
    } = opts;

    function hydrateBoundaryPatchSnapshot(inst, patch) {
        if (!inst || !patch || patch.csharpDocument) return patch;
        const stats = ensurePerformanceStats(inst);
        stats.boundarySnapshotExportCount = Number(stats.boundarySnapshotExportCount || 0) + 1;
        patch.snapshot = clone(inst.model);
        patch.csharpDocument = exportToCSharpJson(inst.model);
        patch.lightweight = false;
        patch.deferredSnapshot = false;
        patch.snapshotHydratedAt = Date.now();
        return patch;
    }

    function createBoundaryPatch(inst, transaction, operations, committed, source, createOptions) {
        const copts = createOptions || {};
        const operationList = asArray(operations).map(function (operation) {
            const op = attachOperationMethods(operation);
            return op.toJSON ? op.toJSON() : clone(operation);
        });
        const transactionJson = transaction && transaction.toJSON ? transaction.toJSON() : clone(transaction || {});
        const affectedBlockIds = transactionAffectedBlockIds(transaction, operationList);
        const patch = {
            instanceId: inst.id,
            transactionId: transactionJson.id || '',
            transactionType: transactionJson.type || source || 'default',
            operationIds: operationList.map(getOperationId).filter(Boolean),
            operations: operationList,
            affectedBlockIds,
            selection: createSelectionSnapshot(inst.selection || transactionJson.afterSelection || firstModelSelection(inst.model)),
            modelDelta: {
                kind: operationList.length > 0 ? 'operations' : 'snapshot',
                operations: operationList,
            },
            dirtyState: clone(inst.dirtyState || createInitialDirtyState()),
            differ: (committed && committed.differ) || inst.lastDiffer || null,
            lightweight: copts.deferSnapshot === true,
            deferredSnapshot: copts.deferSnapshot === true,
            createdAt: Date.now(),
        };
        if (copts.deferSnapshot === true) {
            const stats = ensurePerformanceStats(inst);
            stats.lightweightBoundaryPatchCount = Number(stats.lightweightBoundaryPatchCount || 0) + 1;
            return sortObject(patch);
        }
        hydrateBoundaryPatchSnapshot(inst, patch);
        return sortObject(patch);
    }

    function updateDirtyState(inst, patch, source) {
        inst.modelEpoch = Number(inst.modelEpoch || 0) + 1;
        inst.dirtyState = sortObject(Object.assign({}, inst.dirtyState || createInitialDirtyState(), {
            isDirty: true,
            epoch: inst.modelEpoch,
            pendingPatchCount: asArray(inst.boundaryPatches).length + (patch ? 1 : 0),
            lastFailure: null,
            source: source || (patch && patch.transactionType) || 'local',
        }));
        return inst.dirtyState;
    }

    function shouldDeferBoundarySnapshot(transaction, operations, source) {
        const transactionType = (transaction && (transaction.type || transaction.Type)) || source || '';
        if (isTypingLikeTransactionType(transactionType) || isTypingLikeTransactionType(source)) return true;
        const normalizedSource = String(source || transactionType || '').toLowerCase();
        if (normalizedSource === TransactionTypes.Undo || normalizedSource === TransactionTypes.Redo
            || normalizedSource === 'undo' || normalizedSource === 'redo') return true;
        const operationList = asArray(operations);
        return operationList.length > 0 && operationList.every(isFormattingVisualOperation);
    }

    function dispatchDirtyState(inst) {
        const payload = clone(inst.dirtyState || createInitialDirtyState());
        invokeBoundaryMethod(inst, 'HandleJsDirtyStateChanged', payload, 'dirty-state-dispatch-failed');
        return payload;
    }

    function dispatchBoundaryPatch(inst, patch) {
        if (!inst || !patch) return;
        recordTimeline(inst, 'blazor-patch-emit', {
            transactionId: patch.transactionId,
            transactionType: patch.transactionType,
            operationIds: patch.operationIds,
            affectedBlockIds: patch.affectedBlockIds,
        });
        invokeBoundaryMethod(inst, 'HandleJsBoundaryPatchGenerated', patch, 'boundary-patch-dispatch-failed');
    }

    function mergeBoundaryPatches(inst, patches, fallbackTransactionType) {
        const list = asArray(patches).filter(Boolean);
        const latest = list[list.length - 1] || null;
        if (!latest) return null;
        const operations = list.flatMap(function (patch) { return asArray(patch.operations).map(clone); });
        const affected = [];
        list.forEach(function (patch) {
            asArray(patch.affectedBlockIds).forEach(function (blockId) {
                if (blockId && affected.indexOf(blockId) < 0) affected.push(blockId);
            });
        });
        return sortObject(Object.assign({}, clone(latest), {
            transactionId: latest.transactionId || (list[0] && list[0].transactionId) || '',
            transactionType: fallbackTransactionType || latest.transactionType || 'default',
            operationIds: operations.map(getOperationId).filter(Boolean),
            operations,
            affectedBlockIds: affected,
            modelDelta: {
                kind: operations.length > 0 ? 'operations' : 'snapshot',
                operations,
            },
            dirtyState: clone(inst.dirtyState || latest.dirtyState || createInitialDirtyState()),
            coalescedPatchCount: list.length,
            createdAt: latest.createdAt || Date.now(),
        }));
    }

    function mergeTypingBoundaryPatches(inst, patches) {
        return mergeBoundaryPatches(inst, patches, TransactionTypes.Typing);
    }

    function scheduleTypingBoundaryPatchDispatch(inst, patch) {
        if (!inst || !patch) return;
        inst.pendingTypingBoundaryPatches = asArray(inst.pendingTypingBoundaryPatches).concat([patch]);
        const stats = ensurePerformanceStats(inst);
        stats.maxTypingBatchSize = Math.max(Number(stats.maxTypingBatchSize || 0), inst.pendingTypingBoundaryPatches.length);
        stats.maxBoundaryPatchBatchSize = Math.max(Number(stats.maxBoundaryPatchBatchSize || 0), inst.pendingTypingBoundaryPatches.length);
        if (inst.pendingTypingBoundaryTimer) clearTimeout(inst.pendingTypingBoundaryTimer);
        const delay = Math.max(0, Number((inst.options && (inst.options.TypingBatchMs || inst.options.typingBatchMs)) || 500) || 500);
        inst.pendingTypingBoundaryTimer = setTimeout(function () {
            flushTypingBoundaryPatchDispatch(inst);
        }, delay);
        if (inst.timers && inst.timers.indexOf(inst.pendingTypingBoundaryTimer) < 0) inst.timers.push(inst.pendingTypingBoundaryTimer);
        recordTimeline(inst, 'blazor-patch-queued', {
            transactionId: patch.transactionId,
            transactionType: patch.transactionType,
            pendingPatchCount: inst.pendingTypingBoundaryPatches.length,
        });
    }

    function flushTypingBoundaryPatchDispatch(inst) {
        if (!inst) return null;
        if (inst.pendingTypingBoundaryTimer) {
            clearTimeout(inst.pendingTypingBoundaryTimer);
            inst.pendingTypingBoundaryTimer = null;
        }
        const pending = asArray(inst.pendingTypingBoundaryPatches);
        if (!pending.length) return null;
        inst.pendingTypingBoundaryPatches = [];
        const stats = ensurePerformanceStats(inst);
        stats.typingFlushCount = Number(stats.typingFlushCount || 0) + 1;
        stats.maxBoundaryPatchBatchSize = Math.max(Number(stats.maxBoundaryPatchBatchSize || 0), pending.length);
        const merged = mergeTypingBoundaryPatches(inst, pending);
        if (merged) {
            hydrateBoundaryPatchSnapshot(inst, merged);
            dispatchBoundaryPatch(inst, merged);
            dispatchDirtyState(inst);
            flushRuntimeRevisionsChanged(inst);
        }
        return merged;
    }

    function scheduleDeferredBoundaryPatchDispatch(inst, patch) {
        if (!inst || !patch) return;
        inst.pendingDeferredBoundaryPatches = asArray(inst.pendingDeferredBoundaryPatches).concat([patch]);
        const stats = ensurePerformanceStats(inst);
        stats.maxBoundaryPatchBatchSize = Math.max(Number(stats.maxBoundaryPatchBatchSize || 0), inst.pendingDeferredBoundaryPatches.length);
        if (inst.pendingDeferredBoundaryTimer) clearTimeout(inst.pendingDeferredBoundaryTimer);
        const configuredDelay = inst.options ? (inst.options.BoundaryPatchBatchMs ?? inst.options.boundaryPatchBatchMs) : null;
        const delay = Math.max(0, Number(configuredDelay ?? 16) || 16);
        inst.pendingDeferredBoundaryTimer = setTimeout(function () {
            flushDeferredBoundaryPatchDispatch(inst);
        }, delay);
        if (inst.timers && inst.timers.indexOf(inst.pendingDeferredBoundaryTimer) < 0) inst.timers.push(inst.pendingDeferredBoundaryTimer);
        recordTimeline(inst, 'blazor-patch-deferred', {
            transactionId: patch.transactionId,
            transactionType: patch.transactionType,
            pendingPatchCount: inst.pendingDeferredBoundaryPatches.length,
        });
    }

    function flushDeferredBoundaryPatchDispatch(inst) {
        if (!inst) return null;
        if (inst.pendingDeferredBoundaryTimer) {
            clearTimeout(inst.pendingDeferredBoundaryTimer);
            inst.pendingDeferredBoundaryTimer = null;
        }
        const pending = asArray(inst.pendingDeferredBoundaryPatches);
        if (!pending.length) return null;
        inst.pendingDeferredBoundaryPatches = [];
        const stats = ensurePerformanceStats(inst);
        stats.deferredBoundaryPatchDispatchCount = Number(stats.deferredBoundaryPatchDispatchCount || 0) + 1;
        stats.maxBoundaryPatchBatchSize = Math.max(Number(stats.maxBoundaryPatchBatchSize || 0), pending.length);
        const latest = pending[pending.length - 1] || null;
        const merged = mergeBoundaryPatches(inst, pending, (latest && latest.transactionType) || 'deferred');
        if (merged) {
            hydrateBoundaryPatchSnapshot(inst, merged);
            dispatchBoundaryPatch(inst, merged);
            dispatchDirtyState(inst);
            flushRuntimeRevisionsChanged(inst);
        }
        return merged;
    }

    function commitBoundaryPatch(inst, transaction, operations, committed, source) {
        const isTypingPatch = isTypingLikeTransactionType(transaction && transaction.type) || isTypingLikeTransactionType(source);
        const deferSnapshot = shouldDeferBoundarySnapshot(transaction, operations, source);
        const patch = createBoundaryPatch(inst, transaction, operations, committed, source, { deferSnapshot });
        inst.boundaryPatches.push(patch);
        updateDirtyState(inst, patch, source);
        patch.dirtyState = clone(inst.dirtyState);
        if (isTypingPatch) {
            scheduleTypingBoundaryPatchDispatch(inst, patch);
            return patch;
        }
        if (deferSnapshot) {
            flushTypingBoundaryPatchDispatch(inst);
            scheduleDeferredBoundaryPatchDispatch(inst, patch);
            return patch;
        }
        flushTypingBoundaryPatchDispatch(inst);
        flushDeferredBoundaryPatchDispatch(inst);
        dispatchBoundaryPatch(inst, patch);
        dispatchDirtyState(inst);
        return patch;
    }

    return Object.freeze({
        hydrateBoundaryPatchSnapshot,
        createBoundaryPatch,
        updateDirtyState,
        shouldDeferBoundarySnapshot,
        dispatchDirtyState,
        commitBoundaryPatch,
        dispatchBoundaryPatch,
        mergeBoundaryPatches,
        mergeTypingBoundaryPatches,
        scheduleTypingBoundaryPatchDispatch,
        flushTypingBoundaryPatchDispatch,
        scheduleDeferredBoundaryPatchDispatch,
        flushDeferredBoundaryPatchDispatch,
    });
}
