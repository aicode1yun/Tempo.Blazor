const WORD_BOUNDARY_PATTERN = /[\s.,;:!?()[\]{}"']/u;

export function createTypingCoalescer(options = {}) {
    const windowMs = Math.max(0, Number(options.windowMs ?? 1200) || 0);
    const now = typeof options.now === 'function'
        ? options.now
        : () => globalThis.performance?.now?.() ?? Date.now();

    function buildMetadata(change = {}) {
        const edit = change.edit || {};
        const result = change.result || {};
        const beforeSelection = normalizeSelection(change.before?.selection);
        const afterSelection = normalizeSelection(change.after?.selection || change.selection || result.selection);
        const text = String(edit.text ?? '');
        const kind = classifyEdit(edit, result);
        const timestamp = Number(change.timestamp ?? now()) || 0;
        const groupOpen = kind === 'typing-insert' && !containsWordBoundary(text);

        return {
            kind,
            timestamp,
            blockId: beforeSelection?.focus?.blockId || afterSelection?.focus?.blockId || '',
            beforeOffset: Number(beforeSelection?.focus?.offset ?? 0) || 0,
            afterOffset: Number(afterSelection?.focus?.offset ?? 0) || 0,
            insertedText: text,
            groupOpen,
            coalescingWindowMs: windowMs,
        };
    }

    function canCoalesce(previous, next) {
        const previousTyping = previous?.typing;
        const nextTyping = next?.typing;
        if (!previousTyping || !nextTyping) {
            return false;
        }

        if (previousTyping.kind !== 'typing-insert' || nextTyping.kind !== 'typing-insert') {
            return false;
        }

        if (previousTyping.groupOpen !== true) {
            return false;
        }

        if (!previousTyping.blockId || previousTyping.blockId !== nextTyping.blockId) {
            return false;
        }

        if (previousTyping.afterOffset !== nextTyping.beforeOffset) {
            return false;
        }

        if (windowMs > 0 && nextTyping.timestamp - previousTyping.timestamp > windowMs) {
            return false;
        }

        return true;
    }

    function merge(previous, next) {
        const insertedText = `${previous.typing?.insertedText || ''}${next.typing?.insertedText || ''}`;
        return {
            ...previous,
            after: clone(next.after),
            dirtyBlockIds: unique([...(previous.dirtyBlockIds || []), ...(next.dirtyBlockIds || [])]),
            typing: {
                ...next.typing,
                insertedText,
                groupOpen: !containsWordBoundary(insertedText) && next.typing?.groupOpen === true,
            },
        };
    }

    function closeGroup() {
        return null;
    }

    return {
        buildMetadata,
        canCoalesce,
        merge,
        closeGroup,
    };
}

export function containsWordBoundary(text) {
    return WORD_BOUNDARY_PATTERN.test(String(text || ''));
}

function classifyEdit(edit, result) {
    if (result?.autoCorrect === true || result?.autoformat === true) {
        return result?.operation || 'autocorrect';
    }

    if (edit?.type === 'insertText' && typeof edit.text === 'string' && edit.text.length > 0) {
        return 'typing-insert';
    }

    return result?.operation || edit?.type || 'text-edit';
}

function normalizeSelection(selection) {
    if (!selection) {
        return null;
    }

    const anchor = selection.anchor || selection.focus || null;
    const focus = selection.focus || selection.anchor || null;
    if (!anchor || !focus) {
        return null;
    }

    return { anchor, focus };
}

function unique(values) {
    return [...new Set(values.map(value => String(value || '')).filter(Boolean))];
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
