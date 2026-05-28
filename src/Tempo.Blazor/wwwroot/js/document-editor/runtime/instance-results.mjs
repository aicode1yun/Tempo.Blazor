// Phase D — runtime/instance-results.mjs
// Stable error envelope shapes for the runtime's "instance not available" cases.
// Used by every method on `runtime.*` to return a predictable `{ ok, error }` when
// the instanceId is unknown or has been disposed.
//
// Pure — small but referenced from dozens of call sites.

export function disposedResult(instanceId, methodName) {
    return {
        ok: false,
        error: {
            code: 'disposed',
            message: 'tmDocumentEditorEngine.' + methodName + ': instance is disposed.',
            instanceId: instanceId || '',
        },
    };
}

export function missingResult(instanceId, methodName) {
    return {
        ok: false,
        error: {
            code: 'missing-instance',
            message: 'tmDocumentEditorEngine.' + methodName + ': instance does not exist.',
            instanceId: instanceId || '',
        },
    };
}

// Generic helper for any "method-precondition-failed" case. Same shape, custom code.
export function errorResult(instanceId, methodName, code, message) {
    return {
        ok: false,
        error: {
            code: code || 'error',
            message: message || ('tmDocumentEditorEngine.' + methodName + ': error.'),
            instanceId: instanceId || '',
        },
    };
}
