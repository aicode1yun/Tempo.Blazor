// TmCodeEditor — highlight-overlay plumbing. The component works without this script
// (it falls back to a plain monospace textarea); loading it enables the syntax-highlight
// overlay, Tab/Shift+Tab indentation and scroll sync. Prism is optional exactly like in
// the Notion code block: when window.Prism (or the requested grammar) is missing, the
// overlay renders escaped plain text.
window.tmCodeEditor = window.tmCodeEditor || (function () {
    'use strict';

    var states = new WeakMap();
    var INDENT = '  ';

    function escapeHtml(text) {
        return text
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function highlightHtml(text, language) {
        var grammar = language && window.Prism && window.Prism.languages && window.Prism.languages[language];
        if (grammar) {
            try {
                return window.Prism.highlight(text, grammar, language);
            } catch (e) {
                // fall through to escaped text
            }
        }
        return escapeHtml(text);
    }

    function render(state) {
        var text = state.textarea.value || '';
        // Trailing newline keeps the overlay's last line the same height as the textarea's.
        state.code.innerHTML = highlightHtml(text, state.language) + '\n';
    }

    function syncScroll(state) {
        state.pre.scrollTop = state.textarea.scrollTop;
        state.pre.scrollLeft = state.textarea.scrollLeft;
    }

    // Inserts text at the caret via execCommand when possible so the browser keeps
    // native undo history; setRangeText is the fallback for browsers without it.
    function insertText(textarea, text) {
        textarea.focus();
        var inserted = false;
        try {
            inserted = document.execCommand('insertText', false, text);
        } catch (e) { /* execCommand removed */ }
        if (!inserted) {
            textarea.setRangeText(text, textarea.selectionStart, textarea.selectionEnd, 'end');
            textarea.dispatchEvent(new Event('input', { bubbles: true }));
        }
    }

    function outdentSelection(textarea) {
        var value = textarea.value;
        var start = textarea.selectionStart;
        var end = textarea.selectionEnd;
        var lineStart = value.lastIndexOf('\n', start - 1) + 1;
        var selected = value.slice(lineStart, end);
        var outdented = selected.replace(/(^|\n)(  |\t)/g, '$1');
        if (outdented === selected) {
            return;
        }
        textarea.setSelectionRange(lineStart, end);
        insertText(textarea, outdented);
        textarea.setSelectionRange(lineStart, lineStart + outdented.length);
    }

    function handleKeydown(state, e) {
        if (e.key !== 'Tab' || state.textarea.disabled || state.textarea.readOnly) {
            return;
        }
        e.preventDefault();
        if (e.shiftKey) {
            outdentSelection(state.textarea);
        } else if (state.textarea.selectionStart !== state.textarea.selectionEnd
                   && state.textarea.value.slice(state.textarea.selectionStart, state.textarea.selectionEnd).indexOf('\n') >= 0) {
            // Multi-line selection → indent each selected line.
            var value = state.textarea.value;
            var start = state.textarea.selectionStart;
            var end = state.textarea.selectionEnd;
            var lineStart = value.lastIndexOf('\n', start - 1) + 1;
            var selected = value.slice(lineStart, end);
            var indented = INDENT + selected.replace(/\n/g, '\n' + INDENT);
            state.textarea.setSelectionRange(lineStart, end);
            insertText(state.textarea, indented);
            state.textarea.setSelectionRange(lineStart, lineStart + indented.length);
        } else {
            insertText(state.textarea, INDENT);
        }
        render(state);
    }

    return {
        init: function (body, textarea, code, language) {
            if (!body || !textarea || !code || states.has(textarea)) {
                return;
            }
            var state = {
                body: body,
                textarea: textarea,
                code: code,
                pre: code.parentElement,
                language: language || null,
                onInput: null,
                onScroll: null,
                onKeydown: null
            };
            state.onInput = function () { render(state); };
            state.onScroll = function () { syncScroll(state); };
            state.onKeydown = function (e) { handleKeydown(state, e); };

            textarea.addEventListener('input', state.onInput);
            textarea.addEventListener('scroll', state.onScroll);
            textarea.addEventListener('keydown', state.onKeydown);
            states.set(textarea, state);

            render(state);
            // The overlay class flips the textarea text transparent — only safe once JS runs.
            body.classList.add('tm-code-editor--highlighted');
        },

        setLanguage: function (textarea, language) {
            var state = states.get(textarea);
            if (!state) return;
            state.language = language || null;
            state.code.className = language ? 'language-' + language : '';
            render(state);
        },

        setValue: function (textarea, value) {
            var state = states.get(textarea);
            if (!state) return;
            state.textarea.value = value || '';
            render(state);
        },

        refresh: function (textarea) {
            var state = states.get(textarea);
            if (!state) return;
            render(state);
            syncScroll(state);
        },

        destroy: function (textarea) {
            var state = states.get(textarea);
            if (!state) return;
            textarea.removeEventListener('input', state.onInput);
            textarea.removeEventListener('scroll', state.onScroll);
            textarea.removeEventListener('keydown', state.onKeydown);
            states.delete(textarea);
        }
    };
})();
