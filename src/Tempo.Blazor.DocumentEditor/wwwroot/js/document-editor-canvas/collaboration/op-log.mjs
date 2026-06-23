import { asArray, asText, clone, sortObject } from '../../document-editor/core/helpers.mjs';

const PROTOCOL_VERSION = 1;

export function createCanvasOperationLog(options = {}) {
    const documentId = asText(options.documentId || options.model?.documentId || 'canvas-document');
    const clientId = asText(options.clientId || `canvas-${randomId()}`);
    let localSequence = Number(options.localSequence || 0) || 0;
    let remoteSequence = Number(options.remoteSequence || 0) || 0;
    const localBatches = [];
    const pendingLocalBatches = [];
    const remoteBatches = [];
    const remoteCursors = new Map();

    function recordLocalChange(change = {}) {
        const beforeModel = change.beforeModel || change.before || {};
        const afterModel = change.afterModel || change.after || change.model || {};
        const operations = diffModels(beforeModel, afterModel, {
            clientId,
            sequence: localSequence + 1,
            source: change.operation || change.source || 'canvasChange',
        });
        if (operations.length === 0) {
            return null;
        }

        localSequence += 1;
        const batch = sortObject({
            id: `canvas-local-${clientId}-${localSequence}`,
            documentId: asText(afterModel.documentId || beforeModel.documentId || documentId),
            protocolVersion: PROTOCOL_VERSION,
            baseVersionId: asText(beforeModel.version ?? ''),
            clientId,
            transactionId: asText(change.transactionId || `canvas-tx-${clientId}-${localSequence}`),
            localSequence,
            selectionAfter: clone(change.selection || change.selectionAfter || null),
            operations,
        });
        localBatches.push(batch);
        pendingLocalBatches.push(batch);
        return batch;
    }

    function appendRemoteBatch(batch = {}) {
        const normalized = normalizeRemoteBatch(batch);
        if (!normalized) {
            return null;
        }

        remoteSequence = Math.max(remoteSequence, Number(normalized.sequence || 0) || 0);
        remoteBatches.push(normalized);
        return normalized;
    }

    // B1: hand the not-yet-relayed local batches to the host (C#), which relays them to collaborators.
    // Removes them from the pending queue (ownership transfers to the host transport). The host is the dumb
    // pipe — it forwards each batch's JSON verbatim; remotes apply it via applyRemoteOperationBatch.
    function takeLocalBatches() {
        const taken = pendingLocalBatches.splice(0, pendingLocalBatches.length);
        return taken.map(clone);
    }

    function acknowledgeThrough(sequence) {
        const value = Number(sequence || 0) || 0;
        if (value <= 0) {
            return;
        }

        for (let index = pendingLocalBatches.length - 1; index >= 0; index -= 1) {
            if ((Number(pendingLocalBatches[index].localSequence || 0) || 0) <= value) {
                pendingLocalBatches.splice(index, 1);
            }
        }
    }

    function upsertCursor(cursor = {}) {
        const key = asText(cursor.sessionId || cursor.clientId);
        if (!key) {
            return [];
        }

        if ((Number(cursor.offset) || 0) < 0 || !asText(cursor.displayName)) {
            remoteCursors.delete(key);
        } else {
            remoteCursors.set(key, normalizeCursor(cursor));
        }

        return cursors();
    }

    function replaceCursors(items = []) {
        remoteCursors.clear();
        for (const cursor of asArray(items)) {
            const normalized = normalizeCursor(cursor);
            const key = asText(normalized.sessionId || normalized.clientId);
            if (key && asText(normalized.displayName) && (Number(normalized.offset) || 0) >= 0) {
                remoteCursors.set(key, normalized);
            }
        }

        return cursors();
    }

    function cursors() {
        return Array.from(remoteCursors.values())
            .sort((left, right) => asText(left.sessionId).localeCompare(asText(right.sessionId)));
    }

    function snapshot() {
        return sortObject({
            protocolVersion: PROTOCOL_VERSION,
            documentId,
            clientId,
            localSequence,
            remoteSequence,
            pendingLocalBatches: pendingLocalBatches.map(clone),
            localBatches: localBatches.map(clone),
            remoteBatches: remoteBatches.map(clone),
            remoteCursors: cursors(),
        });
    }

    return Object.freeze({
        clientId,
        documentId,
        recordLocalChange,
        takeLocalBatches,
        appendRemoteBatch,
        acknowledgeThrough,
        upsertCursor,
        replaceCursors,
        cursors,
        snapshot,
    });
}

export function diffModels(beforeModel = {}, afterModel = {}, options = {}) {
    const operations = [];
    const beforeBlocks = flattenBlocks(beforeModel);
    const afterBlocks = flattenBlocks(afterModel);
    const beforeById = new Map(beforeBlocks.map((entry, index) => [entry.block.id, { ...entry, index }]));
    const afterById = new Map(afterBlocks.map((entry, index) => [entry.block.id, { ...entry, index }]));

    for (const entry of afterBlocks) {
        if (!beforeById.has(entry.block.id)) {
            operations.push(createOperation('insertBlock', entry.block.id, options, {
                target: {
                    blockId: entry.block.id,
                    tableCellId: entry.cellId || null,
                    order: entry.index,
                },
                block: clone(entry.block),
            }));
        }
    }

    for (const entry of beforeBlocks) {
        if (!afterById.has(entry.block.id)) {
            operations.push(createOperation('deleteBlock', entry.block.id, options, {
                target: {
                    blockId: entry.block.id,
                    tableCellId: entry.cellId || null,
                    order: entry.index,
                },
                block: clone(entry.block),
            }));
        }
    }

    for (const entry of afterBlocks) {
        const beforeEntry = beforeById.get(entry.block.id);
        if (!beforeEntry) {
            continue;
        }

        if (beforeEntry.index !== entry.index || beforeEntry.cellId !== entry.cellId) {
            operations.push(createOperation('moveBlock', entry.block.id, options, {
                target: {
                    blockId: entry.block.id,
                    tableCellId: entry.cellId || null,
                    order: entry.index,
                },
            }));
        }

        const textDiff = diffTextBlock(beforeEntry.block, entry.block);
        if (textDiff) {
            operations.push(createOperation(textDiff.type, entry.block.id, options, {
                target: {
                    blockId: entry.block.id,
                    tableCellId: entry.cellId || null,
                    offset: textDiff.offset,
                    length: textDiff.length,
                },
                text: textDiff.text,
            }));
            continue;
        }

        if (JSON.stringify(sortObject(beforeEntry.block)) !== JSON.stringify(sortObject(entry.block))) {
            operations.push(createOperation('updateBlock', entry.block.id, options, {
                target: {
                    blockId: entry.block.id,
                    tableCellId: entry.cellId || null,
                    order: entry.index,
                },
                block: clone(entry.block),
            }));
        }
    }

    return operations;
}

function diffTextBlock(beforeBlock, afterBlock) {
    if (!hasTextRuns(beforeBlock) || !hasTextRuns(afterBlock)) {
        return null;
    }

    const beforeText = blockText(beforeBlock);
    const afterText = blockText(afterBlock);
    if (beforeText === afterText) {
        return null;
    }

    let prefix = 0;
    while (prefix < beforeText.length
        && prefix < afterText.length
        && beforeText[prefix] === afterText[prefix]) {
        prefix += 1;
    }

    let suffix = 0;
    while (suffix < beforeText.length - prefix
        && suffix < afterText.length - prefix
        && beforeText[beforeText.length - suffix - 1] === afterText[afterText.length - suffix - 1]) {
        suffix += 1;
    }

    const removed = beforeText.slice(prefix, beforeText.length - suffix);
    const inserted = afterText.slice(prefix, afterText.length - suffix);
    if (removed && inserted) {
        return null;
    }

    if (inserted) {
        return {
            type: 'insertText',
            offset: prefix,
            length: inserted.length,
            text: inserted,
        };
    }

    return {
        type: 'deleteText',
        offset: prefix,
        length: removed.length,
        text: removed,
    };
}

function createOperation(type, blockId, options, patch) {
    const sequence = Number(options.sequence || 0) || 0;
    const key = `${asText(options.clientId)}-${sequence}-${type}-${blockId}-${patch.target?.offset ?? patch.target?.order ?? 0}`;
    return sortObject({
        operationId: `canvas-op-${key}`,
        schemaVersion: PROTOCOL_VERSION,
        type,
        target: patch.target || {},
        metadata: {
            clientId: asText(options.clientId),
            source: asText(options.source || 'canvasChange'),
            createdAt: new Date().toISOString(),
        },
        text: patch.text ?? null,
        block: patch.block ?? null,
    });
}

// Each TOP-LEVEL block is one diff unit. We deliberately do NOT recurse into table cells: a table's
// cells/rows live inside the table block, so any cell change is captured as a single `updateBlock(table)`
// carrying the whole table (the accepted `updateBlock` granularity). Recursing produced two bugs that broke
// operation-relay (Phase B0.4): a redundant insertText/updateBlock on the cell IN ADDITION to the table
// updateBlock (double-apply on remote), and a spurious moveBlock for every nested cell block (because the
// global map index was compared against the within-array index).
function flattenBlocks(model) {
    const result = [];
    asArray(model?.body?.blocks || model?.blocks || []).forEach((block, index) => {
        if (!block || !asText(block.id)) {
            return;
        }

        result.push({ block, index, cellId: null });
    });

    return result;
}

function hasTextRuns(block) {
    return asArray(block?.content?.runs).length > 0;
}

function blockText(block) {
    return asArray(block?.content?.runs).map(run => asText(run?.text)).join('');
}

function normalizeRemoteBatch(batch) {
    const source = batch && batch.batch ? batch : { sequence: batch?.sequence ?? 0, batch };
    const payload = source.batch || {};
    if (!payload || !Array.isArray(payload.operations)) {
        return null;
    }

    return sortObject({
        sequence: Number(source.sequence || 0) || 0,
        sessionId: asText(source.sessionId || ''),
        batch: {
            ...clone(payload),
            operations: payload.operations.map(operation => sortObject(operation)),
        },
    });
}

function normalizeCursor(cursor) {
    return sortObject({
        documentId: asText(cursor.documentId || ''),
        sessionId: asText(cursor.sessionId || ''),
        clientId: asText(cursor.clientId || ''),
        displayName: asText(cursor.displayName || ''),
        blockId: asText(cursor.blockId || ''),
        inlineIndex: cursor.inlineIndex == null ? null : Number(cursor.inlineIndex),
        offset: cursor.offset == null ? null : Number(cursor.offset),
        color: asText(cursor.color || ''),
        updatedAt: asText(cursor.updatedAt || ''),
    });
}

function randomId() {
    if (globalThis.crypto?.randomUUID) {
        return globalThis.crypto.randomUUID();
    }

    return Math.random().toString(36).slice(2);
}
