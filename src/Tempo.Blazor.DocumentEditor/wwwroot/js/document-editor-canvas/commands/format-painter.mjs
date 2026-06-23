import { createCanvasRunText, normalizeMarkType } from '../layout/canvas-text-style.mjs';

const FORMAT_PAINTER_COMMANDS = new Map([
    ['copyformat', 'copyFormatting'],
    ['copyformatting', 'copyFormatting'],
    ['formatpainter', 'copyFormatting'],
    ['lockformatpainter', 'copyFormatting'],
    ['pasteformat', 'pasteFormatting'],
    ['pasteformatting', 'pasteFormatting'],
    ['applyformat', 'pasteFormatting'],
    ['applyformatting', 'pasteFormatting'],
    ['cancelformatpainter', 'cancelFormatPainter'],
    ['clearformatpainter', 'cancelFormatPainter'],
]);

export function createFormatPainterState(initial = {}) {
    const source = initial && typeof initial === 'object' ? initial : {};
    return {
        active: source.active === true,
        sticky: source.sticky === true,
        payload: source.payload ? clone(source.payload) : null,
    };
}

export function isFormatPainterCommand(commandId) {
    return FORMAT_PAINTER_COMMANDS.has(normalizeCommandId(commandId));
}

export function canonicalFormatPainterCommandId(commandId) {
    return FORMAT_PAINTER_COMMANDS.get(normalizeCommandId(commandId)) || 'copyFormatting';
}

export function applyFormatPainterCommand(model, selection, commandId, argument = null, state = createFormatPainterState()) {
    const canonical = canonicalFormatPainterCommandId(commandId);
    const nextState = createFormatPainterState(state);

    if (canonical === 'cancelFormatPainter') {
        return {
            changed: false,
            model,
            selection,
            state: createFormatPainterState(),
            operation: canonical,
            dirtyBlockIds: [],
        };
    }

    if (canonical === 'copyFormatting') {
        const payload = captureFormatting(model, selection);
        if (!payload) {
            return { changed: false, model, selection, state: nextState, operation: canonical, dirtyBlockIds: [] };
        }

        return {
            changed: false,
            model,
            selection,
            state: {
                active: true,
                sticky: isStickyCopy(commandId, argument),
                payload,
            },
            operation: canonical,
            dirtyBlockIds: [],
            payload,
        };
    }

    if (!nextState.payload) {
        return { changed: false, model, selection, state: nextState, operation: canonical, dirtyBlockIds: [] };
    }

    const working = clone(model || {});
    const range = orderedSelection(working, selection);
    if (!range) {
        return { changed: false, model, selection, state: nextState, operation: canonical, dirtyBlockIds: [] };
    }

    const dirtyBlockIds = new Set();
    const payload = nextState.payload;
    for (const block of selectedBlocks(working, range)) {
        const start = block.id === range.anchor.blockId ? range.anchor.offset : 0;
        const end = block.id === range.focus.blockId ? range.focus.offset : blockText(block).length;
        if (applyMarksToBlock(block, start, end, payload.marks)) {
            dirtyBlockIds.add(block.id);
        }

        const beforeParagraph = JSON.stringify(block.paragraphProperties || {});
        block.paragraphProperties = clone(payload.paragraphProperties || {});
        if (JSON.stringify(block.paragraphProperties || {}) !== beforeParagraph) {
            dirtyBlockIds.add(block.id);
        }

        if (payload.contentFormat) {
            block.content = block.content && typeof block.content === 'object' ? block.content : { type: 'paragraph', runs: [] };
            Object.assign(block.content, clone(payload.contentFormat));
            dirtyBlockIds.add(block.id);
        }
    }

    if (dirtyBlockIds.size === 0) {
        return { changed: false, model, selection, state: nextState, operation: canonical, dirtyBlockIds: [] };
    }

    synchronizeSectionsWithBody(working);
    working.version = Number(working.version || 0) + 1;
    const stateAfterPaste = nextState.sticky
        ? nextState
        : createFormatPainterState();

    return {
        changed: true,
        model: working,
        selection: range,
        state: stateAfterPaste,
        operation: canonical,
        dirtyBlockIds: Array.from(dirtyBlockIds),
        payload: clone(payload),
    };
}

export function queryFormatPainterCommandState(model, selection, state = createFormatPainterState()) {
    const canCopy = captureFormatting(model, selection) !== null;
    const canPaste = state.payload !== null;
    const commands = {
        copyformatting: commandState(!canCopy, false),
        formatpainter: commandState(!canCopy, state.active === true),
        lockformatpainter: commandState(!canCopy, state.active === true && state.sticky === true),
        pasteformatting: commandState(!canPaste || !selection?.focus?.blockId, false),
        cancelformatpainter: commandState(!state.active && !state.payload, false),
    };

    return {
        commands,
        formatPainter: {
            active: state.active === true,
            sticky: state.sticky === true,
            hasPayload: state.payload !== null,
            sourceBlockId: state.payload?.sourceBlockId || null,
        },
    };
}

function captureFormatting(model, selection) {
    const range = orderedSelection(model, selection);
    if (!range) {
        return null;
    }

    const block = findBlock(model, range.anchor.blockId);
    if (!block) {
        return null;
    }

    const offset = range.anchor.offset === range.focus.offset
        ? range.focus.offset
        : range.anchor.offset;
    const run = runAtOffset(block, offset) || runsOrEmpty(block)[0] || null;
    const content = block.content && typeof block.content === 'object' ? block.content : {};
    return {
        sourceBlockId: block.id,
        marks: normalizeMarks(run?.marks || []),
        paragraphProperties: clone(block.paragraphProperties || {}),
        contentFormat: {
            type: content.type || block.type || 'paragraph',
            styleId: content.styleId ?? null,
            styleName: content.styleName ?? null,
            list: content.list ? clone(content.list) : null,
            headingLevel: content.headingLevel ?? null,
            outlineLevel: content.outlineLevel ?? null,
        },
    };
}

function applyMarksToBlock(block, startOffset, endOffset, marks) {
    const start = clampOffset(block, startOffset);
    const end = clampOffset(block, Math.max(start, Number(endOffset || 0) || 0));
    if (end <= start) {
        return false;
    }

    const before = JSON.stringify(block.content?.runs || []);
    block.content = block.content && typeof block.content === 'object' ? block.content : { type: 'paragraph', runs: [] };
    block.content.runs = rewriteRunsInRange(block, start, end, run => ({
        ...run,
        marks: normalizeMarks(marks),
    }));
    return JSON.stringify(block.content.runs || []) !== before;
}

function rewriteRunsInRange(block, startOffset, endOffset, transform) {
    const output = [];
    let cursor = 0;
    const start = clampOffset(block, startOffset);
    const end = clampOffset(block, endOffset);

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
            before.id = `${run.id || block.id}-painter-before-${start}`;
            before.text = text.slice(0, localStart);
            output.push(before);
        }

        if (localEnd > localStart) {
            const selected = clone(run);
            selected.id = `${run.id || block.id}-painter-${start}-${end}`;
            selected.text = text.slice(localStart, localEnd);
            output.push(transform(selected));
        }

        if (localEnd < text.length) {
            const after = clone(run);
            after.id = `${run.id || block.id}-painter-after-${end}`;
            after.text = text.slice(localEnd);
            output.push(after);
        }
    }

    return compactRuns(output, block.id);
}

function compactRuns(runs, blockId) {
    const compacted = [];
    for (const run of runs) {
        const normalized = {
            ...clone(run),
            type: run.type || 'text',
            text: String(run.text ?? ''),
            marks: normalizeMarks(run.marks || []),
        };
        const previous = compacted.at(-1);
        if (previous && normalized.type === 'text' && previous.type === 'text' && sameMarks(previous.marks, normalized.marks)) {
            previous.text = `${previous.text || ''}${normalized.text || ''}`;
        } else if (normalized.text.length > 0 || normalized.type !== 'text') {
            normalized.id = normalized.id || `${blockId}-painter-${compacted.length + 1}`;
            compacted.push(normalized);
        }
    }

    return compacted.length > 0 ? compacted : [{ id: `${blockId}-run`, type: 'text', text: '', marks: [] }];
}

function sameMarks(left, right) {
    return JSON.stringify(normalizeMarks(left)) === JSON.stringify(normalizeMarks(right));
}

function normalizeMarks(marks) {
    const byType = new Map();
    for (const mark of Array.isArray(marks) ? marks : []) {
        const type = normalizeMarkType(mark?.type);
        if (type) {
            byType.set(type, { ...clone(mark), type: mark.type || type });
        }
    }

    return Array.from(byType.values()).sort((left, right) => normalizeMarkType(left.type).localeCompare(normalizeMarkType(right.type)));
}

function selectedBlocks(model, range) {
    const blocks = editableBlocks(model);
    const startIndex = blocks.findIndex(block => String(block.id || '') === String(range.anchor.blockId || ''));
    const endIndex = blocks.findIndex(block => String(block.id || '') === String(range.focus.blockId || ''));
    if (startIndex < 0 || endIndex < 0) {
        return [];
    }

    return blocks.slice(startIndex, endIndex + 1);
}

function orderedSelection(model, selection) {
    const anchor = normalizePosition(model, selection?.anchor || selection?.focus);
    const focus = normalizePosition(model, selection?.focus || selection?.anchor);
    if (!anchor || !focus) {
        return null;
    }

    const blocks = editableBlocks(model);
    const anchorIndex = blocks.findIndex(block => String(block.id || '') === String(anchor.blockId || ''));
    const focusIndex = blocks.findIndex(block => String(block.id || '') === String(focus.blockId || ''));
    if (anchorIndex < 0 || focusIndex < 0) {
        return null;
    }

    if (anchorIndex < focusIndex || (anchorIndex === focusIndex && anchor.offset <= focus.offset)) {
        return { anchor, focus };
    }

    return { anchor: focus, focus: anchor };
}

function normalizePosition(model, position) {
    const block = findBlock(model, position?.blockId);
    if (!block) {
        return null;
    }

    return {
        blockId: block.id,
        offset: clampOffset(block, position?.offset),
    };
}

function runAtOffset(block, offset) {
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

    return previous || null;
}

function blockText(block) {
    return runsOrEmpty(block).map(run => createCanvasRunText(run)).join('');
}

function clampOffset(block, offset) {
    const length = blockText(block).length;
    return Math.max(0, Math.min(length, Number(offset || 0) || 0));
}

function runsOrEmpty(block) {
    return Array.isArray(block?.content?.runs) ? block.content.runs : [];
}

function findBlock(model, blockId) {
    const id = String(blockId || '');
    return editableBlocks(model).find(block => String(block.id || '') === id) || null;
}

function editableBlocks(model) {
    const blocks = [];
    const visit = block => {
        if (!block || typeof block !== 'object') {
            return;
        }

        if (Array.isArray(block?.content?.runs)) {
            blocks.push(block);
        }

        for (const row of block?.content?.table?.rows || []) {
            for (const cell of row?.cells || []) {
                for (const child of cell?.blocks || []) {
                    visit(child);
                }
            }
        }

        for (const child of block?.content?.contentControl?.blocks || []) {
            visit(child);
        }
    };

    for (const block of model?.body?.blocks || []) {
        visit(block);
    }
    return blocks;
}

function synchronizeSectionsWithBody(model) {
    if (!Array.isArray(model?.sections)) {
        return;
    }

    const byId = new Map(editableBlocks(model).map(block => [String(block.id || ''), block]));
    for (const section of model.sections) {
        if (!Array.isArray(section?.blocks)) {
            continue;
        }

        for (let index = 0; index < section.blocks.length; index += 1) {
            const replacement = byId.get(String(section.blocks[index]?.id || ''));
            if (replacement) {
                section.blocks[index] = clone(replacement);
            }
        }
    }
}

function isStickyCopy(commandId, argument) {
    return normalizeCommandId(commandId) === 'lockformatpainter'
        || argument?.sticky === true
        || argument?.lock === true
        || argument?.mode === 'lock';
}

function commandState(disabled, active) {
    return {
        disabled,
        active,
        mixed: false,
        value: null,
        state: disabled ? 'disabled' : active ? 'active' : 'inactive',
    };
}

function normalizeCommandId(commandId) {
    return String(commandId == null ? '' : commandId).replace(/[\s_-]/g, '').toLowerCase();
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
