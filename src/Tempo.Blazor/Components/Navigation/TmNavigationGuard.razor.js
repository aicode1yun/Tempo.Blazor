// Browser `beforeunload` guard for TmNavigationGuard. Tab-close/refresh cannot reliably await a
// server round-trip, so the dirty flag is cached client-side and the handler reads it synchronously
// — no .NET invocation happens from inside the unload handler itself.
let dirty = false;
let handler = null;

function onBeforeUnload(event) {
    if (!dirty) {
        return undefined;
    }

    event.preventDefault();
    event.returnValue = '';
    return '';
}

export function register(initialDirty) {
    dirty = !!initialDirty;
    if (handler) {
        window.removeEventListener('beforeunload', handler);
    }
    handler = onBeforeUnload;
    window.addEventListener('beforeunload', handler);
}

export function setDirty(value) {
    dirty = !!value;
}

export function dispose() {
    if (handler) {
        window.removeEventListener('beforeunload', handler);
        handler = null;
    }
    dirty = false;
}
