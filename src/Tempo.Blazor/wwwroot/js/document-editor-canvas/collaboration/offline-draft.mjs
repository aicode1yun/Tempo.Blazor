import { asArray, asText, clone, sortObject } from '../../document-editor/core/helpers.mjs';

export const CANVAS_OFFLINE_DRAFT_SCHEMA_VERSION = 1;

export function createCanvasOfflineDraft(options = {}) {
    const documentId = asText(options.documentId || options.runtimeState?.model?.documentId);
    if (!documentId) {
        throw new Error('Canvas offline draft requires a document id.');
    }

    const runtimeState = clone(options.runtimeState || {});
    const jsonSnapshot = asText(options.jsonSnapshot || JSON.stringify(runtimeState.model || {}));
    const updatedAt = normalizeTimestamp(options.updatedAt || new Date().toISOString());

    return sortObject({
        schemaVersion: CANVAS_OFFLINE_DRAFT_SCHEMA_VERSION,
        id: asText(options.id || `canvas-draft-${documentId}-${updatedAt.replace(/[^0-9a-z]/gi, '')}`),
        documentId,
        baseVersionId: options.baseVersionId == null ? null : asText(options.baseVersionId),
        jsonSnapshot,
        runtimeState,
        runtimeStateJson: JSON.stringify(runtimeState),
        runtimeDirtyEpoch: Number(runtimeState.dirtyEpoch || 0) || 0,
        runtimeUndoEpoch: Number(runtimeState.undoEpoch || 0) || 0,
        operationBatches: asArray(options.operationBatches).map(clone),
        state: asText(options.state || 'PendingSync'),
        syncStatus: asText(options.syncStatus || 'Offline'),
        updatedAt,
    });
}

export function selectCanvasOfflineResumeDraft(drafts = [], serverDocument = {}, options = {}) {
    const pending = asArray(drafts)
        .filter(draft => asText(draft.state || draft.State) !== 'Synced')
        .sort((left, right) => normalizeTimestamp(right.updatedAt || right.UpdatedAt)
            .localeCompare(normalizeTimestamp(left.updatedAt || left.UpdatedAt)));

    const draft = pending[0] || null;
    if (!draft) {
        return { action: 'none', draft: null, model: clone(serverDocument), runtimeState: null };
    }

    const preferLocal = options.preferLocalDraft === true;
    const serverTimestamp = normalizeTimestamp(
        serverDocument?.metadata?.modifiedAt
        || serverDocument?.metadata?.createdAt
        || serverDocument?.Metadata?.ModifiedAt
        || serverDocument?.Metadata?.CreatedAt
        || '');
    const draftTimestamp = normalizeTimestamp(draft.updatedAt || draft.UpdatedAt);
    const useLocal = preferLocal && (!serverTimestamp || draftTimestamp > serverTimestamp);

    return {
        action: useLocal ? 'resumeLocal' : 'serverSnapshot',
        draft: clone(draft),
        model: useLocal ? parseSnapshot(draft.jsonSnapshot || draft.JsonSnapshot) : clone(serverDocument),
        runtimeState: useLocal ? parseRuntimeState(draft.runtimeState || draft.runtimeStateJson || draft.RuntimeStateJson) : null,
    };
}

export async function syncCanvasOfflineDraft(draft, syncProvider) {
    if (!syncProvider || typeof syncProvider.syncDraft !== 'function') {
        throw new Error('Canvas offline sync requires a sync provider.');
    }

    try {
        const result = await syncProvider.syncDraft(clone(draft));
        return classifyCanvasOfflineSyncResult(draft, result);
    } catch (error) {
        return {
            success: false,
            status: 'Failed',
            draft: {
                ...clone(draft),
                syncStatus: 'Failed',
            },
            errorMessage: asText(error?.message || error),
        };
    }
}

export function classifyCanvasOfflineSyncResult(draft, result = {}) {
    if (result.success === true || result.Success === true) {
        return {
            success: true,
            status: 'Online',
            draft: {
                ...clone(draft),
                state: 'Synced',
                syncStatus: 'Online',
            },
            saveResult: clone(result.saveResult || result.SaveResult || null),
        };
    }

    const conflict = result.conflict || result.Conflict || null;
    if (conflict) {
        return {
            success: false,
            status: 'Conflict',
            draft: {
                ...clone(draft),
                state: 'Conflict',
                syncStatus: 'Conflict',
            },
            conflict: clone(conflict),
        };
    }

    return {
        success: false,
        status: 'Failed',
        draft: {
            ...clone(draft),
            syncStatus: 'Failed',
        },
        errorMessage: asText(result.errorMessage || result.ErrorMessage || ''),
    };
}

function parseSnapshot(json) {
    try {
        return JSON.parse(asText(json));
    } catch {
        return {};
    }
}

function parseRuntimeState(value) {
    if (!value) {
        return null;
    }

    if (typeof value === 'string') {
        try {
            return JSON.parse(value);
        } catch {
            return null;
        }
    }

    return clone(value);
}

function normalizeTimestamp(value) {
    if (!value) {
        return '';
    }

    const date = value instanceof Date ? value : new Date(value);
    return Number.isNaN(date.getTime()) ? asText(value) : date.toISOString();
}
