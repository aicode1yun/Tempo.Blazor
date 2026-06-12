// Tracks the last-focused text input/textarea so the variable picker can insert a token at the
// caret even after focus moves to the picker button.
let lastEditable = null;

document.addEventListener('focusin', (e) => {
    const t = e.target;
    if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA')) {
        lastEditable = t;
    }
});

export function insertToken(token) {
    const el = lastEditable;
    if (!el || !el.isConnected) {
        return false;
    }
    const start = el.selectionStart ?? el.value.length;
    const end = el.selectionEnd ?? el.value.length;
    el.value = el.value.slice(0, start) + token + el.value.slice(end);
    const caret = start + token.length;
    el.setSelectionRange(caret, caret);
    // Notify Blazor's binding so the model updates.
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.focus();
    return true;
}
