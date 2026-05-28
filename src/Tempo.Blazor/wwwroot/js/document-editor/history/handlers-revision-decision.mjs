// Phase D — history/handlers-revision-decision.mjs
// `createRevisionDecisionHandler({createRevisionEngine, OperationTypes})` factory
// → `applyRevisionDecision(model, op, differ)` — dispatches AcceptRevision /
// RejectRevision operations to the revision engine. Emits a markerChange diff entry
// with the new status. Returns the engine's nextSelection (or null).

export function createRevisionDecisionHandler(options) {
    const opts = options || {};
    if (typeof opts.createRevisionEngine !== 'function') {
        throw new TypeError(
            'createRevisionDecisionHandler requires options.createRevisionEngine (function)');
    }
    if (!opts.OperationTypes || typeof opts.OperationTypes !== 'object') {
        throw new TypeError(
            'createRevisionDecisionHandler requires options.OperationTypes (object)');
    }
    const { createRevisionEngine, OperationTypes } = opts;

    return function applyRevisionDecision(model, op, differ) {
        const revisionId = op.revisionId || op.RevisionId;
        const engine = createRevisionEngine(model, {});
        const isAccept = op.type === OperationTypes.AcceptRevision;
        const decision = isAccept
            ? engine.acceptRevision(revisionId, op.selection || op.Selection || null)
            : engine.rejectRevision(revisionId, op.selection || op.Selection || null);
        differ.record({
            markerChange: {
                revisionId: revisionId,
                status: isAccept ? 'Accepted' : 'Rejected',
            },
            invalidatedLayoutScopes: ['document'],
            invalidatedOverlayScopes: ['revisions'],
        });
        return {
            ok: decision.ok !== false,
            invalidatedLayoutScopes: ['document'],
            nextSelection: decision.selection || null,
        };
    };
}
