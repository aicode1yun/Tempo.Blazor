export function createCommandDispatcher() {
    const handlers = new Map();

    function register(commandId, handler) {
        const id = normalizeCommandId(commandId);
        if (!id) {
            throw new Error('Command id is required.');
        }
        if (typeof handler !== 'function') {
            throw new Error(`Command '${id}' must be registered with a function.`);
        }

        handlers.set(id, handler);
        return () => handlers.delete(id);
    }

    function execute(commandId, payload) {
        const id = normalizeCommandId(commandId);
        const handler = handlers.get(id);
        if (!handler) {
            return { handled: false, commandId: id };
        }

        return {
            handled: true,
            commandId: id,
            result: handler(payload),
        };
    }

    function listCommands() {
        return Array.from(handlers.keys()).sort();
    }

    return {
        register,
        execute,
        listCommands,
    };
}

function normalizeCommandId(commandId) {
    return String(commandId == null ? '' : commandId).trim().toLowerCase();
}
