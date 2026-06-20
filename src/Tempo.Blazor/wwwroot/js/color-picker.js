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

    function adjustDropdownPosition(root) {
        const dropdown = root ? root.querySelector(".tm-color-picker-dropdown") : null;
        if (!dropdown) {
            return;
        }

        dropdown.style.transform = "";
        const rect = dropdown.getBoundingClientRect();
        const padding = 8;
        let offsetX = 0;
        let offsetY = 0;
        if (rect.right > window.innerWidth - padding) {
            offsetX = window.innerWidth - padding - rect.right;
        }

        if (rect.left + offsetX < padding) {
            offsetX = padding - rect.left;
        }

        if (rect.bottom > window.innerHeight - padding) {
            offsetY = window.innerHeight - padding - rect.bottom;
        }

        if (rect.top + offsetY < padding) {
            offsetY = padding - rect.top;
        }

        if (Math.abs(offsetX) > 0.5 || Math.abs(offsetY) > 0.5) {
            dropdown.style.transform = `translate(${Math.round(offsetX)}px, ${Math.round(offsetY)}px)`;
        }
    }

    return {
        adjustDropdownPosition: adjustDropdownPosition,
        focusPaletteSwatch: focusPaletteSwatch,
        registerEscape: registerEscape,
        unregister: unregister
    };
})();
