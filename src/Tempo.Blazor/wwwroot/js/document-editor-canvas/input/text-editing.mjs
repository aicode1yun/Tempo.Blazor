import { nextGraphemeBoundary, prevGraphemeBoundary } from '../../document-editor/layout/grapheme.mjs';
import { canEditRestrictedSelection } from '../annotations/restricted-editing.mjs';
import { appendRevision, applyDeletionRevision, createRevision, revisionMark } from '../annotations/track-changes.mjs';
import { createCanvasRunText } from '../layout/canvas-text-style.mjs';

const EDITABLE_BLOCK_TYPES = new Set(['paragraph', 'heading', 'list', 'quote']);

export function applyCanvasTextEdit(model, selection, edit) {
    const working = cloneModel(model);
    ensureBodyBlocks(working);
    const normalizedSelection = normalizeSelection(selection, working);
    if (!normalizedSelection) {
        return { changed: false, model: working, selection: null, operation: null };
    }

    const protection = canEditRestrictedSelection(working, normalizedSelection);
    if (!protection.allowed) {
        return {
            changed: false,
            model: working,
            selection: normalizedSelection,
            operation: null,
            protected: true,
            readonlyReason: protection.reason,
        };
    }

    const type = String(edit?.type || '');
    let result;
    if (type === 'insertText') {
        result = insertText(working, normalizedSelection, String(edit?.text ?? ''), edit);
    } else if (type === 'insertLineBreak') {
        result = insertText(working, normalizedSelection, '\n', edit);
        result.operation = 'insertLineBreak';
    } else if (type === 'insertParagraph') {
        result = insertParagraph(working, normalizedSelection);
    } else if (type === 'deleteBackward') {
        result = deleteBackward(working, normalizedSelection, edit);
    } else if (type === 'deleteForward') {
        result = deleteForward(working, normalizedSelection, edit);
    } else if (type === 'replaceRange') {
        result = replaceRange(working, edit.range || normalizedSelection, String(edit?.text ?? ''), edit);
    } else {
        return { changed: false, model: working, selection: normalizedSelection, operation: null };
    }

    if (!result.changed) {
        return { ...result, model: working };
    }

    working.version = Number(working.version || 0) + 1;
    normalizeBlockOrder(working.body.blocks);
    synchronizeSectionsWithBody(working);
    return { ...result, model: working };
}

export function canvasBlockText(model, blockId) {
    const block = findEditableBlock(model, blockId);
    return block ? blockText(block) : '';
}

export function normalizeCanvasSelection(selection, model) {
    return normalizeSelection(selection, model);
}

function insertText(model, selection, text, edit = {}) {
    const value = String(text || '');
    if (!value) {
        return { changed: false, selection, operation: 'insertText' };
    }

    const collapsed = isCollapsedSelection(selection)
        ? selection
        : deleteSelection(model, selection).selection;
    const block = writableEditableBlock(model, collapsed.focus.blockId);
    if (!block) {
        return { changed: false, selection: collapsed, operation: 'insertText' };
    }

    const offset = clampOffset(block, collapsed.focus.offset);
    const insertionRevision = edit.trackChanges === true
        ? appendRevision(model, createRevision('insertion', block.id, { startOffset: offset, endOffset: offset + value.length }, edit))
        : null;
    const insertion = createTextRun(block, value, offset, edit);
    if (insertionRevision) {
        insertion.marks = (insertion.marks || []).filter(mark => String(mark?.type || '').toLowerCase() !== 'revision');
        insertion.marks.push(revisionMark(insertionRevision));
    }
    const split = splitRunsAtOffset(block, offset);
    block.content.runs = compactRuns([...split.before, insertion, ...split.after], block.id);
    const caret = { blockId: block.id, offset: offset + value.length };
    return {
        changed: true,
        selection: collapsedSelection(caret),
        operation: 'insertText',
        dirtyBlockIds: [block.id],
        revisionIds: insertionRevision ? [insertionRevision.id] : [],
    };
}

function replaceRange(model, range, text, edit = {}) {
    const deleted = deleteSelection(model, range);
    const target = deleted.selection || normalizeSelection(range, model);
    if (!target) {
        return { changed: false, selection: null, operation: 'replaceRange' };
    }

    const inserted = insertText(model, target, text, edit);
    if (!inserted.changed && !deleted.changed) {
        return { changed: false, selection: target, operation: 'replaceRange' };
    }

    return {
        changed: true,
        selection: inserted.selection || target,
        operation: 'replaceRange',
        dirtyBlockIds: unique([...(deleted.dirtyBlockIds || []), ...(inserted.dirtyBlockIds || [])]),
        removedBlockIds: deleted.removedBlockIds || [],
    };
}

function insertParagraph(model, selection) {
    const collapsed = isCollapsedSelection(selection)
        ? selection
        : deleteSelection(model, selection).selection;
    const entry = writableEditableBlockEntry(model, collapsed.focus.blockId);
    if (!entry) {
        return { changed: false, selection: collapsed, operation: 'insertParagraph' };
    }

    const block = entry.block;
    const offset = clampOffset(block, collapsed.focus.offset);
    const split = splitRunsAtOffset(block, offset);
    const newBlock = cloneBlockForSplit(block, model);
    block.content.runs = compactRuns(split.before, block.id);
    newBlock.content.runs = compactRuns(split.after, newBlock.id);
    entry.list.splice(entry.index + 1, 0, newBlock);
    const caret = { blockId: newBlock.id, offset: 0 };
    return {
        changed: true,
        selection: collapsedSelection(caret),
        operation: 'insertParagraph',
        dirtyBlockIds: [block.id, newBlock.id],
        insertedBlockId: newBlock.id,
    };
}

function deleteBackward(model, selection, edit = {}) {
    if (!isCollapsedSelection(selection)) {
        return edit.trackChanges === true
            ? { ...applyDeletionRevision(model, selection, edit), operation: 'deleteBackward' }
            : { ...deleteSelection(model, selection), operation: 'deleteBackward' };
    }

    let entry = findEditableBlockEntry(model, selection.focus.blockId);
    if (!entry) {
        return { changed: false, selection, operation: 'deleteBackward' };
    }

    let block = entry.block;
    const text = blockText(block);
    const offset = clampOffset(block, selection.focus.offset);
    if (offset > 0) {
        const start = prevGraphemeBoundary(text, offset);
        if (edit.trackChanges === true) {
            return {
                ...applyDeletionRevision(model, {
                    anchor: { blockId: block.id, offset: start },
                    focus: { blockId: block.id, offset },
                }, edit),
                operation: 'deleteBackward',
                deletedText: text.slice(start, offset),
            };
        }

        block = writableEditableBlock(model, block.id);
        deleteRangeWithinBlock(block, start, offset);
        const caret = { blockId: block.id, offset: start };
        return {
            changed: start !== offset,
            selection: collapsedSelection(caret),
            operation: 'deleteBackward',
            dirtyBlockIds: [block.id],
            deletedText: text.slice(start, offset),
        };
    }

    const previous = previousEditableBlock(model, entry.ordinal);
    if (!previous) {
        return { changed: false, selection, operation: 'deleteBackward' };
    }

    const writablePrevious = writableEditableBlockEntry(model, previous.block.id);
    entry = findEditableBlockEntry(model, block.id);
    block = entry?.block || block;
    const previousTextLength = blockText(writablePrevious.block).length;
    writablePrevious.block.content.runs = compactRuns([
        ...runsOrEmpty(writablePrevious.block),
        ...runsOrEmpty(block),
    ], writablePrevious.block.id);
    entry.list.splice(entry.index, 1);
    const caret = { blockId: writablePrevious.block.id, offset: previousTextLength };
    return {
        changed: true,
        selection: collapsedSelection(caret),
        operation: 'deleteBackward',
        dirtyBlockIds: [writablePrevious.block.id],
        removedBlockIds: [block.id],
        deletedText: '\n',
    };
}

function deleteForward(model, selection, edit = {}) {
    if (!isCollapsedSelection(selection)) {
        return edit.trackChanges === true
            ? { ...applyDeletionRevision(model, selection, edit), operation: 'deleteForward' }
            : { ...deleteSelection(model, selection), operation: 'deleteForward' };
    }

    let entry = findEditableBlockEntry(model, selection.focus.blockId);
    if (!entry) {
        return { changed: false, selection, operation: 'deleteForward' };
    }

    let block = entry.block;
    const text = blockText(block);
    const offset = clampOffset(block, selection.focus.offset);
    if (offset < text.length) {
        const end = nextGraphemeBoundary(text, offset);
        if (edit.trackChanges === true) {
            return {
                ...applyDeletionRevision(model, {
                    anchor: { blockId: block.id, offset },
                    focus: { blockId: block.id, offset: end },
                }, edit),
                operation: 'deleteForward',
                deletedText: text.slice(offset, end),
            };
        }

        block = writableEditableBlock(model, block.id);
        deleteRangeWithinBlock(block, offset, end);
        const caret = { blockId: block.id, offset };
        return {
            changed: end !== offset,
            selection: collapsedSelection(caret),
            operation: 'deleteForward',
            dirtyBlockIds: [block.id],
            deletedText: text.slice(offset, end),
        };
    }

    const next = nextEditableBlock(model, entry.ordinal);
    if (!next) {
        return { changed: false, selection, operation: 'deleteForward' };
    }

    block = writableEditableBlock(model, block.id);
    const nextEntry = findEditableBlockEntry(model, next.block.id);
    block.content.runs = compactRuns([
        ...runsOrEmpty(block),
        ...runsOrEmpty(next.block),
    ], block.id);
    nextEntry.list.splice(nextEntry.index, 1);
    const caret = { blockId: block.id, offset };
    return {
        changed: true,
        selection: collapsedSelection(caret),
        operation: 'deleteForward',
        dirtyBlockIds: [block.id],
        removedBlockIds: [next.block.id],
        deletedText: '\n',
    };
}

function deleteSelection(model, selection) {
    const range = orderedSelection(selection, model);
    if (!range || isCollapsedSelection(range)) {
        return { changed: false, selection: range, dirtyBlockIds: [] };
    }

    const entries = editableBlockEntries(model);
    const startIndex = entries.findIndex(entry => String(entry.block?.id || '') === String(range.anchor.blockId || ''));
    const endIndex = entries.findIndex(entry => String(entry.block?.id || '') === String(range.focus.blockId || ''));
    if (startIndex < 0 || endIndex < 0) {
        return { changed: false, selection: range, dirtyBlockIds: [] };
    }

    let startBlock = entries[startIndex].block;
    let endBlock = entries[endIndex].block;
    if (startIndex === endIndex) {
        startBlock = writableEditableBlock(model, startBlock.id);
        deleteRangeWithinBlock(startBlock, range.anchor.offset, range.focus.offset);
        return {
            changed: true,
            selection: collapsedSelection(range.anchor),
            dirtyBlockIds: [startBlock.id],
        };
    }

    startBlock = writableEditableBlock(model, startBlock.id);
    endBlock = startBlock.id === endBlock.id ? startBlock : writableEditableBlock(model, endBlock.id);
    const startSplit = splitRunsAtOffset(startBlock, range.anchor.offset);
    const endSplit = splitRunsAtOffset(endBlock, range.focus.offset);
    startBlock.content.runs = compactRuns([...startSplit.before, ...endSplit.after], startBlock.id);
    const removedBlocks = entries
        .slice(startIndex + 1, endIndex + 1)
        .map(entry => entry.block.id);
    for (const entry of entries.slice(startIndex + 1, endIndex + 1).reverse()) {
        entry.list.splice(entry.index, 1);
    }
    return {
        changed: true,
        selection: collapsedSelection(range.anchor),
        dirtyBlockIds: [startBlock.id],
        removedBlockIds: removedBlocks,
    };
}

function deleteRangeWithinBlock(block, start, end) {
    const safeStart = clampOffset(block, start);
    const safeEnd = clampOffset(block, Math.max(safeStart, Number(end || 0) || 0));
    const before = splitRunsAtOffset(block, safeStart).before;
    const after = splitRunsAtOffset(block, safeEnd).after;
    block.content.runs = compactRuns([...before, ...after], block.id);
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
            if (target - start >= text.length / 2) {
                before.push(clone(run));
            } else {
                after.push(clone(run));
            }
            continue;
        }

        const local = Math.max(0, Math.min(String(run.text || '').length, target - start));
        if (local > 0) {
            const left = clone(run);
            left.id = `${run.id || block.id}-before-${target}`;
            left.text = String(run.text || '').slice(0, local);
            before.push(left);
        }

        if (local < String(run.text || '').length) {
            const right = clone(run);
            right.id = `${run.id || block.id}-after-${target}`;
            right.text = String(run.text || '').slice(local);
            after.push(right);
        }
    }

    return { before, after };
}

function compactRuns(runs, blockId) {
    const compacted = [];
    for (const run of runs.map(clone)) {
        if (String(run?.type || 'text') === 'text' && String(run.text || '').length === 0) {
            continue;
        }

        const previous = compacted[compacted.length - 1];
        if (canMergeTextRuns(previous, run)) {
            previous.text = `${previous.text || ''}${run.text || ''}`;
            continue;
        }

        compacted.push(run);
    }

    if (compacted.length === 0) {
        compacted.push(createEmptyTextRun(blockId));
    }

    return compacted;
}

function canMergeTextRuns(left, right) {
    return !!left
        && !!right
        && String(left.type || 'text') === 'text'
        && String(right.type || 'text') === 'text'
        && JSON.stringify(left.marks || []) === JSON.stringify(right.marks || [])
        && !left.field
        && !right.field
        && !left.token
        && !right.token
        && !left.noteReference
        && !right.noteReference
        && !left.drawing
        && !right.drawing;
}

function createTextRun(block, text, offset, edit) {
    const template = styleRunForInsertion(block, offset);
    return {
        id: uniqueRunId(block, offset),
        type: 'text',
        text,
        marks: Array.isArray(edit?.marks) ? edit.marks.map(clone) : clone(template?.marks || []),
        field: null,
        token: null,
        noteReference: null,
        drawing: null,
        preserve: {},
    };
}

function styleRunForInsertion(block, offset) {
    const runs = runsOrEmpty(block);
    if (!runs.length) {
        return null;
    }

    let cursor = 0;
    for (const run of runs) {
        const text = createCanvasRunText(run);
        const next = cursor + text.length;
        if (offset <= next && String(run?.type || 'text') === 'text') {
            return run;
        }

        cursor = next;
    }

    return runs.find(run => String(run?.type || 'text') === 'text') || null;
}

function cloneBlockForSplit(block, model) {
    const copy = cloneBlockForWrite(block);
    copy.id = uniqueBlockId(model, block.id);
    copy.order = Number(block.order || 0) + 1;
    copy.content.type = copy.content.type || block.type || 'paragraph';
    return copy;
}

function cloneBlockForWrite(block) {
    const copy = { ...(block || {}) };
    copy.preserve = clone(block?.preserve || {});
    copy.content = clone(block?.content || {});
    return copy;
}

function normalizeSelection(selection, model) {
    const first = firstEditablePosition(model);
    if (!first) {
        return null;
    }

    const anchor = normalizePosition(selection?.anchor || selection?.focus || first, model) || first;
    const focus = normalizePosition(selection?.focus || selection?.anchor || anchor, model) || anchor;
    return { anchor, focus };
}

function normalizePosition(position, model) {
    const block = findEditableBlock(model, position?.blockId);
    if (!block) {
        return null;
    }

    return {
        blockId: block.id,
        offset: clampOffset(block, position?.offset),
    };
}

function orderedSelection(selection, model) {
    const normalized = normalizeSelection(selection, model);
    if (!normalized) {
        return null;
    }

    const anchorIndex = editableBlockIndex(model, normalized.anchor.blockId);
    const focusIndex = editableBlockIndex(model, normalized.focus.blockId);
    if (anchorIndex < focusIndex || (anchorIndex === focusIndex && normalized.anchor.offset <= normalized.focus.offset)) {
        return normalized;
    }

    return { anchor: normalized.focus, focus: normalized.anchor };
}

function isCollapsedSelection(selection) {
    return selection?.anchor?.blockId === selection?.focus?.blockId
        && Number(selection?.anchor?.offset || 0) === Number(selection?.focus?.offset || 0);
}

function collapsedSelection(position) {
    return { anchor: clonePosition(position), focus: clonePosition(position) };
}

function clonePosition(position) {
    return {
        blockId: String(position?.blockId || ''),
        offset: Math.max(0, Number(position?.offset || 0) || 0),
    };
}

function firstEditablePosition(model) {
    const block = editableBlockEntries(model)[0]?.block || null;
    return block ? { blockId: block.id, offset: 0 } : null;
}

function editableBlockIndex(model, blockId) {
    return editableBlockEntries(model).findIndex(entry => String(entry.block?.id || '') === String(blockId || ''));
}

function findEditableBlock(model, blockId) {
    return findEditableBlockEntry(model, blockId)?.block || null;
}

function writableEditableBlock(model, blockId) {
    return writableEditableBlockEntry(model, blockId)?.block || null;
}

function writableEditableBlockEntry(model, blockId) {
    const entry = findEditableBlockEntry(model, blockId);
    if (!entry) {
        return null;
    }

    const copy = cloneBlockForWrite(entry.block);
    entry.list[entry.index] = copy;
    return { ...entry, block: copy };
}

function previousEditableBlock(model, index) {
    return editableBlockEntries(model)[index - 1] || null;
}

function nextEditableBlock(model, index) {
    return editableBlockEntries(model)[index + 1] || null;
}

function isEditableBlock(block) {
    const type = String(block?.type || block?.content?.type || '').toLowerCase();
    return EDITABLE_BLOCK_TYPES.has(type);
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

function clampOffset(block, offset) {
    const length = blockText(block).length;
    return Math.max(0, Math.min(length, Number(offset || 0) || 0));
}

function createEmptyTextRun(blockId) {
    return {
        id: `${blockId || 'block'}-empty-run`,
        type: 'text',
        text: '',
        marks: [],
        field: null,
        token: null,
        noteReference: null,
        drawing: null,
        preserve: {},
    };
}

function uniqueRunId(block, offset) {
    const base = `${block.id || 'block'}-run-input-${Math.max(0, Number(offset || 0) || 0)}`;
    const existing = new Set(runsOrEmpty(block).map(run => String(run.id || '')));
    if (!existing.has(base)) {
        return base;
    }

    let index = 2;
    while (existing.has(`${base}-${index}`)) {
        index += 1;
    }

    return `${base}-${index}`;
}

function uniqueBlockId(model, sourceBlockId) {
    const base = `${sourceBlockId || 'block'}-split`;
    const existing = new Set(editableBlockEntries(model).map(entry => String(entry.block.id || '')));
    if (!existing.has(base)) {
        return base;
    }

    let index = 2;
    while (existing.has(`${base}-${index}`)) {
        index += 1;
    }

    return `${base}-${index}`;
}

function ensureBodyBlocks(model) {
    if (!model.body || typeof model.body !== 'object') {
        model.body = { blocks: [] };
    }

    if (!Array.isArray(model.body.blocks)) {
        model.body.blocks = [];
    }
}

function findEditableBlockEntry(model, blockId) {
    return editableBlockEntries(model).find(entry => String(entry.block?.id || '') === String(blockId || '')) || null;
}

function editableBlockEntries(model) {
    ensureBodyBlocks(model);
    const entries = [];
    appendEditableEntries(model.body.blocks, entries);
    for (const headerFooter of Array.isArray(model.headersFooters) ? model.headersFooters : []) {
        appendEditableEntries(headerFooter?.blocks, entries);
    }

    for (const note of Array.isArray(model.notes) ? model.notes : []) {
        appendEditableEntries(note?.blocks, entries);
    }

    entries.forEach((entry, ordinal) => {
        entry.ordinal = ordinal;
    });
    return entries;
}

function appendEditableEntries(blocks, entries) {
    if (!Array.isArray(blocks)) {
        return;
    }

    blocks.forEach((block, index) => {
        if (isEditableBlock(block)) {
            entries.push({ block, list: blocks, index, ordinal: entries.length });
        }

        const rows = block?.content?.table?.rows;
        if (!Array.isArray(rows)) {
            return;
        }

        for (const row of rows) {
            for (const cell of Array.isArray(row?.cells) ? row.cells : []) {
                appendEditableEntries(cell?.blocks, entries);
            }
        }
    });
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
        if (matching.length > 0) {
            section.blocks = matching;
            for (const block of matching) {
                assigned.add(block.id);
            }
        } else {
            section.blocks = [];
        }
    }

    const unassigned = blocks.filter(block => !assigned.has(block.id));
    if (unassigned.length > 0) {
        model.sections[0].blocks = [...(model.sections[0].blocks || []), ...unassigned];
    }
}

function normalizeBlockOrder(blocks) {
    blocks.forEach((block, index) => {
        block.order = (index + 1) * 10;
    });
}

function cloneModel(model) {
    const source = model || {};
    const copy = { ...source };
    copy.body = { ...(source.body || {}) };
    copy.body.blocks = Array.isArray(source.body?.blocks) ? source.body.blocks.slice() : [];
    copy.sections = Array.isArray(source.sections)
        ? source.sections.map(section => ({
            ...section,
            blocks: Array.isArray(section?.blocks) ? section.blocks.slice() : [],
        }))
        : source.sections;
    copy.headersFooters = Array.isArray(source.headersFooters)
        ? source.headersFooters.map(region => ({
            ...region,
            blocks: Array.isArray(region?.blocks) ? region.blocks.slice() : [],
        }))
        : source.headersFooters;
    copy.notes = Array.isArray(source.notes)
        ? source.notes.map(note => ({
            ...note,
            blocks: Array.isArray(note?.blocks) ? note.blocks.slice() : [],
        }))
        : source.notes;
    copy.revisions = Array.isArray(source.revisions) ? source.revisions.map(clone) : source.revisions;
    return copy;
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}

function unique(values) {
    return Array.from(new Set(values.filter(value => value != null).map(String)));
}
