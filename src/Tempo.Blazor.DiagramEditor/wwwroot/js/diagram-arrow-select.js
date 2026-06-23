/**
 * TmDiagramArrowSelect — keyboard navigation support
 * Uses capture-phase keydown on document to intercept ArrowUp/ArrowDown/Enter/Escape
 * before they bubble to parent scroll containers.
 */
window.tmDiagramArrowSelect = {
    _handlers: new Map(), // menuId -> handler

    /**
     * Initialise keyboard capture for a menu.
     * @param {string} menuId  The id of the <div role="listbox">
     * @param {DotNetObjectReference} dotNetRef
     * @param {number} focusedIndex
     */
    init: function (menuId, dotNetRef, focusedIndex) {
        var self = this;

        // Clean up any previous handler for this id
        this.destroy(menuId);

        var menu = document.getElementById(menuId);
        if (!menu) {
            console.warn('TmDiagramArrowSelect.init: menu not found', menuId);
            return;
        }

        // Capture-phase handler on document — intercepts keys before they reach
        // the parent scroll container (e.g. properties panel).
        var handler = function (e) {
            // Only handle keys when the menu itself or one of its children has focus
            var target = document.activeElement;
            if (!menu.contains(target)) return;

            switch (e.key) {
                case 'ArrowDown':
                    e.preventDefault();
                    e.stopPropagation();
                    dotNetRef.invokeMethodAsync('OnArrowDown');
                    return;
                case 'ArrowUp':
                    e.preventDefault();
                    e.stopPropagation();
                    dotNetRef.invokeMethodAsync('OnArrowUp');
                    return;
                case 'Enter':
                    e.preventDefault();
                    e.stopPropagation();
                    dotNetRef.invokeMethodAsync('OnEnter');
                    return;
                case 'Escape':
                    e.preventDefault();
                    e.stopPropagation();
                    dotNetRef.invokeMethodAsync('OnEscape');
                    return;
            }
        };

        document.addEventListener('keydown', handler, true);
        self._handlers.set(menuId, handler);

        // Focus the first option (or the one at focusedIndex) after a short delay
        // so Blazor has finished rendering the menu buttons.
        setTimeout(function () {
            var buttons = menu.querySelectorAll('button[role="option"]');
            var idx = focusedIndex >= 0 && focusedIndex < buttons.length ? focusedIndex : 0;
            if (buttons[idx]) {
                buttons[idx].focus();
            }
        }, 50);
    },

    /**
     * Remove the capture-phase handler for a given menu.
     * @param {string} menuId
     */
    destroy: function (menuId) {
        var handler = this._handlers.get(menuId);
        if (handler) {
            document.removeEventListener('keydown', handler, true);
            this._handlers.delete(menuId);
        }
    },

    /**
     * Scroll a specific option into view.
     * @param {string} menuId
     * @param {number} index
     */
    scrollToOption: function (menuId, index) {
        var menu = document.getElementById(menuId);
        if (!menu) return;
        var buttons = menu.querySelectorAll('button[role="option"]');
        var btn = buttons[index];
        if (btn) {
            btn.focus();
            btn.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        }
    }
};
