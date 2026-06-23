const FOCUSABLE_SELECTOR = [
    'a[href]',
    'area[href]',
    'button:not([disabled])',
    'input:not([disabled]):not([type="hidden"])',
    'select:not([disabled])',
    'textarea:not([disabled])',
    'iframe',
    'object',
    'embed',
    '[contenteditable="true"]',
    '[tabindex]:not([tabindex="-1"])'
].join(',');

const traps = new WeakMap();

export function trapFocus(root, initialFocus = null) {
    if (!isElement(root)) {
        return;
    }

    releaseFocusTrap(root, false);
    const document = root.ownerDocument;
    const state = {
        previousActive: isFocusable(document.activeElement) ? document.activeElement : null,
        onKeyDown: event => keepTabInside(root, event),
        onFocusIn: event => keepFocusInside(root, event)
    };

    root.addEventListener('keydown', state.onKeyDown, true);
    document.addEventListener('focusin', state.onFocusIn, true);
    root.setAttribute('data-focus-trap-active', 'true');
    traps.set(root, state);

    queueMicrotask(() => {
        if (!root.isConnected || traps.get(root) !== state) {
            return;
        }

        const target = isFocusable(initialFocus)
            ? initialFocus
            : firstFocusable(root);
        target?.focus?.({ preventScroll: true });
    });
}

export function releaseFocusTrap(root, restoreFocus = true) {
    const state = traps.get(root);
    if (!state) {
        return;
    }

    root.removeEventListener('keydown', state.onKeyDown, true);
    root.ownerDocument.removeEventListener('focusin', state.onFocusIn, true);
    root.removeAttribute('data-focus-trap-active');
    traps.delete(root);

    if (restoreFocus && isRestorable(state.previousActive)) {
        queueMicrotask(() => state.previousActive.focus?.({ preventScroll: true }));
    }
}

export function focusFirst(root) {
    firstFocusable(root)?.focus?.({ preventScroll: true });
}

function keepTabInside(root, event) {
    if (event.key !== 'Tab') {
        return;
    }

    const focusables = focusableElements(root);
    if (focusables.length === 0) {
        event.preventDefault();
        root.focus?.({ preventScroll: true });
        return;
    }

    const active = root.ownerDocument.activeElement;
    const currentIndex = focusables.indexOf(active);
    if (event.shiftKey && (currentIndex <= 0 || !root.contains(active))) {
        event.preventDefault();
        focusables[focusables.length - 1].focus({ preventScroll: true });
        return;
    }

    if (!event.shiftKey && currentIndex === focusables.length - 1) {
        event.preventDefault();
        focusables[0].focus({ preventScroll: true });
    }
}

function keepFocusInside(root, event) {
    if (!root.isConnected || root.contains(event.target)) {
        return;
    }

    queueMicrotask(() => {
        if (root.isConnected && !root.contains(root.ownerDocument.activeElement)) {
            firstFocusable(root)?.focus?.({ preventScroll: true });
        }
    });
}

function firstFocusable(root) {
    return focusableElements(root)[0] || (root.tabIndex >= 0 ? root : null);
}

function focusableElements(root) {
    if (!isElement(root)) {
        return [];
    }

    return Array.from(root.querySelectorAll(FOCUSABLE_SELECTOR))
        .filter(isFocusable);
}

function isFocusable(value) {
    if (!isElement(value)) {
        return false;
    }

    const style = value.ownerDocument.defaultView?.getComputedStyle(value);
    return !value.hasAttribute('disabled')
        && value.getAttribute('aria-hidden') !== 'true'
        && style?.visibility !== 'hidden'
        && style?.display !== 'none'
        && (value.offsetParent !== null || style?.position === 'fixed' || value === value.ownerDocument.activeElement);
}

function isRestorable(value) {
    return isElement(value)
        && value.isConnected
        && !value.hasAttribute('disabled')
        && value.getAttribute('aria-hidden') !== 'true';
}

function isElement(value) {
    return value instanceof Element;
}
