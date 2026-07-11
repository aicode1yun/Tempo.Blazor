// Toolbar overflow measurement controller.
//
// TmDocumentEditorToolbar volá createOverflowController(ribbonGroupsElement, dotNetRef) po prvním
// renderu a disposeOverflowController(ribbonGroupsElement) při dispose. Controller měří, které
// [data-command] prvky ribbonu jsou mimo viditelnou oblast (ribbon má overflow-x: auto), a hlásí
// změny přes dotNetRef.invokeMethodAsync('SetOverflowingAsync', isOverflowing, commandNames) —
// tím se v toolbaru objeví tlačítko More s overflow menu jako doplněk ke scrollování.
//
// Kontrakt:
// - položka je „overflowed", když je viditelná méně než polovinou šířky (většinově viditelné,
//   jen okrajově oříznuté položky jsou stále dobře klikatelné → nehlásí se),
// - prvky uvnitř plovoucího UI (role=dialog/menu/listbox — galerie stylů, menu symbolů…) se
//   neměří: plovoucí vrstvy nejsou součástí ribbon osy,
// - .NET se volá jen při skutečné změně stavu (signature dedup — žádný chatty interop),
// - scroll/resize/mutation (přepnutí ribbon tabu) se coalescují přes requestAnimationFrame.

const VISIBLE_RATIO = 0.5;
const controllers = new WeakMap();

export function computeOverflowState(container) {
    if (!container || typeof container.querySelectorAll !== 'function') {
        return { isOverflowing: false, overflowedCommandNames: [] };
    }

    const containerRect = container.getBoundingClientRect();
    const overflowedCommandNames = [];
    const seen = new Set();
    for (const element of container.querySelectorAll('[data-command]')) {
        const command = element.getAttribute('data-command');
        if (!command || seen.has(command) || isInsideFloatingUi(element, container)) {
            continue;
        }

        const rect = element.getBoundingClientRect();
        const width = rect.right - rect.left;
        if (width <= 0) {
            continue;
        }

        const visibleWidth = Math.min(rect.right, containerRect.right) - Math.max(rect.left, containerRect.left);
        if (visibleWidth < width * VISIBLE_RATIO) {
            seen.add(command);
            overflowedCommandNames.push(command);
        }
    }

    return { isOverflowing: overflowedCommandNames.length > 0, overflowedCommandNames };
}

export function createOverflowController(container, dotNetRef) {
    if (!container || !dotNetRef) {
        return;
    }

    disposeOverflowController(container);
    const view = container.ownerDocument?.defaultView ?? globalThis;
    const state = {
        container,
        dotNetRef,
        view,
        lastSignature: null,
        scheduled: false,
        disposed: false,
        observers: [],
        removeScrollListener: null,
    };

    const schedule = () => scheduleMeasure(state);
    if (typeof view.ResizeObserver === 'function') {
        const resizeObserver = new view.ResizeObserver(schedule);
        resizeObserver.observe(container);
        state.observers.push(resizeObserver);
    }

    if (typeof view.MutationObserver === 'function') {
        // Přepnutí ribbon tabu vymění obsah panelu — ResizeObserver na kontejneru to nezachytí.
        const mutationObserver = new view.MutationObserver(schedule);
        mutationObserver.observe(container, { childList: true, subtree: true });
        state.observers.push(mutationObserver);
    }

    container.addEventListener('scroll', schedule, { passive: true });
    state.removeScrollListener = () => container.removeEventListener('scroll', schedule);

    controllers.set(container, state);
    scheduleMeasure(state);
}

export function disposeOverflowController(container) {
    const state = container ? controllers.get(container) : null;
    if (!state) {
        return;
    }

    state.disposed = true;
    for (const observer of state.observers) {
        observer.disconnect();
    }
    state.removeScrollListener?.();
    controllers.delete(container);
}

function scheduleMeasure(state) {
    if (state.disposed || state.scheduled) {
        return;
    }

    state.scheduled = true;
    const run = () => {
        state.scheduled = false;
        if (!state.disposed) {
            measureNow(state);
        }
    };

    if (typeof state.view.requestAnimationFrame === 'function') {
        state.view.requestAnimationFrame(run);
    } else {
        run();
    }
}

function measureNow(state) {
    const { isOverflowing, overflowedCommandNames } = computeOverflowState(state.container);
    const signature = `${isOverflowing}|${overflowedCommandNames.join(',')}`;
    if (signature === state.lastSignature) {
        return;
    }

    state.lastSignature = signature;
    state.dotNetRef
        .invokeMethodAsync('SetOverflowingAsync', isOverflowing, overflowedCommandNames)
        .catch(() => { /* Blazor circuit/komponenta už neexistuje — měření je best effort. */ });
}

function isInsideFloatingUi(element, container) {
    let node = element.parentElement ?? null;
    while (node && node !== container) {
        const role = typeof node.getAttribute === 'function' ? node.getAttribute('role') : null;
        if (role === 'dialog' || role === 'menu' || role === 'listbox') {
            return true;
        }
        node = node.parentElement ?? null;
    }

    return false;
}
