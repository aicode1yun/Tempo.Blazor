// Tempo.Blazor focus trap: keeps Tab focus within a container (e.g. TmModal) and
// restores focus to the previously-focused element on deactivate. Keyed by id so
// nested/stacked traps don't clobber each other's return target.
window.tmFocusTrap = (function () {
    const returnFocus = new Map();
    const FOCUSABLE = [
        'a[href]', 'button:not([disabled])', 'textarea:not([disabled])',
        'input:not([disabled]):not([type="hidden"])', 'select:not([disabled])',
        'audio[controls]', 'video[controls]', '[contenteditable]:not([contenteditable="false"])',
        '[tabindex]:not([tabindex="-1"])'
    ].join(',');

    function visibleFocusable(element) {
        return Array.from(element.querySelectorAll(FOCUSABLE))
            .filter(el => el.offsetParent !== null && !el.hasAttribute('disabled'));
    }

    return {
        activate: function (element, id) {
            if (!element) return;
            returnFocus.set(id, document.activeElement);

            const handler = function (e) {
                if (e.key !== 'Tab') return;
                const list = visibleFocusable(element);
                if (list.length === 0) { e.preventDefault(); element.focus(); return; }
                const first = list[0];
                const last = list[list.length - 1];
                const active = document.activeElement;
                if (e.shiftKey && (active === first || !element.contains(active))) {
                    e.preventDefault();
                    last.focus();
                } else if (!e.shiftKey && (active === last || !element.contains(active))) {
                    e.preventDefault();
                    first.focus();
                }
            };
            element._tmTrapHandler = handler;
            element.addEventListener('keydown', handler);

            const list = visibleFocusable(element);
            (list[0] || element).focus();
        },

        deactivate: function (element, id) {
            if (element && element._tmTrapHandler) {
                element.removeEventListener('keydown', element._tmTrapHandler);
                delete element._tmTrapHandler;
            }
            const target = returnFocus.get(id);
            returnFocus.delete(id);
            if (target && typeof target.focus === 'function') {
                try { target.focus(); } catch (e) { /* element gone */ }
            }
        }
    };
})();
