// TmQueryInput — minimal caret helpers. The component works without this script
// (it falls back to end-of-text tracking), but loading it enables true caret-aware
// autocomplete: reading/writing the input's selectionStart.
window.tmQueryInput = window.tmQueryInput || {
    getCaret: function (el) {
        if (el && typeof el.selectionStart === "number") {
            return el.selectionStart;
        }
        return 0;
    },
    setCaret: function (el, pos) {
        if (!el || typeof el.setSelectionRange !== "function") {
            return;
        }
        try {
            el.focus();
            el.setSelectionRange(pos, pos);
        } catch (e) {
            // setSelectionRange throws on some input types / detached nodes — ignore.
        }
    }
};
