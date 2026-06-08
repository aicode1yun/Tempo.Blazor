export function createCanvasCommentOverlay(options = {}) {
    const doc = options.document || globalThis.document;
    if (!doc || typeof doc.createElement !== 'function') {
        throw new Error('Canvas comment overlay requires a DOM-like document.');
    }

    const root = doc.createElement('div');
    root.className = 'tm-document-canvas-comment-overlay';
    root.setAttribute('data-testid', 'document-canvas-comment-overlay');
    root.setAttribute('aria-hidden', 'true');
    Object.assign(root.style, {
        position: 'absolute',
        inset: '0',
        pointerEvents: 'none',
        zIndex: '34',
    });

    let canvasStack = null;
    let selectedCommentId = '';
    let markers = [];

    function mount(stack) {
        canvasStack = stack || null;
        const parent = canvasStack?.root || stack;
        if (parent?.appendChild && root.parentNode !== parent) {
            parent.appendChild(root);
        }

        return api;
    }

    function update(model, render, options = {}) {
        selectedCommentId = String(options.selectedCommentId || selectedCommentId || '');
        markers = buildCommentMarkers(model, render, { selectedCommentId })
            .map(marker => withPagePlacement(marker, canvasStack));
        root.replaceChildren(...markers.map(marker => createMarkerElement(doc, marker)));
        root.setAttribute('data-canvas-comment-marker-count', String(markers.length));
        root.setAttribute('data-canvas-comment-selected-id', selectedCommentId);
        return snapshot();
    }

    function select(commentId) {
        selectedCommentId = String(commentId || '');
        for (const child of Array.from(root.children || [])) {
            const active = child.getAttribute?.('data-comment-id') === selectedCommentId;
            child.className = active
                ? 'tm-document-canvas-comment-overlay__marker tm-document-canvas-comment-overlay__marker--selected'
                : 'tm-document-canvas-comment-overlay__marker';
            child.setAttribute?.('data-canvas-comment-selected', String(active));
        }

        root.setAttribute('data-canvas-comment-selected-id', selectedCommentId);
        return markerForComment(selectedCommentId);
    }

    function markerForComment(commentId) {
        const id = String(commentId || '');
        return markers.find(marker => marker.commentId === id) || null;
    }

    function destroy() {
        if (root.parentNode) {
            root.parentNode.removeChild(root);
        }

        markers = [];
        canvasStack = null;
    }

    function snapshot() {
        return {
            markerCount: markers.length,
            selectedCommentId,
            markers: markers.map(marker => ({ ...marker })),
        };
    }

    const api = {
        root,
        mount,
        update,
        select,
        markerForComment,
        snapshot,
        destroy,
    };

    return api;
}

export function buildCommentMarkers(model, render, options = {}) {
    const selectedCommentId = String(options.selectedCommentId || '');
    const comments = commentsOf(model);
    const commands = annotationCommands(render, 'commentAnchor');
    const markers = [];
    const usedComments = new Set();

    for (const command of commands) {
        const commentId = String(command.commentId || '');
        if (!commentId) {
            continue;
        }

        const comment = comments.find(item => item.id === commentId) || null;
        usedComments.add(commentId);
        markers.push(createMarkerFromRect({
            id: `${commentId}-${markers.length}`,
            commentId,
            blockId: String(command.blockId || comment?.anchor?.blockId || ''),
            startOffset: nullableNumber(command.start ?? comment?.anchor?.startOffset) ?? 0,
            endOffset: nullableNumber(command.end ?? comment?.anchor?.endOffset) ?? null,
            pageIndex: Number(command.pageIndex || 0) || 0,
            rect: rectFromCommand(command),
            status: commentStatus(comment),
            selected: commentId === selectedCommentId,
            source: 'inline-mark',
        }));
    }

    for (const comment of comments) {
        if (usedComments.has(comment.id)) {
            continue;
        }

        const range = rectForCommentAnchor(comment.anchor, render);
        if (!range) {
            continue;
        }

        markers.push(createMarkerFromRect({
            id: `${comment.id}-${markers.length}`,
            commentId: comment.id,
            blockId: String(comment.anchor?.blockId || ''),
            startOffset: comment.anchor?.startOffset ?? 0,
            endOffset: comment.anchor?.endOffset ?? null,
            pageIndex: range.pageIndex,
            rect: range.rect,
            status: commentStatus(comment),
            selected: comment.id === selectedCommentId,
            source: 'comment-anchor',
        }));
    }

    return mergeAdjacentMarkers(markers);
}

export function buildCommentRailItems(model, options = {}) {
    const selectedCommentId = String(options.selectedCommentId || '');
    return commentsOf(model)
        .sort((left, right) =>
            String(left.anchor?.blockId || '').localeCompare(String(right.anchor?.blockId || ''))
            || Number(left.anchor?.startOffset ?? 0) - Number(right.anchor?.startOffset ?? 0)
            || left.id.localeCompare(right.id))
        .map(comment => ({
            id: comment.id,
            anchor: { ...comment.anchor },
            status: commentStatus(comment),
            selected: comment.id === selectedCommentId,
            entryCount: entriesOf(comment).length,
            previewText: entriesOf(comment)[0]?.text || '',
        }));
}

export function addCommentToCanvasModel(model, selection, payload = {}) {
    const working = clone(model || {});
    ensureCommentCollections(working);
    const anchor = normalizeCommentAnchor(payload.anchor || payload.Anchor || selectionToAnchor(selection, working));
    if (!anchor.blockId) {
        return unchanged(working, selection, 'addComment');
    }

    const comment = normalizeCommentPayload(payload.comment || payload.Comment || payload, anchor);
    upsertComment(working, comment);
    const dirtyBlockIds = applyCommentAnchorMarkToModel(working, comment);
    working.version = Number(working.version || 0) + 1;
    return {
        changed: true,
        model: working,
        selection: selectionForComment(comment),
        operation: 'addComment',
        dirtyBlockIds,
        commentId: comment.id,
    };
}

export function deleteCommentFromCanvasModel(model, commentIdOrPayload) {
    const working = clone(model || {});
    ensureCommentCollections(working);
    const commentId = String(commentIdOrPayload?.commentId || commentIdOrPayload?.CommentId || commentIdOrPayload?.id || commentIdOrPayload || '').trim();
    if (!commentId || !working.comments.some(comment => String(comment?.id || '') === commentId)) {
        return unchanged(working, null, 'deleteComment');
    }

    working.comments = working.comments.filter(comment => String(comment?.id || '') !== commentId);
    const dirtyBlockIds = removeCommentAnchorMarksFromModel(working, commentId);
    working.version = Number(working.version || 0) + 1;
    return {
        changed: true,
        model: working,
        selection: null,
        operation: 'deleteComment',
        dirtyBlockIds,
        commentId,
    };
}

export function selectionForComment(comment) {
    const anchor = normalizeAnchor(comment?.anchor || comment?.Anchor);
    if (!anchor.blockId) {
        return null;
    }

    const offset = Math.max(0, Number(anchor.startOffset ?? anchor.endOffset ?? 0) || 0);
    return {
        anchor: { blockId: anchor.blockId, offset },
        focus: { blockId: anchor.blockId, offset },
    };
}

function createMarkerElement(doc, marker) {
    const element = doc.createElement('button');
    element.type = 'button';
    element.className = marker.selected
        ? 'tm-document-canvas-comment-overlay__marker tm-document-canvas-comment-overlay__marker--selected'
        : 'tm-document-canvas-comment-overlay__marker';
    element.setAttribute('data-testid', 'document-canvas-comment-marker');
    element.setAttribute('data-comment-id', marker.commentId);
    element.setAttribute('data-block-id', marker.blockId);
    element.setAttribute('data-page-index', String(marker.pageIndex));
    element.setAttribute('data-canvas-comment-status', marker.status);
    element.setAttribute('data-canvas-comment-selected', String(marker.selected));
    Object.assign(element.style, {
        position: 'absolute',
        left: `${marker.rect.x * marker.scale + marker.pageOffsetX}px`,
        top: `${marker.rect.y * marker.scale + marker.pageOffsetY}px`,
        width: `${marker.rect.width * marker.scale}px`,
        height: `${marker.rect.height * marker.scale}px`,
        pointerEvents: 'auto',
    });
    return element;
}

function withPagePlacement(marker, canvasStack) {
    const page = canvasStack?.pages?.get?.(String(marker.pageIndex));
    const pageElement = page?.pageElement || null;
    const scale = Math.max(0.01, Number(pageElement?.getAttribute?.('data-canvas-page-zoom-scale') || 1) || 1);
    return {
        ...marker,
        pageOffsetX: Number(pageElement?.offsetLeft || 0) || 0,
        pageOffsetY: Number(pageElement?.offsetTop || 0) || 0,
        scale,
    };
}

function commentsOf(model) {
    return (Array.isArray(model?.comments) ? model.comments : Array.isArray(model?.Comments) ? model.Comments : [])
        .map(comment => ({
            ...comment,
            id: String(comment?.id || comment?.Id || ''),
            anchor: normalizeAnchor(comment?.anchor || comment?.Anchor),
        }))
        .filter(comment => comment.id);
}

function normalizeAnchor(anchor = {}) {
    return {
        type: String(anchor.type || anchor.Type || ''),
        blockId: String(anchor.blockId || anchor.BlockId || ''),
        startOffset: nullableNumber(anchor.startOffset ?? anchor.StartOffset),
        endOffset: nullableNumber(anchor.endOffset ?? anchor.EndOffset),
    };
}

function rectForCommentAnchor(anchor, render) {
    const blockId = String(anchor?.blockId || '');
    if (!blockId) {
        return null;
    }

    const start = anchor.startOffset ?? 0;
    const end = Math.max(start + 1, anchor.endOffset ?? start + 1);
    const textCommands = annotationCommands(render, null)
        .filter(command => (command.type === 'textRun' || command.type === 'field' || command.type === 'formControl')
            && String(command.blockId || '') === blockId
            && Number(command.end ?? 0) > start
            && Number(command.start ?? 0) < end);
    if (textCommands.length === 0) {
        const paragraph = annotationCommands(render, null)
            .find(command => command.type === 'paragraphBox' && String(command.blockId || '') === blockId);
        return paragraph ? { pageIndex: Number(paragraph.pageIndex || 0) || 0, rect: rectFromCommand(paragraph) } : null;
    }

    return unionCommandRects(textCommands);
}

function annotationCommands(render, type) {
    const commands = Array.isArray(render?.displayList?.commands)
        ? render.displayList.commands
        : Array.isArray(render?.commands)
            ? render.commands
            : [];
    return type ? commands.filter(command => command?.type === type) : commands;
}

function unionCommandRects(commands) {
    const first = commands[0];
    const pageIndex = Number(first?.pageIndex || 0) || 0;
    const samePage = commands.filter(command => (Number(command.pageIndex || 0) || 0) === pageIndex);
    const minX = Math.min(...samePage.map(command => Number(command.x || 0) || 0));
    const minY = Math.min(...samePage.map(command => Number(command.y || 0) || 0));
    const maxX = Math.max(...samePage.map(command => (Number(command.x || 0) || 0) + Math.max(1, Number(command.width || 0) || 1)));
    const maxY = Math.max(...samePage.map(command => (Number(command.y || 0) || 0) + Math.max(1, Number(command.height || 0) || 1)));
    return {
        pageIndex,
        rect: { x: minX, y: minY, width: maxX - minX, height: maxY - minY },
    };
}

function rectFromCommand(command) {
    return {
        x: Number(command.x || 0) || 0,
        y: Number(command.y || 0) || 0,
        width: Math.max(2, Number(command.width || 0) || 2),
        height: Math.max(12, Number(command.height || 0) || 12),
    };
}

function createMarkerFromRect(marker) {
    return {
        ...marker,
        startOffset: Math.max(0, Number(marker.startOffset || 0) || 0),
        endOffset: marker.endOffset == null ? null : Math.max(0, Number(marker.endOffset || 0) || 0),
        rect: {
            x: marker.rect.x,
            y: marker.rect.y,
            width: Math.max(8, marker.rect.width),
            height: Math.max(14, marker.rect.height),
        },
    };
}

function mergeAdjacentMarkers(markers) {
    return markers
        .sort((left, right) =>
            left.pageIndex - right.pageIndex
            || left.rect.y - right.rect.y
            || left.rect.x - right.rect.x
            || left.commentId.localeCompare(right.commentId));
}

function commentStatus(comment) {
    return String(comment?.status || comment?.Status || 'open').toLowerCase() === 'resolved'
        ? 'resolved'
        : 'open';
}

function nullableNumber(value) {
    return value == null || Number.isNaN(Number(value)) ? null : Number(value);
}

function ensureCommentCollections(model) {
    model.body = model.body || { blocks: [] };
    model.body.blocks = Array.isArray(model.body.blocks) ? model.body.blocks : [];
    model.sections = Array.isArray(model.sections) ? model.sections : [];
    model.comments = Array.isArray(model.comments) ? model.comments : [];
}

function normalizeCommentPayload(input = {}, fallbackAnchor = {}) {
    const source = input || {};
    const id = String(source.id || source.Id || `comment-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`);
    const entries = entriesOf(source).map(entry => ({
        id: String(entry?.id || entry?.Id || `entry-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`),
        text: String(entry?.text ?? entry?.Text ?? ''),
        author: entry?.author || entry?.Author || null,
        createdAt: entry?.createdAt || entry?.CreatedAt || new Date().toISOString(),
        isExternalAuthor: entry?.isExternalAuthor === true || entry?.IsExternalAuthor === true,
    }));
    const text = String(source.text ?? source.Text ?? '').trim();
    if (entries.length === 0 && text) {
        entries.push({
            id: `entry-${id}`,
            text,
            author: source.author || source.Author || null,
            createdAt: new Date().toISOString(),
            isExternalAuthor: false,
        });
    }

    return {
        id,
        status: normalizeCommentStatus(source.status ?? source.Status),
        visibility: source.visibility ?? source.Visibility ?? 'Internal',
        anchor: normalizeCommentAnchor(source.anchor || source.Anchor || fallbackAnchor),
        entries,
    };
}

function normalizeCommentAnchor(anchor = {}) {
    const normalized = normalizeAnchor(anchor);
    const start = normalized.startOffset ?? 0;
    const end = normalized.endOffset ?? start;
    return {
        type: normalized.type || (end > start ? 'TextRange' : 'Block'),
        blockId: normalized.blockId,
        startOffset: Math.min(start, end),
        endOffset: Math.max(start, end),
    };
}

function normalizeCommentStatus(value) {
    return String(value || 'Open').toLowerCase() === 'resolved' ? 'Resolved' : 'Open';
}

function entriesOf(comment) {
    return Array.isArray(comment?.entries)
        ? comment.entries
        : (Array.isArray(comment?.Entries) ? comment.Entries : []);
}

function selectionToAnchor(selection, model) {
    const anchor = selection?.anchor || selection?.Anchor || null;
    const focus = selection?.focus || selection?.Focus || anchor;
    const blockId = String(anchor?.blockId || anchor?.BlockId || focus?.blockId || focus?.BlockId || '');
    const block = findBodyBlock(model, blockId);
    if (!block) {
        return {};
    }

    const start = Number(anchor?.offset ?? anchor?.Offset ?? 0) || 0;
    const end = Number(focus?.offset ?? focus?.Offset ?? start) || start;
    return {
        type: start === end ? 'Block' : 'TextRange',
        blockId,
        startOffset: Math.max(0, Math.min(start, end)),
        endOffset: Math.max(0, Math.max(start, end)),
    };
}

function upsertComment(model, comment) {
    const index = model.comments.findIndex(item => String(item?.id || '') === comment.id);
    if (index >= 0) {
        model.comments[index] = comment;
    } else {
        model.comments.push(comment);
    }
}

function applyCommentAnchorMarkToModel(model, comment) {
    const anchor = normalizeCommentAnchor(comment.anchor || comment.Anchor);
    if (anchor.type.toLowerCase() !== 'textrange' || !anchor.blockId || anchor.endOffset <= anchor.startOffset) {
        return [];
    }

    const block = findBodyBlock(model, anchor.blockId);
    if (!block) {
        return [];
    }

    applyCommentAnchorMarkToBlock(block, anchor.startOffset, anchor.endOffset, comment.id);
    syncSectionBlock(model, block);
    return [block.id].filter(Boolean);
}

function removeCommentAnchorMarksFromModel(model, commentId) {
    const dirty = [];
    for (const block of bodyBlocks(model)) {
        const runs = runsOrEmpty(block);
        let changed = false;
        for (const run of runs) {
            const marks = Array.isArray(run.marks) ? run.marks : [];
            const nextMarks = marks.filter(mark => String(mark?.commentAnchor?.commentId || mark?.CommentAnchor?.CommentId || '') !== commentId);
            if (nextMarks.length !== marks.length) {
                run.marks = nextMarks;
                changed = true;
            }
        }

        if (changed) {
            dirty.push(block.id);
            syncSectionBlock(model, block);
        }
    }

    return dirty.filter(Boolean);
}

function applyCommentAnchorMarkToBlock(block, startOffset, endOffset, commentId) {
    const runs = runsOrEmpty(block);
    const replacement = [];
    let cursor = 0;
    for (const run of runs) {
        const text = String(run?.text || '');
        const runStart = cursor;
        const runEnd = cursor + text.length;
        cursor = runEnd;
        if (runEnd <= startOffset || runStart >= endOffset || String(run?.type || 'text').toLowerCase() !== 'text') {
            replacement.push(clone(run));
            continue;
        }

        const localStart = Math.max(0, startOffset - runStart);
        const localEnd = Math.min(text.length, endOffset - runStart);
        addTextSlice(replacement, run, 0, localStart, null);
        addTextSlice(replacement, run, localStart, localEnd, commentId);
        addTextSlice(replacement, run, localEnd, text.length, null);
    }

    block.content.runs = compactRuns(replacement);
}

function addTextSlice(target, run, start, end, commentId) {
    if (end <= start) {
        return;
    }

    const copy = clone(run);
    copy.id = `${run.id || 'run'}-${start}-${end}-${commentId || 'plain'}`;
    copy.text = String(run.text || '').slice(start, end);
    copy.marks = Array.isArray(copy.marks)
        ? copy.marks.filter(mark => String(mark?.type || '').toLowerCase() !== 'commentanchor')
        : [];
    if (commentId) {
        copy.marks.push({
            type: 'commentAnchor',
            commentAnchor: { commentId, anchorId: commentId },
        });
    }

    target.push(copy);
}

function compactRuns(runs) {
    const compacted = [];
    for (const run of runs.filter(item => String(item?.text || '').length > 0)) {
        const previous = compacted[compacted.length - 1];
        if (previous
            && String(previous.type || 'text') === 'text'
            && String(run.type || 'text') === 'text'
            && JSON.stringify(previous.marks || []) === JSON.stringify(run.marks || [])) {
            previous.text = `${previous.text || ''}${run.text || ''}`;
            continue;
        }

        compacted.push(run);
    }

    return compacted;
}

function syncSectionBlock(model, block) {
    const sectionId = String(block?.sectionId || '');
    if (!sectionId) {
        return;
    }

    const section = (model.sections || []).find(item => String(item?.id || '') === sectionId);
    if (!section || !Array.isArray(section.blocks)) {
        return;
    }

    const index = section.blocks.findIndex(item => String(item?.id || '') === String(block.id || ''));
    if (index >= 0) {
        section.blocks[index] = clone(block);
    }
}

function findBodyBlock(model, blockId) {
    return bodyBlocks(model).find(block => String(block?.id || '') === String(blockId || '')) || null;
}

function bodyBlocks(model) {
    return Array.isArray(model?.body?.blocks) ? model.body.blocks : [];
}

function runsOrEmpty(block) {
    block.content = block.content || { type: block.type || 'paragraph', runs: [] };
    block.content.runs = Array.isArray(block.content.runs) ? block.content.runs : [];
    return block.content.runs;
}

function unchanged(model, selection, operation) {
    return { changed: false, model, selection, operation, dirtyBlockIds: [] };
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
