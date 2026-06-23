// Phase D — history/transactions.mjs
// `createTransactionsModule({ idCounters, deps })` returns the transaction builder
// (state machine: apply / rollback / commit / toJSON). Mirrors the legacy IIFE
// `createTransaction` exactly but accepts its engine-side dependencies as injected
// parameters instead of reaching into closure state.
//
// Injected deps:
//   `applyOperation(model, operation, opts) → { ok, operation, invalidatedLayoutScopes, nextSelection }`
//   `replaceModelContents(model, snapshot)` — restore the model in place from a clone
//   `withStableSelectionToken(instanceId, selection, model) → selection` — bind a
//                                                                          selection
//                                                                          to a model
//                                                                          fingerprint
//   `createDocumentFingerprint(model) → string` — FNV-1a fingerprint
//   `createDiffer() → { snapshot() }` — render diff collector
//
// Plus `idCounters` from `./id-counters.mjs` for transaction-id generation.

import { asArray, asText, clone, sortObject, unique } from '../core/helpers.mjs';
import { TransactionTypes } from './operation-types.mjs';

export function createTransactionsModule(options) {
    const opts = options || {};
    const idCounters = opts.idCounters;
    const deps = opts.deps || {};

    if (!idCounters || typeof idCounters.nextTransactionId !== 'function') {
        throw new TypeError('createTransactionsModule requires options.idCounters with nextTransactionId()');
    }
    const required = ['applyOperation', 'replaceModelContents', 'withStableSelectionToken',
        'createDocumentFingerprint', 'createDiffer'];
    for (const key of required) {
        if (typeof deps[key] !== 'function') {
            throw new TypeError(`createTransactionsModule requires deps.${key} (function)`);
        }
    }

    const {
        applyOperation,
        replaceModelContents,
        withStableSelectionToken,
        createDocumentFingerprint,
        createDiffer,
    } = deps;

    function createTransaction(model, options) {
        const txOpts = options || {};
        const lightweightSnapshots = txOpts.lightweightSnapshots === true
            || txOpts.LightweightSnapshots === true;
        const snapshot = lightweightSnapshots ? null : clone(model);
        const instanceId = asText(txOpts.instanceId || txOpts.InstanceId
            || txOpts.documentInstanceId || txOpts.DocumentInstanceId || '');
        const beforeSelection = (txOpts.beforeSelection || txOpts.BeforeSelection)
            ? withStableSelectionToken(instanceId,
                txOpts.beforeSelection || txOpts.BeforeSelection, model)
            : null;
        const beforeDocFingerprint = lightweightSnapshots
            ? ''
            : (txOpts.beforeDocFingerprint || txOpts.BeforeDocFingerprint
                || createDocumentFingerprint(model));
        const commandName = txOpts.commandName || txOpts.CommandName
            || txOpts.label || txOpts.Label || txOpts.type || txOpts.Type || 'Document change';

        const transaction = {
            id: txOpts.id || idCounters.nextTransactionId(),
            type: txOpts.type || TransactionTypes.Default,
            label: txOpts.label || txOpts.type || 'Document change',
            commandName,
            instanceId,
            beforeModelSnapshot: snapshot,
            afterModelSnapshot: null,
            beforeDocFingerprint,
            afterDocFingerprint: null,
            beforeSelection: clone(beforeSelection),
            afterSelection: clone(beforeSelection),
            operations: [],
            invalidatedScopes: [],
            lightweightSnapshots,
            differ: createDiffer(),
            committed: false,
            rolledBack: false,
            renderSuppressed: true,

            rollback() {
                if (snapshot) replaceModelContents(model, snapshot);
                this.rolledBack = true;
                this.renderSuppressed = false;
                return { ok: true, transaction: this.toJSON() };
            },

            apply(operation) {
                const result = applyOperation(model, operation, {
                    differ: this.differ,
                    selection: this.afterSelection,
                });
                if (!result.ok) {
                    this.rollback();
                    return result;
                }
                this.operations.push(result.operation);
                this.invalidatedScopes = unique(this.invalidatedScopes
                    .concat(asArray(result.invalidatedLayoutScopes)));
                this.afterSelection = result.nextSelection
                    ? withStableSelectionToken(this.instanceId, result.nextSelection, model)
                    : clone(this.afterSelection);
                return result;
            },

            commit() {
                this.committed = true;
                this.renderSuppressed = false;
                if (!this.lightweightSnapshots) {
                    this.afterModelSnapshot = clone(model);
                    this.afterDocFingerprint = createDocumentFingerprint(model);
                } else {
                    this.afterDocFingerprint = '';
                }
                if (this.afterSelection) {
                    this.afterSelection = withStableSelectionToken(this.instanceId,
                        this.afterSelection, model);
                }
                return {
                    ok: true,
                    transaction: this.toJSON(),
                    order: ['differ', 'layout', 'render', 'selection-restore'],
                    differ: this.differ.snapshot(),
                };
            },

            toJSON() {
                return sortObject({
                    id: this.id,
                    type: this.type,
                    label: this.label,
                    commandName: this.commandName,
                    instanceId: this.instanceId,
                    beforeDocFingerprint: this.beforeDocFingerprint,
                    afterDocFingerprint: this.afterDocFingerprint,
                    beforeSelection: this.beforeSelection,
                    afterSelection: this.afterSelection,
                    invalidatedScopes: this.invalidatedScopes,
                    operationCount: this.operations.length,
                    lightweightSnapshots: this.lightweightSnapshots === true,
                    committed: this.committed,
                    rolledBack: this.rolledBack,
                    renderSuppressed: this.renderSuppressed,
                });
            },
        };

        return transaction;
    }

    return Object.freeze({ createTransaction });
}
