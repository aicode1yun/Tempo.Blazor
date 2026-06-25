// Phase R.4.2 / R.4.4 — core-engine/input-surface.mjs
// Off-screen keyboard + IME capture for the new model-owned engine. A hidden,
// focusable element receives keystrokes and composition events; it is NOT the
// document — it's a capture buffer that stays empty between keystrokes. Each event is
// normalised into an editing intent and forwarded to the supplied handlers, which
// route into the model (see edit-model.mjs). This is the OnlyOffice `text_input`
// architecture (off-screen input + app-owned rendering), independently implemented.
//
// `createInputSurface({ doc?, handlers })` → surface:
//   - `element` — the hidden capture element.
//   - `mount(parent)` / `destroy()` — attach / detach + remove listeners.
//   - `focus()` / `blur()` / `isComposing()`.
//
// handlers (all optional):
//   insertText(text), insertParagraph(), insertLineBreak(),
//   deleteBackward(), deleteForward(),
//   caretMove({ key, shiftKey, ctrlKey, metaKey }),
//   compositionStart(), compositionUpdate(data), compositionEnd(data),
//   undo(), redo().
//
// Design: during IME composition the textarea is allowed to hold the composing text
// (events flow through); on `compositionend` the final string is emitted via
// `compositionEnd` and the buffer is cleared. Outside composition, `beforeinput` is
// intercepted (preventDefault) so the textarea never accumulates — the document is the
// single source of truth.

const CARET_KEYS = new Set([
    'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown',
    'Home', 'End', 'PageUp', 'PageDown',
]);

export function createInputSurface(options) {
    const opts = options || {};
    const doc = opts.doc || globalThis.document;
    const handlers = opts.handlers || {};

    const el = doc.createElement('textarea');
    el.className = 'tm-core-input-surface';
    el.setAttribute('autocomplete', 'off');
    el.setAttribute('autocorrect', 'off');
    el.setAttribute('autocapitalize', 'off');
    el.setAttribute('spellcheck', 'false');
    // R.4.7 — the off-screen capture IS the accessible text input: a focusable multiline
    // textbox (not aria-hidden). The rendered positioned-DOM carries the document content
    // semantics; this element conveys "you are editing text" + receives keyboard/IME.
    el.setAttribute('role', 'textbox');
    el.setAttribute('aria-multiline', 'true');
    el.setAttribute('aria-label', opts.ariaLabel || 'Document editor');
    el.setAttribute('tabindex', '0');
    // Off-screen but focusable + event-capable (NOT display:none / visibility:hidden,
    // which would stop it receiving keyboard + composition events).
    el.style.cssText = [
        'position:absolute', 'top:0', 'left:-9999px',
        'width:1px', 'height:1px', 'opacity:0', 'padding:0', 'margin:0',
        'border:0', 'outline:0', 'resize:none', 'overflow:hidden',
        'white-space:pre', 'z-index:-1',
    ].join(';');

    let composing = false;

    function call(name, arg) {
        const fn = handlers[name];
        return typeof fn === 'function' ? fn(arg) : undefined;
    }

    function clearBuffer() {
        try { el.value = ''; } catch { /* ignore */ }
    }

    // Text insertion + paste + IME-final flow through beforeinput. Control keys
    // (Enter / Backspace / Delete / arrows) are handled in keydown so they route
    // deterministically regardless of the empty capture buffer (a textarea fires
    // `insertLineBreak` for Enter and may not fire `deleteContentBackward` when empty).
    function onBeforeInput(e) {
        if (composing) return; // let IME flow through the textarea
        const inputType = e.inputType || '';
        switch (inputType) {
            case 'insertText':
            case 'insertReplacementText':
            case 'insertFromPaste':
            case 'insertFromDrop':
                if (e.data != null) { e.preventDefault(); call('insertText', String(e.data)); clearBuffer(); }
                return;
            default:
                return; // paragraph / line-break / deletes are handled in keydown
        }
    }

    // Fallback for environments / events where beforeinput.data is unavailable:
    // read the textarea on `input` and forward its content, then clear.
    function onInput(e) {
        if (composing) return;
        const inputType = e && e.inputType;
        // Composition-related input is owned by the compositionupdate/end path — never
        // re-forward it here (it can arrive with composing already cleared on `end`).
        if (inputType && /[Cc]omposition/.test(inputType)) { clearBuffer(); return; }
        if (inputType && /^insert/.test(inputType)) {
            // beforeinput already handled insertions (it ran preventDefault); buffer empty.
            clearBuffer();
            return;
        }
        const value = el.value;
        if (value) { call('insertText', value); clearBuffer(); }
    }

    function onKeyDown(e) {
        // IME guard: while composing — or on the IME "process" keydown that browsers fire
        // with keyCode 229 BEFORE `compositionstart` (and the Apple keyPress-before-start
        // quirk) — let composition own the key; never route it as a control key.
        if (composing || e.isComposing || e.keyCode === 229 || e.which === 229) return;
        const key = e.key;
        // Undo / redo (Ctrl/Cmd+Z, Ctrl/Cmd+Shift+Z, Ctrl/Cmd+Y).
        if ((e.ctrlKey || e.metaKey) && (key === 'z' || key === 'Z')) {
            e.preventDefault();
            if (e.shiftKey) call('redo'); else call('undo');
            clearBuffer();
            return;
        }
        if ((e.ctrlKey || e.metaKey) && (key === 'y' || key === 'Y')) {
            e.preventDefault();
            call('redo');
            clearBuffer();
            return;
        }
        // Ctrl/Cmd+Shift+V → arm a plain-text paste for the upcoming `paste` event (don't
        // preventDefault — we still want the native paste event to fire).
        if ((e.ctrlKey || e.metaKey) && e.shiftKey && (key === 'v' || key === 'V')) {
            plainPasteArmed = true;
            return;
        }
        if (key === 'Enter') {
            e.preventDefault();
            if (e.shiftKey) {
                if (typeof handlers.insertLineBreak === 'function') call('insertLineBreak');
                else call('insertText', '\n');
            } else {
                call('insertParagraph');
            }
            clearBuffer();
            return;
        }
        if (key === 'Tab' && !e.ctrlKey && !e.metaKey && !e.altKey) {
            // R.4.8 / R.5.9 — Tab navigates table cells, or nests/un-nests a list item. Only
            // swallow Tab when actually handled (otherwise leave default focus navigation intact).
            const handled = call('tabKey', { shiftKey: !!e.shiftKey });
            if (handled) { e.preventDefault(); clearBuffer(); return; }
        }
        if (key === 'Backspace') { e.preventDefault(); call('deleteBackward'); clearBuffer(); return; }
        if (key === 'Delete') { e.preventDefault(); call('deleteForward'); clearBuffer(); return; }
        if (CARET_KEYS.has(key)) {
            e.preventDefault();
            call('caretMove', { key, shiftKey: !!e.shiftKey, ctrlKey: !!e.ctrlKey, metaKey: !!e.metaKey });
        }
    }

    // R.5.2 — clipboard. The off-screen textarea is empty, so the native copy/cut of its
    // (empty) selection is useless: the handler serialises the MODEL selection onto the event's
    // clipboardData and we preventDefault to suppress the default. Paste reads the clipboard and
    // routes a parsed fragment into the model. Ctrl/Cmd+Shift+V arms a plain-text paste.
    let plainPasteArmed = false;
    function onCopy(e) { if (call('copy', e.clipboardData) !== false) { if (typeof e.preventDefault === 'function') e.preventDefault(); clearBuffer(); } }
    function onCut(e) { if (call('cut', e.clipboardData) !== false) { if (typeof e.preventDefault === 'function') e.preventDefault(); clearBuffer(); } }
    function onPaste(e) {
        if (composing) return;
        const plain = plainPasteArmed; plainPasteArmed = false;
        const handler = handlers.paste;
        const handled = typeof handler === 'function' ? handler(e.clipboardData, plain) : undefined;
        if (handled !== false) { if (typeof e.preventDefault === 'function') e.preventDefault(); clearBuffer(); }
    }

    function onCompositionStart() { composing = true; call('compositionStart'); }
    function onCompositionUpdate(e) { call('compositionUpdate', e && e.data != null ? String(e.data) : ''); }
    function onCompositionEnd(e) {
        composing = false;
        const data = e && e.data != null ? String(e.data) : '';
        call('compositionEnd', data);
        clearBuffer();
    }

    el.addEventListener('beforeinput', onBeforeInput);
    el.addEventListener('input', onInput);
    el.addEventListener('keydown', onKeyDown);
    el.addEventListener('copy', onCopy);
    el.addEventListener('cut', onCut);
    el.addEventListener('paste', onPaste);
    el.addEventListener('compositionstart', onCompositionStart);
    el.addEventListener('compositionupdate', onCompositionUpdate);
    el.addEventListener('compositionend', onCompositionEnd);

    function mount(parent) {
        if (parent && typeof parent.appendChild === 'function') parent.appendChild(el);
        return surface;
    }
    function focus() { try { el.focus({ preventScroll: true }); } catch { try { el.focus(); } catch { /* ignore */ } } }
    function blur() { try { el.blur(); } catch { /* ignore */ } }
    function destroy() {
        el.removeEventListener('beforeinput', onBeforeInput);
        el.removeEventListener('input', onInput);
        el.removeEventListener('keydown', onKeyDown);
        el.removeEventListener('copy', onCopy);
        el.removeEventListener('cut', onCut);
        el.removeEventListener('paste', onPaste);
        el.removeEventListener('compositionstart', onCompositionStart);
        el.removeEventListener('compositionupdate', onCompositionUpdate);
        el.removeEventListener('compositionend', onCompositionEnd);
        if (el.parentNode && typeof el.parentNode.removeChild === 'function') el.parentNode.removeChild(el);
    }

    const surface = {
        element: el,
        mount,
        focus,
        blur,
        destroy,
        isComposing: function () { return composing; },
    };
    return surface;
}
