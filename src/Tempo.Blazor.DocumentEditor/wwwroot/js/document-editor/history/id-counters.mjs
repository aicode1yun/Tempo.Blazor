// Phase D — history/id-counters.mjs
// Factory for the operation/transaction id counters that the legacy IIFE keeps as
// module-private `_operationCounter` / `_transactionCounter` variables.
//
// Extracting the counters first lets future module migrations (createOperation,
// createTransaction, history stack) be pure functions that take a counter instance
// instead of reaching into closure state — which is the prerequisite for moving the
// operation builder out of the monolith.

export function createIdCounters(initial = {}) {
    let operation = Number(initial.operation || 0);
    let transaction = Number(initial.transaction || 0);
    let instance = Number(initial.instance || 0);

    return Object.freeze({
        nextOperationId() {
            operation += 1;
            return 'op-' + operation;
        },
        nextTransactionId() {
            transaction += 1;
            return 'tx-' + transaction;
        },
        nextInstanceId() {
            instance += 1;
            return instance;
        },
        snapshot() {
            return { operation, transaction, instance };
        },
        reset() {
            operation = 0;
            transaction = 0;
            instance = 0;
        },
    });
}
