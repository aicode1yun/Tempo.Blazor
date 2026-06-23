import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCollaborationClient } from '../collab-client-adapter.mjs';
import {
    createCanvasOfflineDraft,
    selectCanvasOfflineResumeDraft,
    syncCanvasOfflineDraft,
} from '../offline-draft.mjs';
import { diffModels } from '../op-log.mjs';

test('offline draft save and resume preserve canvas model, runtime state, and operation batches', () => {
    const before = documentModel('phase-20-offline-js', 'p1', 'Start');
    const after = documentModel('phase-20-offline-js', 'p1', 'Start offline');
    const operations = diffModels(before, after, { clientId: 'client-a', sequence: 1, source: 'typing' });
    const runtimeState = {
        schemaVersion: 1,
        engine: 'CanvasDocumentEngine',
        dirtyEpoch: 3,
        undoEpoch: 2,
        model: after,
        collaboration: {
            protocolVersion: 1,
            pendingLocalBatches: [{ id: 'batch-1', operations }],
        },
    };

    const draft = createCanvasOfflineDraft({
        documentId: after.documentId,
        baseVersionId: 'v1',
        runtimeState,
        operationBatches: [{ id: 'batch-1', operations }],
        updatedAt: '2026-06-05T10:00:00.000Z',
    });
    const resume = selectCanvasOfflineResumeDraft([draft], {
        documentId: after.documentId,
        metadata: { modifiedAt: '2026-06-05T09:59:00.000Z' },
    }, { preferLocalDraft: true });

    assert.equal(JSON.parse(JSON.stringify(draft)).documentId, 'phase-20-offline-js');
    assert.equal(draft.runtimeDirtyEpoch, 3);
    assert.equal(draft.runtimeUndoEpoch, 2);
    assert.equal(draft.operationBatches[0].operations[0].type, 'insertText');
    assert.equal(resume.action, 'resumeLocal');
    assert.equal(resume.model.body.blocks[0].content.runs[0].text, 'Start offline');
    assert.equal(resume.runtimeState.collaboration.pendingLocalBatches[0].operations[0].text, ' offline');
});

test('offline sync retries after reconnect through the reused collaboration relay client', async () => {
    const sentMessages = [];
    const appliedOperations = [];
    const client = createCanvasCollaborationClient({
        clientId: 'client-a',
        send: message => sentMessages.push(message),
        applyRemoteOperation: operation => appliedOperations.push(operation),
    });
    const operation = {
        operationId: 'op-a',
        type: 'insertText',
        target: { blockId: 'p1', offset: 5, length: 8 },
        metadata: { clientId: 'client-a' },
        text: ' offline',
    };

    client.localOperation(operation);
    assert.equal(sentMessages[0].ops[0].type, 'insertText');
    client.receiveServerChange({ sequence: 1, base: 0, clientId: 'client-b', ops: [{
        operationId: 'op-b',
        type: 'insertText',
        target: { blockId: 'p1', offset: 5, length: 7 },
        metadata: { clientId: 'client-b' },
        text: ' remote',
    }] });
    assert.equal(appliedOperations[0].target.offset, 5);
    assert.equal(client.getState().outstanding.ops[0].target.offset, 12);

    let connected = false;
    const draft = createCanvasOfflineDraft({
        documentId: 'phase-20-offline-js',
        baseVersionId: 'v1',
        runtimeState: { model: documentModel('phase-20-offline-js', 'p1', 'Start offline') },
        operationBatches: [{ id: 'offline-batch', operations: sentMessages[0].ops }],
    });
    const syncProvider = {
        async syncDraft(candidate) {
            if (!connected) {
                throw new Error('Transport disconnected.');
            }

            return {
                success: true,
                saveResult: {
                    documentId: candidate.documentId,
                    concurrencyToken: 'v2',
                },
            };
        },
    };

    const failed = await syncCanvasOfflineDraft(draft, syncProvider);
    connected = true;
    const synced = await syncCanvasOfflineDraft(draft, syncProvider);

    assert.equal(failed.status, 'Failed');
    assert.equal(synced.success, true);
    assert.equal(synced.status, 'Online');
    assert.equal(synced.draft.state, 'Synced');
    assert.equal(synced.saveResult.concurrencyToken, 'v2');
});

test('offline sync keeps conflict metadata when the server rejects the draft base version', async () => {
    const draft = createCanvasOfflineDraft({
        documentId: 'phase-20-offline-conflict',
        baseVersionId: 'v1',
        runtimeState: { model: documentModel('phase-20-offline-conflict', 'p1', 'Local') },
    });

    const result = await syncCanvasOfflineDraft(draft, {
        async syncDraft() {
            return {
                success: false,
                conflict: {
                    documentId: 'phase-20-offline-conflict',
                    localBaseVersionId: 'v1',
                    serverVersionId: 'v2',
                    reason: 'Base version is stale.',
                },
            };
        },
    });

    assert.equal(result.success, false);
    assert.equal(result.status, 'Conflict');
    assert.equal(result.draft.state, 'Conflict');
    assert.equal(result.conflict.serverVersionId, 'v2');
});

function documentModel(documentId, blockId, text) {
    return {
        documentId,
        body: {
            blocks: [{
                id: blockId,
                type: 'paragraph',
                content: {
                    type: 'paragraph',
                    runs: [{ id: `${blockId}-run`, type: 'text', text, marks: [] }],
                },
            }],
        },
    };
}
