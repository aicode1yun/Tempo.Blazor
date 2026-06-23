// Phase D — runtime/operation-performance.mjs
// `recordOperationPerformance(...)` — strict-mode accumulator that the engine calls
// after each apply-operation batch. Tracks total input operation timing + per-op-type
// latency buckets (typing for InsertText/DeleteRange/Split/Merge, image-drag for
// UpdateImageLayout), and records an `operation-performance` timeline entry.

import { asArray, asText } from '../core/helpers.mjs';
import { OperationTypes } from '../history/operation-types.mjs';

export function recordOperationPerformance(
    ensureStats, recordTimelineFn, inst, operationList, elapsedMs, invalidatedScopes, source) {
    if (!inst) return null;
    const stats = ensureStats(inst);
    if (!stats) return null;
    const elapsed = Math.max(0, Number(elapsedMs || 0) || 0);
    const operations = asArray(operationList);
    const scopes = asArray(invalidatedScopes).map(asText).filter(Boolean);
    const isFullDocument = scopes.indexOf('document') >= 0 || scopes.length === 0;

    stats.inputOperationCount = Number(stats.inputOperationCount || 0) + operations.length;
    stats.inputOperationLastMs = elapsed;
    stats.inputOperationTotalMs = Number(stats.inputOperationTotalMs || 0) + elapsed;
    stats.inputOperationMaxMs = Math.max(Number(stats.inputOperationMaxMs || 0), elapsed);
    stats.incrementalOperationCount = Number(stats.incrementalOperationCount || 0)
        + (isFullDocument ? 0 : operations.length);
    stats.fullDocumentLayoutCount = Number(stats.fullDocumentLayoutCount || 0)
        + (isFullDocument ? 1 : 0);
    stats.lastInputOperationType = operations
        .map(function (operation) { return operation.type || operation.Type || ''; })
        .filter(Boolean)
        .join(',') || asText(source || '');

    operations.forEach(function (operation) {
        const type = operation.type || operation.Type || '';
        if (type === OperationTypes.InsertText
            || type === OperationTypes.DeleteRange
            || type === OperationTypes.SplitParagraph
            || type === OperationTypes.MergeParagraph) {
            stats.typingLatencyCount = Number(stats.typingLatencyCount || 0) + 1;
            stats.typingLatencyLastMs = elapsed;
            stats.typingLatencyTotalMs = Number(stats.typingLatencyTotalMs || 0) + elapsed;
            stats.typingLatencyMaxMs = Math.max(Number(stats.typingLatencyMaxMs || 0), elapsed);
        }
        if (type === OperationTypes.UpdateImageLayout) {
            stats.imageDragLatencyCount = Number(stats.imageDragLatencyCount || 0) + 1;
            stats.imageDragLatencyLastMs = elapsed;
            stats.imageDragLatencyTotalMs = Number(stats.imageDragLatencyTotalMs || 0) + elapsed;
            stats.imageDragLatencyMaxMs = Math.max(Number(stats.imageDragLatencyMaxMs || 0), elapsed);
        }
    });

    if (typeof recordTimelineFn === 'function') {
        recordTimelineFn(inst, 'operation-performance', {
            source: source || '',
            elapsedMs: elapsed,
            operationTypes: operations.map(function (operation) {
                return operation.type || operation.Type || '';
            }),
            invalidatedScopes: scopes,
            fullDocumentLayout: isFullDocument,
        });
    }
    return stats;
}
