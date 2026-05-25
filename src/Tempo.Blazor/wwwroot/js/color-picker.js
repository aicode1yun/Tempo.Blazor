window.tmColorPicker = window.tmColorPicker || (function () {
    const handlers = new WeakMap();

    function unregister(root) {
        const entry = root ? handlers.get(root) : null;
        if (!entry) {
            return;
        }

        document.removeEventListener("keydown", entry.keydownHandler, true);
        document.removeEventListener("pointerdown", entry.pointerdownHandler, true);
        handlers.delete(root);
    }

    function registerEscape(root, dotNetRef) {
        if (!root || !dotNetRef) {
            return;
        }

        unregister(root);

        const close = function (event, restoreFocus) {
            if (!document.body.contains(root)) {
                unregister(root);
                return;
            }

            if (!root.querySelector(".tm-color-picker-dropdown")) {
                return;
            }

            if (event) {
                event.preventDefault();
                event.stopPropagation();
                if (typeof event.stopImmediatePropagation === "function") {
                    event.stopImmediatePropagation();
                }
            }

            dotNetRef.invokeMethodAsync("CloseFromGlobalAsync", restoreFocus === true);
        };

        const keydownHandler = function (event) {
            if (!event || event.key !== "Escape") {
                return;
            }

            close(event, true);
        };

        const pointerdownHandler = function (event) {
            if (!event || root.contains(event.target)) {
                return;
            }

            close(null, false);
        };

        handlers.set(root, { keydownHandler: keydownHandler, pointerdownHandler: pointerdownHandler });
        document.addEventListener("keydown", keydownHandler, true);
        document.addEventListener("pointerdown", pointerdownHandler, true);
    }

    function focusPaletteSwatch(root, index) {
        if (!root) {
            return;
        }

        const swatch = root.querySelector(`.tm-color-palette-swatch[data-palette-index="${index}"]`);
        if (swatch && typeof swatch.focus === "function") {
            swatch.focus({ preventScroll: true });
        }
    }

    return {
        focusPaletteSwatch: focusPaletteSwatch,
        registerEscape: registerEscape,
        unregister: unregister
    };
})();
