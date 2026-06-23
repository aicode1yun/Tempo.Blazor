// Phase D — runtime/watchdog.mjs
// `createWatchdogInstaller({ getMarkers?, upsertMarker?, getDebugSnapshot? })` →
//   `installWatchdog(runtime)` → patches runtime.create / loadDocument / getDocument /
//   executeCommand / applyRemoteOperation(Batch) with try-catch + exponential-backoff
//   recovery, maintains per-instance watchdog contexts, and exposes
//   `runtime.__watchdog` for diagnostics and forced simulation.
//
// Pure watchdog helpers (state constants, backoff math, event recording, context
// factory) are imported from watchdog-helpers.mjs. The only truly injected deps are
// optional overrides for runtime calls that differ across host environments (e.g.
// `getMarkers`, `getDebugSnapshot`). All other runtime method references are obtained
// directly from the `runtime` object passed to `installWatchdog`.
//
// `uninstall()` restores the original runtime methods and clears all contexts.

import {
    WD_READY, WD_RECOVERING, WD_FAILED,
    WD_DEFAULT_MAX_ATTEMPTS, WD_DEFAULT_BACKOFF_MS,
    cloneWatchdogJson, parseWatchdogJson,
    unwrapWatchdogDocumentSnapshot, wrapWatchdogDocumentSnapshot,
    safeCall, watchdogNow,
    recordWatchdogEvent,
    createWatchdogContext,
} from './watchdog-helpers.mjs';

export function createWatchdogInstaller(options) {
    const opts = options || {};

    return function installWatchdog(runtime) {
        if (!runtime || typeof runtime !== 'object') {
            throw new TypeError('installWatchdog requires a runtime object');
        }

        const watchdogContexts = new Map();

        // ---- private helpers --------------------------------------------------------

        function wdGet(instanceId) {
            return watchdogContexts.get(String(instanceId || '')) || null;
        }

        function notifyDotNet(wd, methodName, detail) {
            if (!wd || !wd.dotNetRef) return;
            try {
                wd.dotNetRef.invokeMethodAsync(methodName, detail || wd.lastRecoveryDetail || null);
            } catch { /* ignore */ }
        }

        function readMarkers(instanceId) {
            if (typeof opts.getMarkers === 'function') {
                return safeCall(() => opts.getMarkers(instanceId), []);
            }
            if (typeof runtime.getMarkers === 'function') {
                return safeCall(() => runtime.getMarkers(instanceId), []);
            }
            return [];
        }

        function readUploadState(debugSnapshot) {
            const pendingUploads = debugSnapshot && (debugSnapshot.PendingUploads || debugSnapshot.pendingUploads);
            const pendingUploadCount = debugSnapshot
                ? Number(debugSnapshot.PendingUploadCount ?? debugSnapshot.pendingUploadCount
                    ?? (Array.isArray(pendingUploads) ? pendingUploads.length : 0))
                : 0;
            return {
                pendingUploadCount: pendingUploadCount || 0,
                PendingUploadCount: pendingUploadCount || 0,
                pendingUploads: Array.isArray(pendingUploads) ? cloneWatchdogJson(pendingUploads) : [],
                PendingUploads: Array.isArray(pendingUploads) ? cloneWatchdogJson(pendingUploads) : [],
            };
        }

        // ---- core lifecycle functions -----------------------------------------------

        function captureStableSnapshot(instanceId, reason) {
            const getDbg = typeof opts.getDebugSnapshot === 'function'
                ? opts.getDebugSnapshot
                : (id) => safeCall(() => origGetDocument(id), null);
            const debugSnapshot = safeCall(() => getDbg(instanceId), null);
            const runtimeSnapshot = parseWatchdogJson(safeCall(() => origGetDocument(instanceId), null));
            const doc = unwrapWatchdogDocumentSnapshot(runtimeSnapshot);
            const snapshot = {
                capturedAt: watchdogNow(),
                CapturedAt: watchdogNow(),
                reason: reason || '',
                Reason: reason || '',
                document: doc,
                Document: null,
                markers: cloneWatchdogJson(readMarkers(instanceId) || []),
                Markers: null,
                selection: cloneWatchdogJson(
                    safeCall(() => runtime.getSelectionSnapshot && runtime.getSelectionSnapshot(instanceId), null)
                    || safeCall(() => runtime.getRuntimeSelection && runtime.getRuntimeSelection(instanceId), null)),
                Selection: null,
                undoState: cloneWatchdogJson(safeCall(() => runtime.getUndoState && runtime.getUndoState(instanceId), null)),
                UndoState: null,
                undoDebug: cloneWatchdogJson(safeCall(() => runtime.getDebugUndoStack && runtime.getDebugUndoStack(instanceId), null)),
                UndoDebug: null,
                uploadState: readUploadState(debugSnapshot),
                UploadState: null,
            };
            snapshot.Document = snapshot.document;
            snapshot.Markers = snapshot.markers;
            snapshot.Selection = snapshot.selection;
            snapshot.UndoState = snapshot.undoState;
            snapshot.UndoDebug = snapshot.undoDebug;
            snapshot.UploadState = snapshot.uploadState;
            return snapshot;
        }

        function rememberStableSnapshot(instanceId, wd, reason) {
            if (!wd) return null;
            const snapshot = captureStableSnapshot(instanceId, reason);
            if (snapshot && snapshot.document) {
                wd.stableSnapshot = snapshot;
            }
            return wd.stableSnapshot;
        }

        function rememberStableSnapshotFromDocument(instanceId, wd, reason, documentSnapshot) {
            if (!wd) return null;
            const doc = unwrapWatchdogDocumentSnapshot(parseWatchdogJson(documentSnapshot));
            if (!doc) return wd.stableSnapshot;
            const getDbg = typeof opts.getDebugSnapshot === 'function'
                ? opts.getDebugSnapshot
                : (id) => safeCall(() => origGetDocument(id), null);
            const debugSnapshot = safeCall(() => getDbg(instanceId), null);
            const snapshot = {
                capturedAt: watchdogNow(),
                CapturedAt: watchdogNow(),
                reason: reason || '',
                Reason: reason || '',
                document: doc,
                Document: doc,
                markers: cloneWatchdogJson(readMarkers(instanceId) || []),
                Markers: null,
                selection: cloneWatchdogJson(
                    safeCall(() => runtime.getSelectionSnapshot && runtime.getSelectionSnapshot(instanceId), null)
                    || safeCall(() => runtime.getRuntimeSelection && runtime.getRuntimeSelection(instanceId), null)),
                Selection: null,
                undoState: cloneWatchdogJson(safeCall(() => runtime.getUndoState && runtime.getUndoState(instanceId), null)),
                UndoState: null,
                undoDebug: cloneWatchdogJson(safeCall(() => runtime.getDebugUndoStack && runtime.getDebugUndoStack(instanceId), null)),
                UndoDebug: null,
                uploadState: readUploadState(debugSnapshot),
                UploadState: null,
            };
            snapshot.Markers = snapshot.markers;
            snapshot.Selection = snapshot.selection;
            snapshot.UndoState = snapshot.undoState;
            snapshot.UndoDebug = snapshot.undoDebug;
            snapshot.UploadState = snapshot.uploadState;
            wd.stableSnapshot = snapshot;
            return wd.stableSnapshot;
        }

        function restoreStableSnapshotExtras(instanceId, stableSnapshot) {
            if (!stableSnapshot) return;
            const markers = stableSnapshot.markers || stableSnapshot.Markers || [];
            if (Array.isArray(markers)) {
                markers.forEach(function (marker) {
                    safeCall(function () {
                        if (typeof runtime.upsertMarker === 'function') {
                            return runtime.upsertMarker(instanceId, marker);
                        }
                        return null;
                    }, null);
                });
            }
            const selection = stableSnapshot.selection || stableSnapshot.Selection || null;
            if (selection) {
                safeCall(function () {
                    if (typeof runtime.restoreSelection === 'function') {
                        return runtime.restoreSelection(instanceId, selection);
                    }
                    return null;
                }, null);
            }
        }

        function captureRecoveryState(instanceId, wd) {
            const rawDoc = wd.forceSnapshotFallback ? null
                : safeCall(() => origGetDocument(instanceId), null);
            const snapshot = rawDoc
                ? unwrapWatchdogDocumentSnapshot(parseWatchdogJson(rawDoc))
                : null;
            const offlineState = safeCall(() => origGetOfflineState && origGetOfflineState(instanceId), null);
            let stableSnapshot = null;
            wd.usedSnapshotFallback = false;

            if (snapshot) {
                stableSnapshot = captureStableSnapshot(instanceId, 'recovery-live');
                stableSnapshot.document = snapshot;
                stableSnapshot.Document = snapshot;
            } else if (wd.stableSnapshot) {
                stableSnapshot = cloneWatchdogJson(wd.stableSnapshot);
                const fallbackDoc = stableSnapshot.document || stableSnapshot.Document || null;
                wd.usedSnapshotFallback = !!fallbackDoc;
                if (wd.usedSnapshotFallback) {
                    recordWatchdogEvent(wd, 'snapshotFallbackUsed', wd.lastErrorSource, null,
                        { usedSnapshotFallback: true, UsedSnapshotFallback: true });
                }
            }

            return { snapshot: snapshot || (stableSnapshot && (stableSnapshot.document || stableSnapshot.Document)) || null, offlineState, stableSnapshot };
        }

        function failRecovery(instanceId, wd, source, error) {
            wd.state = WD_FAILED;
            wd.currentBackoffMs = 0;
            const detail = recordWatchdogEvent(wd, 'runtimeRecoveryFailed', source, error);
            notifyDotNet(wd, 'HandleRuntimeRecoveryFailed', detail);
        }

        function attemptRecovery(instanceId, wd) {
            if (!wd || wd.state !== WD_RECOVERING) return;
            const recoveryState = captureRecoveryState(instanceId, wd);

            try { origDispose(instanceId); } catch { /* ignore */ }

            try {
                if (wd.forceRecoveryFailure) throw new Error('Forced watchdog recovery failure');
                origCreate(wd.rootEl, wd.options, wd.dotNetRef);
            } catch (error) {
                if (wd.attempt < wd.maxAttempts) {
                    wd.state = WD_READY;
                    scheduleRecovery(instanceId, wd, wd.lastErrorSource || 'unknown', error);
                    return;
                }
                failRecovery(instanceId, wd, wd.lastErrorSource || 'unknown', error);
                return;
            }

            try { if (recoveryState.snapshot) origLoadDocument(instanceId, recoveryState.snapshot); } catch { /* ignore */ }
            try { if (recoveryState.offlineState && origApplyOfflineState) origApplyOfflineState(instanceId, recoveryState.offlineState); } catch { /* ignore */ }
            restoreStableSnapshotExtras(instanceId, recoveryState.stableSnapshot);
            if (recoveryState.stableSnapshot) {
                wd.stableSnapshot = cloneWatchdogJson(recoveryState.stableSnapshot);
            }

            wd.state = 'recovered';
            wd.currentBackoffMs = 0;
            const detail = recordWatchdogEvent(wd, 'runtimeRecovered', wd.lastErrorSource || 'unknown', null);
            notifyDotNet(wd, 'HandleRuntimeRecovered', detail);
        }

        function scheduleRecovery(instanceId, wd, source, error) {
            if (!wd || wd.state === WD_RECOVERING) return;
            if (wd.attempt >= wd.maxAttempts) {
                failRecovery(instanceId, wd, source, error);
                return;
            }
            wd.state = WD_RECOVERING;
            wd.lastErrorSource = source || 'unknown';
            wd.attempt += 1;
            wd.currentBackoffMs = Math.max(0, wd.baseBackoffMs || WD_DEFAULT_BACKOFF_MS) * Math.pow(2, Math.max(0, wd.attempt - 1));
            recordWatchdogEvent(wd, 'runtimeRecoveryScheduled', source, error);
            setTimeout(() => attemptRecovery(instanceId, wd), wd.currentBackoffMs);
        }

        // ---- save originals before patching ----------------------------------------

        const origCreate = runtime.create;
        const origDispose = runtime.dispose;
        const origLoadDocument = runtime.loadDocument;
        const origGetDocument = runtime.getDocument;
        const origGetOfflineState = runtime.getOfflineState;
        const origApplyOfflineState = runtime.applyOfflineState;
        const origExecuteCommand = runtime.executeCommand;
        const origApplyBatch = runtime.applyRemoteOperationBatch;
        const origApplyRemoteOperation = runtime.applyRemoteOperation;

        // ---- patch runtime methods -------------------------------------------------

        runtime.create = function (rootEl, instanceOptions, dotNetRef) {
            const instanceId = String((instanceOptions && (instanceOptions.InstanceId || instanceOptions.instanceId)) || '');
            const result = origCreate.apply(runtime, arguments);
            if (instanceId) {
                const wd = createWatchdogContext(rootEl, instanceOptions, dotNetRef);
                watchdogContexts.set(instanceId, wd);
            }
            return result;
        };

        runtime.dispose = function (instanceId) {
            watchdogContexts.delete(String(instanceId || ''));
            return origDispose.apply(runtime, arguments);
        };

        runtime.loadDocument = function (instanceId) {
            try {
                const result = origLoadDocument.apply(runtime, arguments);
                const wd = wdGet(String(instanceId || ''));
                if (wd) {
                    wd.state = WD_READY;
                    wd.attempt = 0;
                    rememberStableSnapshotFromDocument(String(instanceId || ''), wd, 'loadDocument', arguments[1]);
                }
                return result;
            } catch (error) {
                const wd = wdGet(String(instanceId || ''));
                if (wd && wd.state !== WD_RECOVERING) {
                    scheduleRecovery(String(instanceId || ''), wd, 'render', error);
                }
                return undefined;
            }
        };

        runtime.getDocument = function (instanceId) {
            try {
                return origGetDocument.apply(runtime, arguments);
            } catch (error) {
                const wd = wdGet(String(instanceId || ''));
                if (wd && wd.state !== WD_RECOVERING) {
                    scheduleRecovery(String(instanceId || ''), wd, 'serialization', error);
                }
                return (wd && wd.stableSnapshot && wd.stableSnapshot.document)
                    ? JSON.stringify(wrapWatchdogDocumentSnapshot(wd.stableSnapshot.document))
                    : null;
            }
        };

        runtime.executeCommand = function (instanceId) {
            try {
                const result = origExecuteCommand.apply(runtime, arguments);
                const wd = wdGet(String(instanceId || ''));
                if (wd) {
                    wd.state = WD_READY;
                    wd.attempt = 0;
                    rememberStableSnapshot(String(instanceId || ''), wd, 'command');
                }
                return result;
            } catch (error) {
                const wd = wdGet(String(instanceId || ''));
                if (wd && wd.state !== WD_RECOVERING) {
                    scheduleRecovery(String(instanceId || ''), wd, 'command', error);
                }
                return undefined;
            }
        };

        if (origApplyBatch) {
            runtime.applyRemoteOperationBatch = function (instanceId) {
                try {
                    const result = origApplyBatch.apply(runtime, arguments);
                    const wd = wdGet(String(instanceId || ''));
                    if (wd) {
                        wd.state = WD_READY;
                        wd.attempt = 0;
                        rememberStableSnapshot(String(instanceId || ''), wd, 'remoteOperation');
                    }
                    return result;
                } catch (error) {
                    const wd = wdGet(String(instanceId || ''));
                    if (wd && wd.state !== WD_RECOVERING) {
                        scheduleRecovery(String(instanceId || ''), wd, 'remoteOperation', error);
                    }
                    return undefined;
                }
            };
        }

        if (origApplyRemoteOperation) {
            runtime.applyRemoteOperation = function (instanceId) {
                try {
                    const result = origApplyRemoteOperation.apply(runtime, arguments);
                    const wd = wdGet(String(instanceId || ''));
                    if (wd) rememberStableSnapshot(String(instanceId || ''), wd, 'remoteOperation');
                    return result;
                } catch (error) {
                    const wd = wdGet(String(instanceId || ''));
                    if (wd && wd.state !== WD_RECOVERING) {
                        scheduleRecovery(String(instanceId || ''), wd, 'remoteOperation', error);
                    }
                    return undefined;
                }
            };
        }

        // ---- public diagnostic API -------------------------------------------------

        const watchdogApi = {
            getState: (instanceId) => {
                const wd = wdGet(String(instanceId || ''));
                return wd ? wd.state : null;
            },
            getStableSnapshot: (instanceId) => {
                const wd = wdGet(String(instanceId || ''));
                return wd ? cloneWatchdogJson(wd.stableSnapshot) : null;
            },
            getLastRecoveryDetail: (instanceId) => {
                const wd = wdGet(String(instanceId || ''));
                return wd ? cloneWatchdogJson(wd.lastRecoveryDetail) : null;
            },
            getEvents: (instanceId) => {
                const wd = wdGet(String(instanceId || ''));
                return wd ? cloneWatchdogJson(wd.events || []) : [];
            },
            configure: (instanceId, configOptions) => {
                const wd = wdGet(String(instanceId || ''));
                if (!wd) return false;
                if (configOptions && configOptions.maxAttempts != null) wd.maxAttempts = Number(configOptions.maxAttempts) || wd.maxAttempts;
                if (configOptions && configOptions.baseBackoffMs != null) wd.baseBackoffMs = Number(configOptions.baseBackoffMs) || wd.baseBackoffMs;
                if (configOptions && configOptions.forceRecoveryFailure != null) wd.forceRecoveryFailure = !!configOptions.forceRecoveryFailure;
                if (configOptions && configOptions.forceSnapshotFallback != null) wd.forceSnapshotFallback = !!configOptions.forceSnapshotFallback;
                return true;
            },
            simulateCrash: (instanceId, source, simulateOptions) => {
                const wd = wdGet(String(instanceId || ''));
                if (!wd) return false;
                if (simulateOptions) watchdogApi.configure(instanceId, simulateOptions);
                scheduleRecovery(String(instanceId || ''), wd, source || 'command',
                    new Error((simulateOptions && simulateOptions.message) || 'Simulated watchdog crash'));
                return true;
            },
        };

        runtime.__watchdog = watchdogApi;

        // ---- uninstall helper (restores originals, clears contexts) ----------------

        function uninstall() {
            runtime.create = origCreate;
            runtime.dispose = origDispose;
            runtime.loadDocument = origLoadDocument;
            runtime.getDocument = origGetDocument;
            runtime.executeCommand = origExecuteCommand;
            if (origApplyBatch) runtime.applyRemoteOperationBatch = origApplyBatch;
            if (origApplyRemoteOperation) runtime.applyRemoteOperation = origApplyRemoteOperation;
            delete runtime.__watchdog;
            watchdogContexts.clear();
        }

        return { watchdogApi, uninstall };
    };
}
