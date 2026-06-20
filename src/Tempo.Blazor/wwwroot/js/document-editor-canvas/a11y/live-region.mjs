export function createCanvasLiveRegion(options = {}) {
    const doc = options.document;
    if (!doc || typeof doc.createElement !== 'function') {
        throw new Error('CanvasDocumentEngine live region requires a DOM-like document.');
    }

    const messages = options.messages || {};
    const root = doc.createElement('div');
    root.className = 'tm-document-canvas-live-region';
    root.setAttribute('data-testid', 'document-canvas-live-region');
    root.setAttribute('id', options.id || 'document-canvas-live-region');
    root.setAttribute('role', 'status');
    root.setAttribute('aria-live', 'polite');
    root.setAttribute('aria-atomic', 'true');
    if (options.ariaLabel) {
        root.setAttribute('aria-label', options.ariaLabel);
    }

    root.style.position = 'absolute';
    root.style.width = '1px';
    root.style.height = '1px';
    root.style.overflow = 'hidden';
    root.style.clipPath = 'inset(50%)';
    root.style.whiteSpace = 'nowrap';

    let revision = 0;
    let lastCaretKey = '';
    let lastSearchKey = '';

    function announce(message, kind = 'status', detail = {}) {
        const text = String(message || '').trim();
        if (!text) {
            return snapshot();
        }

        revision += 1;
        root.textContent = text;
        root.setAttribute('data-canvas-live-kind', kind);
        root.setAttribute('data-canvas-live-revision', String(revision));
        for (const name of ['mathId', 'slotName', 'offset', 'exit', 'blockId', 'activeIndex', 'count', 'query', 'commentId', 'revisionId']) {
            if (!Object.prototype.hasOwnProperty.call(detail || {}, name)) {
                root.removeAttribute?.(`data-canvas-live-${name}`);
            }
        }

        for (const [name, value] of Object.entries(detail || {})) {
            root.setAttribute(`data-canvas-live-${name}`, String(value ?? ''));
        }

        return snapshot();
    }

    function announceSelection(selection = {}) {
        const focus = selection.focus || selection.Focus || selection.anchor || selection.Anchor || {};
        const blockId = String(focus.blockId || focus.BlockId || '');
        const offset = Math.max(0, Number(focus.offset ?? focus.Offset ?? 0) || 0);
        const key = `${blockId}:${offset}`;
        if (!blockId || key === lastCaretKey) {
            return snapshot();
        }

        lastCaretKey = key;
        return announce(format(messages.caretAnnouncement, blockId, offset), 'caret', { blockId, offset });
    }

    function announceSearch(searchState = {}) {
        const matches = Array.isArray(searchState?.matches) ? searchState.matches : [];
        const count = Number(searchState?.matchCount ?? matches.length) || 0;
        const activeIndex = count > 0 ? Math.max(0, Number(searchState?.activeIndex || 0) || 0) : 0;
        const query = String(searchState?.query || '');
        const key = `${query}:${activeIndex}:${count}`;
        if (key === lastSearchKey) {
            return snapshot();
        }

        lastSearchKey = key;
        const message = count > 0
            ? format(messages.searchResultAnnouncement, activeIndex + 1, count)
            : format(messages.searchNoResultsAnnouncement, query);
        return announce(message, 'find', { activeIndex: activeIndex + 1, count, query });
    }

    function announceComment(commentId) {
        return announce(format(messages.commentAnnouncement, commentId || ''), 'comment', { commentId: commentId || '' });
    }

    function announceRevision(revisionId) {
        return announce(format(messages.revisionAnnouncement, revisionId || ''), 'revision', { revisionId: revisionId || '' });
    }

    function announceSaved() {
        return announce(messages.saveAnnouncement || '', 'save');
    }

    function announceMathSlot(slot = {}) {
        const slotName = String(slot?.slotName || slot?.SlotName || '').trim();
        const mathId = String(slot?.mathId || slot?.MathId || '').trim();
        const offset = Math.max(0, Number(slot?.offset ?? slot?.Offset ?? 0) || 0);
        const exiting = slot?.exit === true || slot?.Exit === true;
        const message = exiting
            ? format(messages.mathExitAnnouncement, slotName || 'equation')
            : format(messages.mathSlotAnnouncement, slotName, offset);
        return announce(message, 'math', { mathId, slotName, offset, exit: exiting });
    }

    function snapshot() {
        return {
            revision,
            message: root.textContent || '',
            kind: root.getAttribute?.('data-canvas-live-kind') || '',
        };
    }

    return {
        root,
        announce,
        announceSelection,
        announceSearch,
        announceComment,
        announceRevision,
        announceSaved,
        announceMathSlot,
        snapshot,
    };
}

function format(template, ...values) {
    return String(template || '').replace(/\{(\d+)\}/g, (_, index) => String(values[Number(index)] ?? ''));
}
