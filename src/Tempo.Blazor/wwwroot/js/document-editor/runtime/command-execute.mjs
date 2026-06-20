// Phase D — runtime/command-execute.mjs
// `createCommandExecutor({ getRuntime })` → `{ execute(instanceId, command) }` —
//   thin command router used by the outer Blazor-bridge IIFE. Parses a flexible
//   command envelope (string | object with command/payload/selectionToken), forwards
//   it to `runtime.executeCommand`, and normalises the result into a stable
//   `{ ok, instanceId, command, ... }` shape. All parsing is pure; only the runtime
//   lookup is injected so the module is environment-agnostic.
//
// Pure standalone exports for use by tests or other callers:
//   `readCommandName(command)`, `readPayload(command)`,
//   `readSelectionToken(command)`, `normalizeResult(instanceId, name, result)`.

function cloneJson(value) {
    if (value === undefined || value === null) return value;
    try { return JSON.parse(JSON.stringify(value)); } catch { return value; }
}

export function readCommandName(command) {
    if (typeof command === 'string') return command;
    const body = command || {};
    return String(body.command || body.Command || body.commandName || body.CommandName
        || body.name || body.Name || body.id || body.Id || '');
}

export function readPayload(command) {
    if (!command || typeof command === 'string') return {};
    const body = command || {};
    return cloneJson(body.payload || body.Payload || {});
}

export function readSelectionToken(command) {
    if (!command || typeof command === 'string') return null;
    const body = command || {};
    const payload = body.payload || body.Payload || {};
    const selection = body.selection || body.Selection || payload.selection || payload.Selection || {};
    return body.selectionToken
        || body.SelectionToken
        || body.stableSelectionToken
        || body.StableSelectionToken
        || payload.selectionToken
        || payload.SelectionToken
        || payload.stableSelectionToken
        || payload.StableSelectionToken
        || selection.selectionToken
        || selection.SelectionToken
        || selection.stableSelectionToken
        || selection.StableSelectionToken
        || null;
}

export function normalizeResult(instanceId, commandName, result) {
    if (result && typeof result === 'object') {
        if (result.ok === false) return result;
        return Object.assign({ ok: true, instanceId, command: commandName }, result);
    }
    return {
        ok: result !== false && result !== undefined,
        instanceId,
        command: commandName,
        result,
    };
}

export function createCommandExecutor(options) {
    const opts = options || {};
    if (typeof opts.getRuntime !== 'function') {
        throw new TypeError('createCommandExecutor requires options.getRuntime (function)');
    }
    const { getRuntime } = opts;

    function execute(instanceId, command) {
        const commandName = readCommandName(command);
        if (!instanceId || !commandName) {
            return {
                ok: false,
                instanceId: instanceId || '',
                command: commandName || '',
                error: {
                    code: 'invalid-command-request',
                    reason: !instanceId ? 'missing-instance-id' : 'missing-command-name',
                },
            };
        }

        const payload = readPayload(command);
        const token = readSelectionToken(command);
        if (token) {
            payload.SelectionToken = token;
            payload.selectionToken = token;
        }
        if (command && typeof command === 'object'
            && (command.selection || command.Selection)
            && !payload.Selection && !payload.selection) {
            payload.Selection = cloneJson(command.Selection || command.selection);
        }

        const runtime = getRuntime();
        if (!runtime || typeof runtime.executeCommand !== 'function') {
            return {
                ok: false,
                instanceId,
                command: commandName,
                error: {
                    code: 'runtime-unavailable',
                    reason: 'runtime.executeCommand is unavailable',
                },
            };
        }

        try {
            return normalizeResult(instanceId, commandName, runtime.executeCommand(instanceId, commandName, payload));
        } catch (error) {
            return {
                ok: false,
                instanceId,
                command: commandName,
                error: {
                    code: 'command-exception',
                    reason: String((error && error.message) || error || 'command-exception'),
                },
            };
        }
    }

    return { execute };
}
