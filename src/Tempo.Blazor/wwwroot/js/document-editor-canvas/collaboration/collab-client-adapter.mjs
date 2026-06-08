import { clone } from '../../document-editor/core/helpers.mjs';
import { createRelayCollabClient } from '../../document-editor/core-engine/collab-client.mjs';

export function createCanvasCollaborationClient(options = {}) {
    const send = typeof options.send === 'function' ? options.send : function () {};
    const applyRemoteOperation = typeof options.applyRemoteOperation === 'function'
        ? options.applyRemoteOperation
        : function () {};

    const relay = createRelayCollabClient({
        clientId: options.clientId,
        revision: options.revision,
        send: message => send({
            ...message,
            ops: message.ops.map(fromTransportOperation),
        }),
        applyRemote: operation => applyRemoteOperation(fromTransportOperation(operation)),
    });

    return Object.freeze({
        clientId: relay.clientId,
        localOperation(operation) {
            relay.localOperation(toTransportOperation(operation));
        },
        receiveServerChange(message = {}) {
            relay.receiveServerChange({
                ...message,
                ops: readCanvasOperations(message).map(toTransportOperation),
            });
        },
        getState() {
            const state = relay.getState();
            return {
                ...state,
                outstanding: state.outstanding
                    ? { ...state.outstanding, ops: state.outstanding.ops.map(fromTransportOperation) }
                    : null,
                buffer: state.buffer.map(fromTransportOperation),
            };
        },
    });
}

export function toTransportOperation(operation = {}) {
    const copy = clone(operation);
    const type = normalizeType(copy.type || copy.Type);
    if (type !== 'inserttext' && type !== 'deletetext') {
        return {
            type: 'canvasOperation',
            canvasOperation: copy,
        };
    }

    const target = copy.target || copy.Target || {};
    return {
        type: type === 'inserttext' ? 'insert' : 'delete',
        blockId: String(target.blockId || target.BlockId || ''),
        offset: Math.max(0, Number(target.offset ?? target.Offset ?? 0) || 0),
        text: String(copy.text ?? copy.Text ?? ''),
        canvasOperation: copy,
    };
}

export function fromTransportOperation(operation = {}) {
    if (operation.canvasOperation) {
        const copy = clone(operation.canvasOperation);
        const target = copy.target || copy.Target || {};
        if (operation.type === 'insert' || operation.type === 'delete') {
            copy.type = operation.type === 'insert' ? 'insertText' : 'deleteText';
            copy.target = {
                ...target,
                blockId: operation.blockId,
                offset: Math.max(0, Number(operation.offset || 0) || 0),
                length: String(operation.text ?? '').length,
            };
            copy.text = String(operation.text ?? '');
        }

        return copy;
    }

    return clone(operation);
}

function readCanvasOperations(message = {}) {
    if (Array.isArray(message.ops)) {
        return message.ops;
    }

    if (Array.isArray(message.batch?.operations)) {
        return message.batch.operations;
    }

    if (Array.isArray(message.Batch?.Operations)) {
        return message.Batch.Operations;
    }

    return [];
}

function normalizeType(value) {
    return String(value || '').replace(/[\s_-]/g, '').toLowerCase();
}
