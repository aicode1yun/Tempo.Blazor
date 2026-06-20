import { createCanvasRunText, normalizeMarkType } from '../layout/canvas-text-style.mjs';
import {
    ADVANCED_INLINE_MARK_TYPES,
    ADVANCED_TOGGLE_MARK_COMMANDS,
    ADVANCED_VALUE_MARK_COMMANDS,
    changeCharacterCase,
    createAdvancedCharacterMark,
    nextFontSizeStep,
    normalizeChangeCaseVariant,
} from './advanced-char.mjs';

const TOGGLE_MARK_COMMANDS = new Map([
    ['bold', 'bold'],
    ['italic', 'italic'],
    ['underline', 'underline'],
    ['strikethrough', 'strikethrough'],
    ['strike', 'strikethrough'],
    ...ADVANCED_TOGGLE_MARK_COMMANDS,
]);

const VALUE_MARK_COMMANDS = new Map([
    ['fontfamily', 'fontFamily'],
    ['fontsize', 'fontSize'],
    ['textcolor', 'textColor'],
    ['highlight', 'highlight'],
    ['link', 'link'],
    ...ADVANCED_VALUE_MARK_COMMANDS,
]);

const INLINE_MARK_TYPES = new Set([
    'bold',
    'italic',
    'underline',
    'strikethrough',
    'fontfamily',
    'fontsize',
    'textcolor',
    'highlight',
    'link',
    ...ADVANCED_INLINE_MARK_TYPES,
]);

export function createInlineFormatState(initial = {}) {
    const source = initial && typeof initial === 'object' ? initial : {};
    return {
        pendingMarks: Array.isArray(source.pendingMarks) ? source.pendingMarks.map(cloneMark) : [],
    };
}

export function applyInlineFormatCommand(model, selection, commandId, argument, state = createInlineFormatState()) {
    const command = normalizeCommandId(commandId);
    const working = clone(model || {});
    ensureBodyBlocks(working);
    const normalizedSelection = normalizeSelection(selection, working);
    if (!normalizedSelection) {
        return { changed: false, model: working, selection: null, state, formattingState: queryInlineFormattingState(working, selection, state) };
    }

    const nextState = createInlineFormatState(state);
    let result;
    if (TOGGLE_MARK_COMMANDS.has(command)) {
        result = applyToggleMark(working, normalizedSelection, TOGGLE_MARK_COMMANDS.get(command), nextState);
    } else if (command === 'togglekerning') {
        result = applyToggleKerning(working, normalizedSelection, nextState);
    } else if (VALUE_MARK_COMMANDS.has(command)) {
        result = applyValueMark(working, normalizedSelection, VALUE_MARK_COMMANDS.get(command), argument, nextState);
    } else if (command === 'clearformatting' || command === 'clearcharacterformatting') {
        result = applyClearFormatting(working, normalizedSelection, nextState);
    } else if (command === 'removelink') {
        result = applyRemoveMark(working, normalizedSelection, 'link', nextState);
    } else if (command === 'changecase') {
        result = applyChangeCase(working, normalizedSelection, argument);
    } else if (command === 'increasefontsize' || command === 'decreasefontsize') {
        result = applyFontSizeStep(working, normalizedSelection, command === 'decreasefontsize' ? -1 : 1, nextState);
    } else {
        result = { changed: false, dirtyBlockIds: [] };
    }

    if (result.changed) {
        working.version = Number(working.version || 0) + 1;
        synchronizeSectionsWithBody(working);
    }

    return {
        changed: result.changed,
        model: working,
        selection: normalizedSelection,
        state: nextState,
        operation: command,
        dirtyBlockIds: result.dirtyBlockIds || [],
        formattingState: queryInlineFormattingState(working, normalizedSelection, nextState),
    };
}

export function queryInlineFormattingState(model, selection, state = createInlineFormatState()) {
    ensureBodyBlocks(model);
    const normalizedSelection = normalizeSelection(selection, model);
    const pending = Array.isArray(state.pendingMarks) ? state.pendingMarks : [];
    const markTypes = [
        'bold',
        'italic',
        'underline',
        'strikethrough',
        'superscript',
        'subscript',
        'smallCaps',
        'allCaps',
        'doubleStrikethrough',
        'fontFamily',
        'fontSize',
        'textColor',
        'highlight',
        'link',
        'characterSpacing',
        'characterScale',
        'kerning',
    ];
    const result = {
        disabled: !normalizedSelection,
        isCollapsed: isCollapsedSelection(normalizedSelection),
        pendingMarks: pending.map(cloneMark),
        commands: {},
    };

    for (const type of markTypes) {
        const commandId = markTypeToCommand(type);
        const normalizedType = normalizeMarkType(type);
        const values = collectSelectedMarkValues(model, normalizedSelection, normalizedType, pending);
        result.commands[commandId] = formatCommandState(values, normalizedType, result.isCollapsed);
    }

    return result;
}

// Merges tri-state pending overrides onto a template (the inherited run marks at the caret). An add-override
// sets/replaces the mark for its type; a remove-override ({ type, remove: true }) deletes the inherited mark,
// which is how "turn bold OFF at the caret" suppresses an inherited bold. Other inherited marks are preserved,
// so a pending colour does not wipe an inherited bold.
export function mergeMarkOverrides(templateMarks = [], overrides = []) {
    const byType = new Map();
    for (const mark of Array.isArray(templateMarks) ? templateMarks : []) {
        byType.set(normalizeMarkType(mark?.type), cloneMark(mark));
    }

    for (const override of Array.isArray(overrides) ? overrides : []) {
        const type = normalizeMarkType(override?.type);
        if (!type) {
            continue;
        }

        if (override?.remove === true) {
            byType.delete(type);
        } else {
            byType.set(type, cloneMark(override));
        }
    }

    return Array.from(byType.values());
}

export function marksForInsertion(state = createInlineFormatState(), templateMarks = []) {
    return mergeMarkOverrides(templateMarks, state.pendingMarks || []);
}

export function linkAtPosition(model, position) {
    const block = findEditableBlock(model, position?.blockId);
    if (!block) {
        return null;
    }

    const offset = clampOffset(block, position?.offset);
    let cursor = 0;
    for (const run of runsOrEmpty(block)) {
        const text = createCanvasRunText(run);
        const start = cursor;
        const end = cursor + text.length;
        cursor = end;
        if (offset < start || offset > end || (offset === end && end !== start && offset !== 0)) {
            continue;
        }

        const link = (run.marks || []).find(mark => normalizeMarkType(mark?.type) === 'link');
        const href = link?.link?.href || link?.href || link?.value;
        return href ? { href: String(href), blockId: block.id, start, end } : null;
    }

    return null;
}

function applyToggleMark(model, selection, markType, state) {
    if (isCollapsedSelection(selection)) {
        togglePendingMark(state, { type: markType }, inheritedMarksAtCaret(model, selection));
        return { changed: false, dirtyBlockIds: [] };
    }

    const normalizedType = normalizeMarkType(markType);
    const shouldRemove = selectionRuns(model, selection)
        .filter(item => item.text.length > 0)
        .every(item => hasMark(item.run, normalizedType));
    return applyMarkToSelection(model, selection, markType, shouldRemove ? null : { type: markType });
}

function applyValueMark(model, selection, markType, argument, state) {
    const mark = createValueMark(markType, argument);
    if (!mark) {
        return applyRemoveMark(model, selection, markType, state);
    }

    if (isCollapsedSelection(selection)) {
        setPendingMark(state, mark);
        return { changed: false, dirtyBlockIds: [] };
    }

    return applyMarkToSelection(model, selection, markType, mark);
}

function applyFontSizeStep(model, selection, direction, state) {
    const values = collectSelectedMarkValues(model, selection, 'fontsize', state.pendingMarks || []);
    const current = values.values.find(value => value != null) || null;
    const next = `${nextFontSizeStep(current, direction)}pt`;
    return applyValueMark(model, selection, 'fontSize', next, state);
}

function applyRemoveMark(model, selection, markType, state) {
    if (isCollapsedSelection(selection)) {
        const inheritedOn = inheritedMarksAtCaret(model, selection)
            .some(mark => normalizeMarkType(mark?.type) === normalizeMarkType(markType));
        if (inheritedOn) {
            setRemovePendingMark(state, markType);
        } else {
            removePendingMark(state, markType);
        }

        return { changed: false, dirtyBlockIds: [] };
    }

    return applyMarkToSelection(model, selection, markType, null);
}

function applyToggleKerning(model, selection, state) {
    const markType = 'kerning';
    const isDisabled = mark => normalizeMarkType(mark?.type) === 'kerning' && String(mark?.value || '').toLowerCase() === 'false';
    if (isCollapsedSelection(selection)) {
        if ((state.pendingMarks || []).some(isDisabled)) {
            removePendingMark(state, markType);
        } else {
            setPendingMark(state, { type: markType, value: 'false' });
        }

        return { changed: false, dirtyBlockIds: [] };
    }

    const shouldRemove = selectionRuns(model, selection)
        .filter(item => item.text.length > 0)
        .every(item => (item.run?.marks || []).some(isDisabled));
    return shouldRemove
        ? applyRemoveMark(model, selection, markType, state)
        : applyMarkToSelection(model, selection, markType, { type: markType, value: 'false' });
}

function applyClearFormatting(model, selection, state) {
    if (isCollapsedSelection(selection)) {
        state.pendingMarks = [];
        return { changed: false, dirtyBlockIds: [] };
    }

    const range = orderedSelection(selection, model);
    const dirtyBlockIds = new Set();
    for (const block of selectedEditableBlocks(model, range)) {
        const start = block.id === range.anchor.blockId ? range.anchor.offset : 0;
        const end = block.id === range.focus.blockId ? range.focus.offset : blockText(block).length;
        block.content.runs = rewriteRunsInRange(block, start, end, run => ({
            ...run,
            marks: (run.marks || []).filter(mark => !INLINE_MARK_TYPES.has(normalizeMarkType(mark?.type))),
        }));
        dirtyBlockIds.add(block.id);
    }

    return { changed: dirtyBlockIds.size > 0, dirtyBlockIds: Array.from(dirtyBlockIds) };
}

function applyMarkToSelection(model, selection, markType, mark) {
    const range = orderedSelection(selection, model);
    const dirtyBlockIds = new Set();
    for (const block of selectedEditableBlocks(model, range)) {
        const start = block.id === range.anchor.blockId ? range.anchor.offset : 0;
        const end = block.id === range.focus.blockId ? range.focus.offset : blockText(block).length;
        block.content.runs = rewriteRunsInRange(block, start, end, run => {
            const removeTypes = exclusiveMarkTypes(markType, mark);
            const existing = (run.marks || []).filter(item => !removeTypes.has(normalizeMarkType(item?.type)));
            return {
                ...run,
                marks: mark ? [...existing, cloneMark(mark)] : existing,
            };
        });
        dirtyBlockIds.add(block.id);
    }

    return { changed: dirtyBlockIds.size > 0, dirtyBlockIds: Array.from(dirtyBlockIds) };
}

function applyChangeCase(model, selection, argument) {
    const variant = normalizeChangeCaseVariant(argument);
    if (!variant || isCollapsedSelection(selection)) {
        return { changed: false, dirtyBlockIds: [] };
    }

    const range = orderedSelection(selection, model);
    const dirtyBlockIds = new Set();
    for (const block of selectedEditableBlocks(model, range)) {
        const start = block.id === range.anchor.blockId ? range.anchor.offset : 0;
        const end = block.id === range.focus.blockId ? range.focus.offset : blockText(block).length;
        let blockChanged = false;
        block.content.runs = rewriteRunsInRange(block, start, end, run => {
            const nextText = changeCharacterCase(run.text || '', variant);
            blockChanged = blockChanged || nextText !== run.text;
            return { ...run, text: nextText };
        });
        if (blockChanged) {
            dirtyBlockIds.add(block.id);
        }
    }

    return { changed: dirtyBlockIds.size > 0, dirtyBlockIds: Array.from(dirtyBlockIds) };
}

function rewriteRunsInRange(block, startOffset, endOffset, transform) {
    const output = [];
    let cursor = 0;
    const start = clampOffset(block, startOffset);
    const end = clampOffset(block, Math.max(start, Number(endOffset || 0) || 0));

    for (const run of runsOrEmpty(block)) {
        const text = createCanvasRunText(run);
        const runStart = cursor;
        const runEnd = cursor + text.length;
        cursor = runEnd;
        if (runEnd <= start || runStart >= end || String(run?.type || 'text') !== 'text') {
            output.push(clone(run));
            continue;
        }

        const localStart = Math.max(0, start - runStart);
        const localEnd = Math.min(text.length, end - runStart);
        if (localStart > 0) {
            const before = clone(run);
            before.id = `${run.id || block.id}-fmt-before-${start}`;
            before.text = text.slice(0, localStart);
            output.push(before);
        }

        if (localEnd > localStart) {
            const selectedRun = clone(run);
            selectedRun.id = `${run.id || block.id}-fmt-${start}-${end}`;
            selectedRun.text = text.slice(localStart, localEnd);
            const middle = transform(selectedRun);
            middle.id = middle.id || selectedRun.id;
            output.push(middle);
        }

        if (localEnd < text.length) {
            const after = clone(run);
            after.id = `${run.id || block.id}-fmt-after-${end}`;
            after.text = text.slice(localEnd);
            output.push(after);
        }
    }

    return compactRuns(output, block.id);
}

function selectedEditableBlocks(model, selection) {
    const range = orderedSelection(selection, model);
    const entries = editableBlockEntries(model);
    const startIndex = entries.findIndex(entry => String(entry.block?.id || '') === String(range.anchor.blockId || ''));
    const endIndex = entries.findIndex(entry => String(entry.block?.id || '') === String(range.focus.blockId || ''));
    if (startIndex < 0 || endIndex < 0) {
        return [];
    }

    return entries.slice(startIndex, endIndex + 1).map(entry => entry.block).filter(isEditableBlock);
}

function selectionRuns(model, selection) {
    const range = orderedSelection(selection, model);
    const items = [];
    for (const block of selectedEditableBlocks(model, range)) {
        const start = block.id === range.anchor.blockId ? range.anchor.offset : 0;
        const end = block.id === range.focus.blockId ? range.focus.offset : blockText(block).length;
        let cursor = 0;
        for (const run of runsOrEmpty(block)) {
            const text = createCanvasRunText(run);
            const runStart = cursor;
            const runEnd = cursor + text.length;
            cursor = runEnd;
            const localStart = Math.max(start, runStart);
            const localEnd = Math.min(end, runEnd);
            if (localEnd > localStart) {
                items.push({ block, run, text: text.slice(localStart - runStart, localEnd - runStart) });
            }
        }
    }

    return items;
}

function collectSelectedMarkValues(model, selection, markType, pending) {
    if (!selection) {
        return { total: 0, marked: 0, values: [] };
    }

    if (isCollapsedSelection(selection)) {
        const pendingMark = pending.find(mark => normalizeMarkType(mark?.type) === markType);
        if (pendingMark) {
            if (pendingMark.remove === true) {
                return { total: 1, marked: 0, values: [] };
            }

            return { total: 1, marked: 1, values: [markValue(pendingMark)] };
        }

        const block = findEditableBlock(model, selection.focus.blockId);
        const run = runAtOffset(block, selection.focus.offset);
        const mark = run?.marks?.find(item => normalizeMarkType(item?.type) === markType);
        return { total: 1, marked: mark ? 1 : 0, values: mark ? [markValue(mark)] : [] };
    }

    const values = [];
    let total = 0;
    let marked = 0;
    for (const item of selectionRuns(model, selection)) {
        total += 1;
        const mark = item.run?.marks?.find(candidate => normalizeMarkType(candidate?.type) === markType);
        if (mark) {
            marked += 1;
            values.push(markValue(mark));
        }
    }

    return { total, marked, values };
}

function formatCommandState(values, markType, isCollapsed) {
    const uniqueValues = Array.from(new Set(values.values.filter(value => value != null).map(String)));
    const active = values.total > 0 && values.marked === values.total;
    const mixed = values.marked > 0 && values.marked < values.total;
    return {
        disabled: false,
        active,
        mixed,
        value: uniqueValues.length === 1 ? uniqueValues[0] : null,
        state: mixed ? 'mixed' : active ? 'active' : 'inactive',
        isCollapsed,
        markType,
    };
}

function createValueMark(markType, argument) {
    const normalizedType = normalizeMarkType(markType);
    if (ADVANCED_INLINE_MARK_TYPES.includes(normalizedType)) {
        return createAdvancedCharacterMark(markType, argument);
    }

    const value = normalizeCommandValue(argument, normalizedType);
    if (!value) {
        return null;
    }

    if (normalizedType === 'link') {
        return { type: 'link', value, link: { href: value } };
    }

    return { type: markType, value };
}

function normalizeCommandValue(argument, markType = '') {
    if (argument == null) {
        return '';
    }

    let value;
    if (typeof argument === 'object') {
        value = argument.value ?? argument.href ?? argument.url ?? '';
    } else {
        value = argument;
    }

    if (markType === 'fontsize') {
        const normalized = String(value).trim().replace(/pt$/iu, '').trim();
        const parsed = Number(normalized);
        return Number.isFinite(parsed) && parsed > 0 ? parsed.toString() : '';
    }

    return String(value).trim();
}

function markValue(mark) {
    return mark?.link?.href || mark?.href || mark?.value || null;
}

// Tri-state toggle at a collapsed caret. The effective state is the inherited run mark overridden by any
// pending entry. Turning a mark OFF that is inherited records a remove-override (so typed text drops it);
// turning OFF a mark that is only pending-on just clears that add-override. Turning ON clears a remove-override
// (restoring the inherited mark) or adds an override when nothing is inherited.
function togglePendingMark(state, mark, inheritedMarks = []) {
    const type = normalizeMarkType(mark.type);
    const inheritedOn = (Array.isArray(inheritedMarks) ? inheritedMarks : [])
        .some(item => normalizeMarkType(item?.type) === type);
    const pendingMark = (state.pendingMarks || []).find(item => normalizeMarkType(item?.type) === type);
    const effectiveOn = pendingMark ? pendingMark.remove !== true : inheritedOn;
    if (effectiveOn) {
        if (inheritedOn) {
            setRemovePendingMark(state, type);
        } else {
            removePendingMark(state, type);
        }

        return;
    }

    if (inheritedOn) {
        removePendingMark(state, type);
    } else {
        setPendingMark(state, mark);
    }
}

function setPendingMark(state, mark) {
    for (const type of exclusiveMarkTypes(mark.type, mark)) {
        removePendingMark(state, type);
    }
    state.pendingMarks.push(cloneMark(mark));
}

function setRemovePendingMark(state, markType) {
    removePendingMark(state, markType);
    state.pendingMarks.push({ type: markType, remove: true });
}

function removePendingMark(state, markType) {
    const type = normalizeMarkType(markType);
    state.pendingMarks = (state.pendingMarks || []).filter(mark => normalizeMarkType(mark?.type) !== type);
}

function inheritedMarksAtCaret(model, selection) {
    const block = findEditableBlock(model, selection?.focus?.blockId);
    const run = runAtOffset(block, selection?.focus?.offset);
    return Array.isArray(run?.marks) ? run.marks : [];
}

function runAtOffset(block, offset) {
    if (!block) {
        return null;
    }

    const target = clampOffset(block, offset);
    let cursor = 0;
    let previous = null;
    for (const run of runsOrEmpty(block)) {
        const text = createCanvasRunText(run);
        const end = cursor + text.length;
        if (target >= cursor && target < end) {
            return run;
        }

        if (target === end) {
            previous = run;
        }

        cursor = end;
    }

    return previous || runsOrEmpty(block).find(run => String(run?.type || 'text') === 'text') || null;
}

function hasMark(run, markType) {
    return (run?.marks || []).some(mark => normalizeMarkType(mark?.type) === markType);
}

function markTypeToCommand(type) {
    const normalized = normalizeMarkType(type);
    if (normalized === 'strikethrough') {
        return 'strikethrough';
    }

    if (normalized === 'fontfamily') {
        return 'fontfamily';
    }

    if (normalized === 'fontsize') {
        return 'fontsize';
    }

    if (normalized === 'textcolor') {
        return 'textcolor';
    }

    if (normalized === 'smallcaps') {
        return 'smallcaps';
    }

    if (normalized === 'allcaps') {
        return 'allcaps';
    }

    if (normalized === 'doublestrikethrough') {
        return 'doublestrikethrough';
    }

    if (normalized === 'characterspacing') {
        return 'characterspacing';
    }

    if (normalized === 'characterscale') {
        return 'characterscale';
    }

    return normalized;
}

function exclusiveMarkTypes(markType, mark) {
    const type = normalizeMarkType(markType);
    const removeTypes = new Set([type]);
    if (type === 'superscript') {
        removeTypes.add('subscript');
    } else if (type === 'subscript') {
        removeTypes.add('superscript');
    } else if (type === 'smallcaps') {
        removeTypes.add('allcaps');
    } else if (type === 'allcaps') {
        removeTypes.add('smallcaps');
    } else if (type === 'strikethrough') {
        removeTypes.add('doublestrikethrough');
    } else if (type === 'doublestrikethrough') {
        removeTypes.add('strikethrough');
    }

    if (type === 'kerning' && mark?.value === 'true') {
        removeTypes.add('kerning');
    }

    return removeTypes;
}

function normalizeCommandId(commandId) {
    return String(commandId == null ? '' : commandId).replace(/[\s_-]/g, '').toLowerCase();
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

function editableBlockIndex(model, blockId) {
    return editableBlockEntries(model).findIndex(entry => String(entry.block?.id || '') === String(blockId || ''));
}

function findEditableBlock(model, blockId) {
    return editableBlockEntries(model).find(entry => String(entry.block?.id || '') === String(blockId || ''))?.block || null;
}

function firstEditablePosition(model) {
    const block = editableBlockEntries(model)[0]?.block || null;
    return block ? { blockId: block.id, offset: 0 } : null;
}

function isEditableBlock(block) {
    const type = String(block?.type || block?.content?.type || '').toLowerCase();
    return type === 'paragraph' || type === 'heading' || type === 'list' || type === 'quote';
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
        compacted.push({
            id: `${blockId || 'block'}-empty-run`,
            type: 'text',
            text: '',
            marks: [],
        });
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

function ensureBodyBlocks(model) {
    if (!model.body || typeof model.body !== 'object') {
        model.body = { blocks: [] };
    }

    if (!Array.isArray(model.body.blocks)) {
        model.body.blocks = [];
    }
}

function editableBlockEntries(model) {
    ensureBodyBlocks(model);
    const entries = [];
    appendEditableEntries(model.body.blocks, entries);
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

function synchronizeSectionsWithBody(model) {
    if (!Array.isArray(model.sections) || model.sections.length === 0) {
        return;
    }

    for (const section of model.sections) {
        const sectionId = String(section?.id || '');
        section.blocks = model.body.blocks.filter(block => String(block.sectionId || '') === sectionId);
    }
}

function cloneMark(mark) {
    return clone(mark || {});
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
