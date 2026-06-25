import { createCanvasRunText, normalizeMarkType, orderedCanvasBlocks } from '../layout/canvas-text-style.mjs';
import { orderedSelection } from '../annotations/track-changes.mjs';

export function listBookmarks(model) {
    const items = [];
    for (const block of orderedCanvasBlocks(model)) {
        let offset = 0;
        for (const run of block?.content?.runs || []) {
            const text = createCanvasRunText(run);
            for (const mark of run?.marks || []) {
                if (normalizeMarkType(mark?.type) !== 'bookmark') {
                    continue;
                }

                const name = String(mark.value || mark.name || mark.preserve?.name || '');
                if (!name) {
                    continue;
                }

                items.push({
                    name,
                    blockId: block.id || '',
                    start: offset,
                    end: offset + text.length,
                });
            }

            offset += text.length;
        }
    }

    return items;
}

export function findBookmark(model, name) {
    const key = String(name || '');
    return listBookmarks(model).find(item => item.name === key) || null;
}

export function applyBookmarkToSelection(model, selection, name, options = {}) {
    const bookmarkName = String(name || options?.name || '').trim();
    const working = clone(model || {});
    ensureBodyBlocks(working);
    const range = explicitSelection(options) || orderedSelection(selection, working);
    if (!bookmarkName || !range?.anchor?.blockId || !range?.focus?.blockId) {
        return { changed: false, model: working, selection: range || null, operation: 'insertBookmark', bookmark: null, dirtyBlockIds: [] };
    }

    if (range.anchor.blockId !== range.focus.blockId) {
        return { changed: false, model: working, selection: range, operation: 'insertBookmark', bookmark: null, dirtyBlockIds: [] };
    }

    const block = findEditableBlock(working, range.anchor.blockId);
    if (!block) {
        return { changed: false, model: working, selection: range, operation: 'insertBookmark', bookmark: null, dirtyBlockIds: [] };
    }

    const start = clampOffset(block, range.anchor.offset);
    const end = clampOffset(block, Math.max(start, Number(range.focus.offset || start) || start));
    const insertText = String(options?.text ?? '');
    let changed = false;
    if (end > start) {
        block.content.runs = markRange(block, start, end, bookmarkName);
        changed = true;
    } else if (insertText.length > 0) {
        const split = splitRunsAtOffset(block, start);
        block.content.runs = compactRuns([
            ...split.before,
            {
                id: uniqueRunId(block, start),
                type: 'text',
                text: insertText,
                marks: [{ type: 'bookmark', value: bookmarkName }],
                preserve: {},
            },
            ...split.after,
        ], block.id);
        changed = true;
    }

    if (!changed) {
        return { changed: false, model: working, selection: range, operation: 'insertBookmark', bookmark: null, dirtyBlockIds: [] };
    }

    working.version = Number(working.version || 0) + 1;
    synchronizeSectionsWithBody(working);
    const bookmark = findBookmark(working, bookmarkName);
    return {
        changed: true,
        model: working,
        selection: {
            anchor: { blockId: block.id, offset: insertText.length > 0 && end === start ? start + insertText.length : end },
            focus: { blockId: block.id, offset: insertText.length > 0 && end === start ? start + insertText.length : end },
        },
        operation: 'insertBookmark',
        bookmark,
        dirtyBlockIds: [block.id],
    };
}

function explicitSelection(options = {}) {
    const blockId = String(options?.blockId || options?.targetBlockId || '');
    if (!blockId) {
        return null;
    }

    const start = Math.max(0, Number(options?.start ?? options?.startOffset ?? 0) || 0);
    const end = Math.max(start, Number(options?.end ?? options?.endOffset ?? start) || start);
    return {
        anchor: { blockId, offset: start },
        focus: { blockId, offset: end },
    };
}

function markRange(block, start, end, name) {
    const replacement = [];
    let cursor = 0;
    for (const run of runsOrEmpty(block)) {
        const text = createCanvasRunText(run);
        const runStart = cursor;
        const runEnd = cursor + text.length;
        cursor = runEnd;
        if (runEnd <= start || runStart >= end || String(run?.type || 'text') !== 'text') {
            replacement.push(removeBookmarkName(run, name));
            continue;
        }

        const localStart = Math.max(0, start - runStart);
        const localEnd = Math.min(text.length, end - runStart);
        if (localStart > 0) {
            replacement.push(removeBookmarkName(sliceRun(run, 0, localStart, `${name}-before`), name));
        }

        const marked = removeBookmarkName(sliceRun(run, localStart, localEnd, `${name}-bookmark`), name);
        marked.marks = Array.isArray(marked.marks) ? marked.marks : [];
        marked.marks.push({ type: 'bookmark', value: name });
        replacement.push(marked);

        if (localEnd < text.length) {
            replacement.push(removeBookmarkName(sliceRun(run, localEnd, text.length, `${name}-after`), name));
        }
    }

    return compactRuns(replacement, block.id);
}

function removeBookmarkName(run, name) {
    const copy = clone(run);
    copy.marks = (Array.isArray(copy.marks) ? copy.marks : [])
        .filter(mark => normalizeMarkType(mark?.type) !== 'bookmark' || String(mark?.value || mark?.name || mark?.preserve?.name || '') !== name);
    return copy;
}

function splitRunsAtOffset(block, offset) {
    const before = [];
    const after = [];
    let cursor = 0;
    const target = clampOffset(block, offset);
    for (const run of runsOrEmpty(block)) {
        const text = createCanvasRunText(run);
        const start = cursor;
        const end = cursor + text.length;
        cursor = end;
        if (end <= target) {
            before.push(clone(run));
            continue;
        }

        if (start >= target) {
            after.push(clone(run));
            continue;
        }

        if (String(run?.type || 'text') !== 'text') {
            after.push(clone(run));
            continue;
        }

        const local = Math.max(0, Math.min(String(run.text || '').length, target - start));
        if (local > 0) {
            before.push(sliceRun(run, 0, local, `before-${target}`));
        }

        if (local < String(run.text || '').length) {
            after.push(sliceRun(run, local, String(run.text || '').length, `after-${target}`));
        }
    }

    return { before, after };
}

function sliceRun(run, start, end, suffix) {
    const copy = clone(run);
    copy.id = `${run.id || 'run'}-${suffix}-${start}-${end}`;
    copy.text = String(run.text || '').slice(start, end);
    copy.marks = Array.isArray(run.marks) ? run.marks.map(clone) : [];
    return copy;
}

function compactRuns(runs, blockId) {
    const compacted = [];
    for (const run of runs.map(clone)) {
        if (String(run?.type || 'text') === 'text' && String(run.text || '').length === 0) {
            continue;
        }

        const previous = compacted[compacted.length - 1];
        if (previous
            && String(previous.type || 'text') === 'text'
            && String(run.type || 'text') === 'text'
            && JSON.stringify(previous.marks || []) === JSON.stringify(run.marks || [])
            && !previous.field
            && !run.field
            && !previous.token
            && !run.token) {
            previous.text = `${previous.text || ''}${run.text || ''}`;
            continue;
        }

        compacted.push(run);
    }

    return compacted.length > 0 ? compacted : [{ id: uniqueRunId({ id: blockId }, 0), type: 'text', text: '', marks: [] }];
}

function ensureBodyBlocks(model) {
    if (!model.body || typeof model.body !== 'object') {
        model.body = { blocks: [] };
    }

    if (!Array.isArray(model.body.blocks)) {
        model.body.blocks = [];
    }
}

function findEditableBlock(model, blockId) {
    return editableBlockEntries(model).find(entry => String(entry.block?.id || '') === String(blockId || ''))?.block || null;
}

function editableBlockEntries(model) {
    const entries = [];
    appendEditableEntries(model?.body?.blocks, entries);
    for (const headerFooter of Array.isArray(model?.headersFooters) ? model.headersFooters : []) {
        appendEditableEntries(headerFooter?.blocks, entries);
    }

    for (const note of Array.isArray(model?.notes) ? model.notes : []) {
        appendEditableEntries(note?.blocks, entries);
    }

    return entries;
}

function appendEditableEntries(blocks, entries) {
    if (!Array.isArray(blocks)) {
        return;
    }

    for (const block of blocks) {
        if (['paragraph', 'heading', 'list', 'quote'].includes(String(block?.type || block?.content?.type || '').toLowerCase())) {
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

function synchronizeSectionsWithBody(model) {
    if (!Array.isArray(model.sections) || model.sections.length === 0) {
        return;
    }

    const blocks = model.body.blocks;
    const assigned = new Set();
    for (const section of model.sections) {
        const sectionId = String(section?.id || '');
        const matching = blocks.filter(block => String(block.sectionId || '') === sectionId);
        section.blocks = matching;
        for (const block of matching) {
            assigned.add(block.id);
        }
    }

    const unassigned = blocks.filter(block => !assigned.has(block.id));
    if (unassigned.length > 0) {
        model.sections[0].blocks = [...(model.sections[0].blocks || []), ...unassigned];
    }
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

function clampOffset(block, offset) {
    const textLength = runsOrEmpty(block).map(run => createCanvasRunText(run)).join('').length;
    return Math.max(0, Math.min(textLength, Number(offset || 0) || 0));
}

function uniqueRunId(block, offset) {
    return `${block?.id || 'block'}-bookmark-${Math.max(0, Number(offset || 0) || 0)}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
