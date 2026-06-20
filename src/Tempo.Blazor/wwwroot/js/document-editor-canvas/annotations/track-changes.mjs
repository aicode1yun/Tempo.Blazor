import { createCanvasRunText } from '../layout/canvas-text-style.mjs';

export function createRevision(type, blockId, range = {}, options = {}) {
    const normalizedType = normalizeRevisionType(type);
    const id = String(options.revisionId || '') || stableId(`canvas-${normalizedType}`);
    return {
        id,
        type: titleCase(normalizedType),
        range: {
            blockId: String(blockId || ''),
            startOffset: Math.max(0, Number(range.startOffset ?? range.start ?? 0) || 0),
            endOffset: Math.max(0, Number(range.endOffset ?? range.end ?? range.startOffset ?? range.start ?? 0) || 0),
        },
        author: {
            id: String(options.author?.id || options.author?.Id || ''),
            displayName: String(options.author?.displayName || options.author?.DisplayName || ''),
        },
        createdAt: new Date(options.now || Date.now()).toISOString(),
        action: 'Pending',
        payloadJson: options.payloadJson || null,
    };
}

export function revisionMark(revision) {
    return {
        type: 'revision',
        revisionId: revision.id,
        value: String(revision.type || 'Insertion'),
    };
}

export function appendRevision(model, revision) {
    if (!Array.isArray(model.revisions)) {
        model.revisions = [];
    }

    model.revisions.push(revision);
    return revision;
}

export function addRevisionMarkToRuns(block, startOffset, endOffset, revision) {
    const runs = runsOrEmpty(block);
    const replacement = [];
    let cursor = 0;
    const start = Math.max(0, Number(startOffset || 0) || 0);
    const end = Math.max(start, Number(endOffset || start) || start);

    for (const run of runs) {
        const text = createCanvasRunText(run);
        const runStart = cursor;
        const runEnd = cursor + text.length;
        cursor = runEnd;
        if (runEnd <= start || runStart >= end || String(run?.type || 'text') !== 'text') {
            replacement.push(clone(run));
            continue;
        }

        const localStart = Math.max(0, start - runStart);
        const localEnd = Math.min(text.length, end - runStart);
        if (localStart > 0) {
            replacement.push(sliceRun(run, 0, localStart, `${revision.id}-before`));
        }

        const marked = sliceRun(run, localStart, localEnd, `${revision.id}-marked`);
        marked.marks = (marked.marks || []).filter(mark => normalizeMarkType(mark?.type) !== 'revision');
        marked.marks.push(revisionMark(revision));
        replacement.push(marked);

        if (localEnd < text.length) {
            replacement.push(sliceRun(run, localEnd, text.length, `${revision.id}-after`));
        }
    }

    block.content.runs = compactRuns(replacement);
}

export function applyDeletionRevision(model, selection, options = {}) {
    const range = orderedSelection(selection, model);
    if (!range) {
        return { changed: false, model, selection, dirtyBlockIds: [], revisions: [] };
    }

    const entries = editableBlockEntries(model);
    const startIndex = entries.findIndex(entry => entry.block.id === range.anchor.blockId);
    const endIndex = entries.findIndex(entry => entry.block.id === range.focus.blockId);
    if (startIndex < 0 || endIndex < 0) {
        return { changed: false, model, selection: range, dirtyBlockIds: [], revisions: [] };
    }

    const revisions = [];
    const dirtyBlockIds = [];
    for (let index = startIndex; index <= endIndex; index += 1) {
        const block = entries[index].block;
        const textLength = blockText(block).length;
        const startOffset = index === startIndex ? range.anchor.offset : 0;
        const endOffset = index === endIndex ? range.focus.offset : textLength;
        if (endOffset <= startOffset) {
            continue;
        }

        const revision = appendRevision(model, createRevision('deletion', block.id, { startOffset, endOffset }, options));
        addRevisionMarkToRuns(block, startOffset, endOffset, revision);
        revisions.push(revision);
        dirtyBlockIds.push(block.id);
    }

    return {
        changed: revisions.length > 0,
        model,
        selection: collapsedSelection(range.anchor),
        operation: 'trackDeletion',
        dirtyBlockIds,
        revisions,
    };
}

export function applyFormattingRevision(model, selection, markType, options = {}) {
    const range = orderedSelection(selection, model);
    if (!range || range.anchor.blockId !== range.focus.blockId || range.anchor.offset === range.focus.offset) {
        return { changed: false, model, dirtyBlockIds: [], revisions: [] };
    }

    const block = editableBlockEntries(model).find(entry => entry.block.id === range.anchor.blockId)?.block || null;
    if (!block) {
        return { changed: false, model, dirtyBlockIds: [], revisions: [] };
    }

    const revision = appendRevision(model, createRevision('formatting', block.id, {
        startOffset: range.anchor.offset,
        endOffset: range.focus.offset,
    }, {
        ...options,
        payloadJson: JSON.stringify({
            markType: titleCase(String(markType || 'bold')),
            newActive: true,
        }),
    }));
    addRevisionMarkToRuns(block, range.anchor.offset, range.focus.offset, revision);
    return {
        changed: true,
        model,
        dirtyBlockIds: [block.id],
        revisions: [revision],
    };
}

export function orderedSelection(selection, model) {
    const anchor = normalizePosition(selection?.anchor || selection?.Anchor, model);
    const focus = normalizePosition(selection?.focus || selection?.Focus || selection?.anchor || selection?.Anchor, model);
    if (!anchor || !focus) {
        return null;
    }

    const entries = editableBlockEntries(model);
    const anchorIndex = entries.findIndex(entry => entry.block.id === anchor.blockId);
    const focusIndex = entries.findIndex(entry => entry.block.id === focus.blockId);
    if (anchorIndex < 0 || focusIndex < 0) {
        return null;
    }

    if (anchorIndex < focusIndex || (anchorIndex === focusIndex && anchor.offset <= focus.offset)) {
        return { anchor, focus };
    }

    return { anchor: focus, focus: anchor };
}

function normalizePosition(position, model) {
    const blockId = String(position?.blockId || position?.BlockId || '');
    const block = editableBlockEntries(model).find(entry => entry.block.id === blockId)?.block || null;
    if (!block) {
        return null;
    }

    return {
        blockId,
        offset: Math.max(0, Math.min(blockText(block).length, Number(position?.offset ?? position?.Offset ?? 0) || 0)),
    };
}

function editableBlockEntries(model) {
    const entries = [];
    appendEditableEntries(model?.body?.blocks, entries);
    return entries;
}

function appendEditableEntries(blocks, entries) {
    if (!Array.isArray(blocks)) {
        return;
    }

    for (const block of blocks) {
        if (isEditableBlock(block)) {
            entries.push({ block });
        }

        const rows = block?.content?.table?.rows;
        if (!Array.isArray(rows)) {
            continue;
        }

        for (const row of rows) {
            for (const cell of Array.isArray(row?.cells) ? row.cells : []) {
                appendEditableEntries(cell?.blocks, entries);
            }
        }
    }
}

function isEditableBlock(block) {
    return ['paragraph', 'heading', 'list', 'quote'].includes(String(block?.type || block?.content?.type || '').toLowerCase());
}

function blockText(block) {
    return runsOrEmpty(block).map(run => createCanvasRunText(run)).join('');
}

function runsOrEmpty(block) {
    if (!block.content || typeof block.content !== 'object') {
        block.content = { type: block.type || 'paragraph', runs: [] };
    }

    if (!Array.isArray(block.content.runs)) {
        block.content.runs = [];
    }

    return block.content.runs;
}

function sliceRun(run, start, end, suffix) {
    const copy = clone(run);
    copy.id = `${run.id || 'run'}-${suffix}-${start}-${end}`;
    copy.text = String(run.text || '').slice(start, end);
    copy.marks = Array.isArray(run.marks) ? run.marks.map(clone) : [];
    return copy;
}

function compactRuns(runs) {
    const compacted = [];
    for (const run of runs.filter(item => String(item?.text || '').length > 0).map(clone)) {
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

    return compacted.length > 0 ? compacted : [{ id: stableId('empty'), type: 'text', text: '', marks: [] }];
}

function collapsedSelection(position) {
    return { anchor: { ...position }, focus: { ...position } };
}

function normalizeRevisionType(type) {
    const normalized = String(type || '').toLowerCase();
    if (normalized === 'deletion' || normalized === 'delete') {
        return 'deletion';
    }

    if (normalized === 'formatting' || normalized === 'format') {
        return 'formatting';
    }

    return 'insertion';
}

function normalizeMarkType(type) {
    return String(type || '').replace(/[\s_-]/g, '').toLowerCase();
}

function titleCase(value) {
    const text = String(value || '');
    return text ? text.charAt(0).toUpperCase() + text.slice(1) : text;
}

function stableId(prefix) {
    return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
