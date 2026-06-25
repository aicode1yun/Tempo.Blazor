import { createCanvasRunText } from '../layout/canvas-text-style.mjs';
import { createMathContentFromLinear, mathToAccessibleText, normalizeMathRun } from '../math/math-model.mjs';
import {
    addMathMatrixColumn,
    addMathMatrixRow,
    collectMathSlots,
    createMathSlotRange,
    deleteTextInMathSlot,
    insertTextInMathSlot,
    moveMathSlot,
    replaceContentInMathSlot,
} from '../math/math-caret.mjs';

const MATH_COMMAND_ALIASES = new Map([
    ['insertequation', 'insertEquation'],
    ['insertmath', 'insertEquation'],
    ['insertlinearmath', 'insertLinearMath'],
    ['insertmathlinear', 'insertLinearMath'],
    ['insertmathsymbol', 'insertMathSymbol'],
    ['insertequationsymbol', 'insertMathSymbol'],
    ['insertfraction', 'insertFraction'],
    ['insertradical', 'insertRadical'],
    ['insertsquareroot', 'insertRadical'],
    ['insertsuperscript', 'insertSuperscript'],
    ['insertsubscript', 'insertSubscript'],
    ['insertnary', 'insertNary'],
    ['insertsum', 'insertNary'],
    ['insertproduct', 'insertNary'],
    ['insertdelimiter', 'insertDelimiter'],
    ['insertparentheses', 'insertDelimiter'],
    ['insertlimit', 'insertLimit'],
    ['insertaccent', 'insertAccent'],
    ['insertbar', 'insertBar'],
    ['insertborderbox', 'insertBorderBox'],
    ['insertmatrix', 'insertMatrix'],
    ['setmathdisplaymode', 'setMathDisplayMode'],
    ['togglemathdisplaymode', 'setMathDisplayMode'],
    ['deactivatemathslot', 'deactivateMathSlot'],
    ['exitmathslot', 'deactivateMathSlot'],
    ['activatemathslot', 'activateMathSlot'],
    ['selectmathslot', 'activateMathSlot'],
    ['focusmathslot', 'activateMathSlot'],
    ['selectmathslotrange', 'selectMathSlotRange'],
    ['selectmathrange', 'selectMathSlotRange'],
    ['movemathslot', 'moveMathSlot'],
    ['nextmathslot', 'moveMathSlot'],
    ['previousmathslot', 'moveMathSlot'],
    ['insertmathslottext', 'insertMathSlotText'],
    ['insertmathslotlinear', 'insertMathSlotText'],
    ['insertmathslotsymbol', 'insertMathSlotText'],
    ['deletemathslotbackward', 'deleteMathSlotBackward'],
    ['backspacemathslot', 'deleteMathSlotBackward'],
    ['deletemathslotforward', 'deleteMathSlotForward'],
    ['deletemathslot', 'deleteMathSlotForward'],
    ['addmathmatrixrow', 'addMathMatrixRow'],
    ['insertmathmatrixrow', 'addMathMatrixRow'],
    ['addmathmatrixcolumn', 'addMathMatrixColumn'],
    ['insertmathmatrixcolumn', 'addMathMatrixColumn'],
]);

export function isMathCommand(commandId) {
    return MATH_COMMAND_ALIASES.has(compact(commandId));
}

export function canonicalMathCommandId(commandId) {
    return MATH_COMMAND_ALIASES.get(compact(commandId)) || '';
}

export function applyMathCommand(model, selection, commandId, payload = null) {
    const command = canonicalMathCommandId(commandId);
    const working = clone(model || {});
    ensureModelCollections(working);

    if (command === 'insertEquation') {
        return insertEquation(working, selection, payload, 'insertEquation');
    }

    if (command === 'insertLinearMath') {
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return editMathSlotText(working, selection, {
                ...(payload || {}),
                linear: payload?.linear ?? payload?.Linear ?? payload?.text ?? payload?.Text ?? 'x',
                replace: payload?.replace ?? payload?.Replace ?? true,
            }, 'insertMathSlotText');
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content: createMathContentFromLinear(payload?.linear ?? payload?.Linear ?? payload?.text ?? payload?.Text ?? 'x'),
        }, 'insertLinearMath');
    }

    if (command === 'insertMathSymbol') {
        const symbol = payload?.symbol ?? payload?.Symbol ?? payload?.text ?? payload?.Text ?? '\\alpha';
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return editMathSlotText(working, selection, {
                ...(payload || {}),
                symbol,
            }, 'insertMathSlotText');
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content: createMathContentFromLinear(symbol),
        }, 'insertMathSymbol');
    }

    if (command === 'insertFraction') {
        const content = {
            elements: [{
                type: 'fraction',
                numerator: payload?.numerator || payload?.Numerator || createMathContentFromLinear(payload?.top ?? payload?.Top ?? ''),
                denominator: payload?.denominator || payload?.Denominator || createMathContentFromLinear(payload?.bottom ?? payload?.Bottom ?? ''),
                fractionType: payload?.fractionType || payload?.FractionType || 'bar',
            }],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertFraction');
    }

    if (command === 'insertRadical') {
        const content = {
            elements: [{
                type: 'radical',
                radicand: payload?.radicand || payload?.Radicand || createMathContentFromLinear(payload?.text ?? payload?.Text ?? ''),
                degree: payload?.degree || payload?.Degree || null,
            }],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertRadical');
    }

    if (command === 'insertSuperscript') {
        const content = {
            elements: [{
                type: 'sup',
                base: payload?.base || payload?.Base || createMathContentFromLinear(payload?.baseText ?? payload?.BaseText ?? ''),
                superscript: payload?.superscript || payload?.Superscript || createMathContentFromLinear(payload?.scriptText ?? payload?.ScriptText ?? ''),
            }],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertSuperscript');
    }

    if (command === 'insertSubscript') {
        const content = {
            elements: [{
                type: 'sub',
                base: payload?.base || payload?.Base || createMathContentFromLinear(payload?.baseText ?? payload?.BaseText ?? ''),
                subscript: payload?.subscript || payload?.Subscript || createMathContentFromLinear(payload?.scriptText ?? payload?.ScriptText ?? ''),
            }],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertSubscript');
    }

    if (command === 'insertNary') {
        const content = {
            elements: [createNaryElement(payload || {})],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertNary');
    }

    if (command === 'insertDelimiter') {
        const content = {
            elements: [createDelimiterElement(payload || {})],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertDelimiter');
    }

    if (command === 'insertLimit') {
        const content = {
            elements: [{
                type: 'limit',
                base: payload?.base || payload?.Base || createMathContentFromLinear(payload?.baseText ?? payload?.BaseText ?? 'lim'),
                lowerLimit: payload?.lowerLimit || payload?.LowerLimit || createMathContentFromLinear(payload?.lowerText ?? payload?.LowerText ?? ''),
                upperLimit: payload?.upperLimit || payload?.UpperLimit || null,
                content: payload?.content || payload?.Content || createMathContentFromLinear(payload?.text ?? payload?.Text ?? ''),
            }],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertLimit');
    }

    if (command === 'insertAccent') {
        const content = {
            elements: [{
                type: 'accent',
                accent: payload?.accent ?? payload?.Accent ?? '̂',
                base: payload?.base || payload?.Base || createMathContentFromLinear(payload?.baseText ?? payload?.BaseText ?? ''),
            }],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertAccent');
    }

    if (command === 'insertBar') {
        const content = {
            elements: [{
                type: 'bar',
                position: payload?.position ?? payload?.Position ?? 'over',
                base: payload?.base || payload?.Base || createMathContentFromLinear(payload?.baseText ?? payload?.BaseText ?? ''),
            }],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertBar');
    }

    if (command === 'insertBorderBox') {
        const content = {
            elements: [{
                type: 'borderBox',
                content: payload?.content || payload?.Content || createMathContentFromLinear(payload?.text ?? payload?.Text ?? ''),
            }],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertBorderBox');
    }

    if (command === 'insertMatrix') {
        const content = {
            elements: [createMatrixElement(payload || {})],
        };
        if (shouldTargetActiveMathSlot(selection, payload)) {
            return replaceActiveMathSlotWithContent(working, selection, content, command);
        }

        return insertEquation(working, selection, {
            ...(payload || {}),
            content,
        }, 'insertMatrix');
    }

    if (command === 'setMathDisplayMode') {
        return setMathDisplayMode(working, selection, payload, 'setMathDisplayMode');
    }

    if (command === 'deactivateMathSlot') {
        return deactivateMathSlot(working, selection, payload, 'deactivateMathSlot');
    }

    if (command === 'activateMathSlot') {
        return activateMathSlot(working, selection, payload, 'activateMathSlot');
    }

    if (command === 'selectMathSlotRange') {
        return selectMathSlotRange(working, selection, payload, 'selectMathSlotRange');
    }

    if (command === 'moveMathSlot') {
        return moveActiveMathSlot(working, selection, payload, 'moveMathSlot');
    }

    if (command === 'insertMathSlotText') {
        return editMathSlotText(working, selection, payload, 'insertMathSlotText');
    }

    if (command === 'deleteMathSlotBackward') {
        return deleteMathSlotText(working, selection, payload, 'deleteMathSlotBackward', 'backward');
    }

    if (command === 'deleteMathSlotForward') {
        return deleteMathSlotText(working, selection, payload, 'deleteMathSlotForward', 'forward');
    }

    if (command === 'addMathMatrixRow') {
        return editMathMatrix(working, selection, payload, 'addMathMatrixRow', 'row');
    }

    if (command === 'addMathMatrixColumn') {
        return editMathMatrix(working, selection, payload, 'addMathMatrixColumn', 'column');
    }

    return unchanged(working, selection, command);
}

export function queryMathCommandState(model, selection) {
    ensureModelCollections(model);
    const hasDocument = !!model;
    const hasBodySelection = !!findEditableBlock(model, selection?.focus?.blockId || selection?.anchor?.blockId);
    const activeRun = findMathAtSelection(model, selection);
    const activeSlot = selection?.math || selection?.Math || null;
    const hasActiveMath = !!activeRun;
    return {
        math: {
            activeMathId: activeRun?.math?.mathId || activeRun?.math?.MathId || activeRun?.id || null,
            displayMode: normalizeMathRun(activeRun || {}).displayMode,
            activeSlotName: activeSlot?.slotName || activeSlot?.SlotName || null,
            activeSlotPath: activeSlot?.slotPath || activeSlot?.SlotPath || null,
            activeSlotOffset: activeSlot?.offset ?? activeSlot?.Offset ?? null,
        },
        commands: {
            insertequation: commandState(hasDocument),
            insertmath: commandState(hasDocument),
            insertlinearmath: commandState(hasBodySelection),
            insertmathsymbol: commandState(hasBodySelection),
            insertfraction: commandState(hasBodySelection),
            insertradical: commandState(hasBodySelection),
            insertsuperscript: commandState(hasBodySelection),
            insertsubscript: commandState(hasBodySelection),
            insertnary: commandState(hasBodySelection),
            insertdelimiter: commandState(hasBodySelection),
            insertlimit: commandState(hasBodySelection),
            insertaccent: commandState(hasBodySelection),
            insertbar: commandState(hasBodySelection),
            insertborderbox: commandState(hasBodySelection),
            insertmatrix: commandState(hasBodySelection),
            setmathdisplaymode: commandState(hasActiveMath, normalizeMathRun(activeRun || {}).displayMode === 1),
            deactivatemathslot: commandState(hasActiveMath),
            activatemathslot: commandState(hasActiveMath),
            selectmathslotrange: commandState(hasActiveMath),
            movemathslot: commandState(hasActiveMath),
            insertmathslottext: commandState(hasActiveMath),
            deletemathslotbackward: commandState(hasActiveMath),
            deletemathslotforward: commandState(hasActiveMath),
            addmathmatrixrow: commandState(hasActiveMath),
            addmathmatrixcolumn: commandState(hasActiveMath),
        },
    };
}

function insertEquation(model, selection, payload, operation) {
    const target = resolveInsertionTarget(model, selection, payload);
    if (!target) {
        return unchanged(model, selection, operation);
    }

    const run = createMathRun(payload || {});
    insertRunAtOffset(target.block, run, target.offset);
    model.version = Number(model.version || 0) + 1;
    const firstSlot = firstEditableMathSlot(run);
    if (shouldActivateInsertedMathSlot(payload, operation) && firstSlot) {
        const insertedTarget = {
            model,
            block: target.block,
            run,
            runStart: target.offset,
            runEnd: target.offset + createCanvasRunText(run).length,
            runLength: createCanvasRunText(run).length,
            mathId: run.math.mathId,
            selection,
        };
        const slotOffset = Math.min(firstSlot.textLength, Number(payload?.slotOffset ?? payload?.SlotOffset ?? 0) || 0);
        return {
            changed: true,
            viewChanged: true,
            model,
            selection: mathSlotSelection(insertedTarget, firstSlot, slotOffset),
            operation,
            dirtyBlockIds: [target.block.id],
            insertedRunIds: [run.id],
            mathId: run.math.mathId,
            mathSlot: slotPayload(firstSlot, slotOffset),
            announcement: firstSlot.slotName,
        };
    }

    return {
        changed: true,
        model,
        selection: collapsedSelection(target.block.id, target.offset + createCanvasRunText(run).length),
        operation,
        dirtyBlockIds: [target.block.id],
        insertedRunIds: [run.id],
        mathId: run.math.mathId,
    };
}

function shouldActivateInsertedMathSlot(payload, operation) {
    if (payload?.activateFirstSlot === false || payload?.ActivateFirstSlot === false) {
        return false;
    }

    if (payload?.activateFirstSlot === true || payload?.ActivateFirstSlot === true) {
        return true;
    }

    const command = compact(operation);
    return command === 'insertfraction'
        || command === 'insertradical'
        || command === 'insertsuperscript'
        || command === 'insertsubscript'
        || command === 'insertnary'
        || command === 'insertdelimiter'
        || command === 'insertlimit'
        || command === 'insertaccent'
        || command === 'insertbar'
        || command === 'insertborderbox'
        || command === 'insertmatrix';
}

function firstEditableMathSlot(run) {
    return collectMathSlots(run).find(slot => slot.path.length > 0) || null;
}

function createMathRun(payload) {
    const providedContent = payload.content || payload.Content || null;
    const mathML = payload.mathML ?? payload.MathML ?? null;
    const content = providedContent || (mathML ? null : createMathContentFromLinear(payload.linear ?? payload.Linear ?? payload.text ?? payload.Text ?? 'x'));
    const displayMode = normalizeDisplayMode(payload.displayMode ?? payload.DisplayMode);
    const run = {
        id: String(payload.id || payload.Id || createId('math-run')),
        type: 'math',
        text: '',
        marks: Array.isArray(payload.marks || payload.Marks) ? (payload.marks || payload.Marks) : [],
        math: {
            mathId: String(payload.mathId || payload.MathId || createId('math')),
            displayMode,
            content,
            altText: String(payload.altText ?? payload.AltText ?? ''),
            mathML,
            ommlXml: payload.ommlXml ?? payload.OmmlXml ?? null,
            metadata: payload.metadata ?? payload.Metadata ?? {},
        },
    };
    const normalized = normalizeMathRun(run);
    run.math.content = normalized.content;
    run.math.altText ||= mathToAccessibleText(normalized);
    return run;
}

function createNaryElement(payload) {
    const requested = compact(payload.operator ?? payload.Operator ?? payload.kind ?? payload.Kind ?? payload.preset ?? payload.Preset ?? 'sum');
    const operator = requested === 'product' || requested === 'prod' || requested === '∏'
        ? '∏'
        : requested === 'integral' || requested === 'int' || requested === '∫'
            ? '∫'
            : '∑';
    return {
        type: 'nary',
        operator,
        lowerLimit: payload.lowerLimit || payload.LowerLimit || createMathContentFromLinear(payload.lowerText ?? payload.LowerText ?? ''),
        upperLimit: payload.upperLimit || payload.UpperLimit || createMathContentFromLinear(payload.upperText ?? payload.UpperText ?? ''),
        base: payload.base || payload.Base || createMathContentFromLinear(payload.text ?? payload.Text ?? ''),
        limitsPlacement: normalizeLimitsPlacement(payload.limitsPlacement ?? payload.LimitsPlacement ?? payload.placement ?? payload.Placement),
    };
}

function createDelimiterElement(payload) {
    return {
        type: 'delimiter',
        open: String(payload.open ?? payload.Open ?? '('),
        close: String(payload.close ?? payload.Close ?? ')'),
        separator: String(payload.separator ?? payload.Separator ?? ''),
        content: payload.content || payload.Content || createMathContentFromLinear(payload.text ?? payload.Text ?? ''),
    };
}

function createMatrixElement(payload) {
    const rowCount = Math.max(1, Math.min(12, integer(payload.rows ?? payload.Rows, 2)));
    const columnCount = Math.max(1, Math.min(12, integer(payload.columns ?? payload.Columns, 2)));
    const values = Array.isArray(payload.values || payload.Values) ? (payload.values || payload.Values) : [];
    return {
        type: 'matrix',
        rows: Array.from({ length: rowCount }, (_, rowIndex) => ({
            cells: Array.from({ length: columnCount }, (_, columnIndex) => {
                const value = values[rowIndex]?.[columnIndex] ?? values[rowIndex * columnCount + columnIndex] ?? '';
                return createMathContentFromLinear(value || `${rowIndex + 1}${columnIndex + 1}`);
            }),
        })),
    };
}

function setMathDisplayMode(model, selection, payload, operation) {
    const target = resolveMathTarget(model, selection, payload);
    if (!target) {
        return unchanged(model, selection, operation);
    }

    const current = normalizeMathRun(target.run || {}).displayMode;
    const requested = payload?.displayMode ?? payload?.DisplayMode ?? payload?.mode ?? payload?.Mode;
    const displayMode = requested == null ? (current === 1 ? 0 : 1) : normalizeDisplayMode(requested);
    if (displayMode === current) {
        return unchanged(model, selection, operation);
    }

    const normalized = normalizeMathRun(target.run || {});
    target.run.math = {
        mathId: normalized.mathId,
        displayMode,
        content: normalized.content,
        altText: normalized.altText || mathToAccessibleText(normalized),
        mathML: normalized.mathML,
        ommlXml: normalized.ommlXml,
        metadata: normalized.metadata || {},
    };
    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: preserveMathSelection(target, selection),
        operation,
        dirtyBlockIds: [target.block.id],
        mathId: target.mathId,
    };
}

function deactivateMathSlot(model, selection, payload, operation) {
    const target = resolveMathTarget(model, selection, payload);
    if (!target) {
        return unchanged(model, selection, operation);
    }

    return {
        changed: false,
        viewChanged: true,
        model,
        selection: collapsedSelection(target.block.id, target.runEnd),
        operation,
        dirtyBlockIds: [],
        mathId: target.mathId,
        mathSlot: {
            mathId: target.mathId,
            runId: target.run.id || '',
            slotPath: [],
            slotName: 'equation',
            offset: 0,
            exit: true,
        },
        announcement: 'equation',
    };
}

function activateMathSlot(model, selection, payload, operation) {
    const target = resolveMathTarget(model, selection, payload);
    if (!target) {
        return unchanged(model, selection, operation);
    }

    const slot = resolveMathSlot(target.run, selection, payload);
    if (!slot) {
        return unchanged(model, selection, operation);
    }

    const offset = resolveSlotOffset(slot, selection, payload);
    return {
        changed: false,
        viewChanged: true,
        model,
        selection: mathSlotSelection(target, slot, offset),
        operation,
        dirtyBlockIds: [],
        mathId: target.mathId,
        mathSlot: slotPayload(slot, offset),
        announcement: slot.slotName,
    };
}

function selectMathSlotRange(model, selection, payload, operation) {
    const target = resolveMathTarget(model, selection, payload);
    if (!target) {
        return unchanged(model, selection, operation);
    }

    const anchorPath = payload?.anchorSlotPath || payload?.AnchorSlotPath || selection?.math?.slotPath || selection?.Math?.SlotPath;
    const focusPath = payload?.focusSlotPath || payload?.FocusSlotPath || payload?.slotPath || payload?.SlotPath || payload?.path || payload?.Path;
    const range = createMathSlotRange(target.run, anchorPath, focusPath);
    if (!range.anchor || !range.focus || range.selectedSlots.length === 0) {
        return unchanged(model, selection, operation);
    }

    const caretOffset = Math.max(target.runStart, Math.min(target.runEnd, target.runStart + target.runLength));
    const position = { blockId: String(target.block.id || ''), offset: caretOffset };
    const selectedSlotPaths = range.selectedSlots.map(slot => slot.path.slice());
    return {
        changed: false,
        viewChanged: true,
        model,
        selection: {
            anchor: position,
            focus: { ...position },
            math: {
                mathId: target.mathId,
                runId: target.run.id || '',
                slotPath: range.focus.path.slice(),
                slotName: range.focus.slotName,
                offset: range.focus.textLength,
                anchorSlotPath: range.anchor.path.slice(),
                focusSlotPath: range.focus.path.slice(),
                selectedSlotPaths,
                structuralPath: range.structuralPath.slice(),
                structuralRange: true,
                isReversed: range.isReversed,
            },
        },
        operation,
        dirtyBlockIds: [],
        mathId: target.mathId,
        mathSlot: slotPayload(range.focus, range.focus.textLength),
        announcement: range.focus.slotName,
    };
}

function moveActiveMathSlot(model, selection, payload, operation) {
    const target = resolveMathTarget(model, selection, payload);
    if (!target) {
        return unchanged(model, selection, operation);
    }

    const currentSlot = resolveMathSlot(target.run, selection, payload);
    if (!currentSlot) {
        return unchanged(model, selection, operation);
    }

    const direction = payload?.direction ?? payload?.Direction
        ?? (compact(operation) === 'previousmathslot' ? 'previous' : 'next');
    const moved = moveMathSlot(target.run, currentSlot.path, direction);
    if (!moved.slot) {
        return unchanged(model, selection, operation);
    }

    const offset = moved.slot.textLength;
    return {
        changed: false,
        viewChanged: true,
        model,
        selection: mathSlotSelection(target, moved.slot, offset),
        operation,
        dirtyBlockIds: [],
        mathId: target.mathId,
        mathSlot: slotPayload(moved.slot, offset),
        announcement: moved.slot.slotName,
    };
}

function editMathSlotText(model, selection, payload, operation) {
    const target = resolveMathTarget(model, selection, payload);
    if (!target) {
        return unchanged(model, selection, operation);
    }

    const slot = resolveMathSlot(target.run, selection, payload);
    if (!slot) {
        return unchanged(model, selection, operation);
    }

    const offset = resolveSlotOffset(slot, selection, payload);
    const edited = shouldReplaceSlotContent(payload)
        ? replaceContentInMathSlot(target.run, slot.path, resolveMathSlotReplacement(payload), { offset })
        : insertTextInMathSlot(target.run, slot.path, resolveMathSlotText(payload), { offset });
    return commitMathEdit(model, target, edited, operation);
}

function deleteMathSlotText(model, selection, payload, operation, direction) {
    const target = resolveMathTarget(model, selection, payload);
    if (!target) {
        return unchanged(model, selection, operation);
    }

    const slot = resolveMathSlot(target.run, selection, payload);
    if (!slot) {
        return unchanged(model, selection, operation);
    }

    const edited = deleteTextInMathSlot(target.run, slot.path, {
        offset: resolveSlotOffset(slot, selection, payload),
        direction,
    });
    return commitMathEdit(model, target, edited, operation);
}

function editMathMatrix(model, selection, payload, operation, dimension) {
    const target = resolveMathTarget(model, selection, payload);
    if (!target) {
        return unchanged(model, selection, operation);
    }

    const matrixPath = payload?.matrixPath || payload?.MatrixPath || payload?.path || payload?.Path || findFirstMatrixPath(target.run);
    if (!matrixPath) {
        return unchanged(model, selection, operation);
    }

    const edited = dimension === 'row'
        ? addMathMatrixRow(target.run, matrixPath, payload || {})
        : addMathMatrixColumn(target.run, matrixPath, payload || {});
    if (edited.changed === false) {
        return unchanged(model, selection, operation);
    }

    return commitMathEdit(model, target, edited, operation);
}

function commitMathEdit(model, target, edited, operation) {
    if (edited.changed === false) {
        return unchanged(model, target.selection, operation);
    }

    target.run.math = {
        mathId: edited.math.mathId,
        displayMode: edited.math.displayMode,
        content: edited.math.content,
        altText: edited.math.altText || mathToAccessibleText(edited.math),
        mathML: edited.math.mathML,
        ommlXml: edited.math.ommlXml,
        metadata: edited.math.metadata || {},
    };
    target.run.text = '';
    model.version = Number(model.version || 0) + 1;
    const selection = mathSlotSelection(target, edited.slot, edited.offset);
    return {
        changed: true,
        model,
        selection,
        operation,
        dirtyBlockIds: [target.block.id],
        mathId: target.mathId,
        mathSlot: slotPayload(edited.slot, edited.offset),
        announcement: edited.slot?.slotName || '',
    };
}

function resolveMathTarget(model, selection, payload) {
    const requestedMathId = String(payload?.mathId ?? payload?.MathId ?? selection?.math?.mathId ?? selection?.Math?.MathId ?? '');
    const requestedRunId = String(payload?.runId ?? payload?.RunId ?? selection?.math?.runId ?? selection?.Math?.RunId ?? '');
    const requestedBlockId = String(payload?.blockId ?? payload?.BlockId ?? selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    const requestedOffset = Number(selection?.focus?.offset ?? selection?.anchor?.offset ?? 0) || 0;

    for (const block of allBodyBlocks(model)) {
        let cursor = 0;
        const runs = runsOrEmpty(block);
        for (let index = 0; index < runs.length; index += 1) {
            const run = runs[index];
            const length = createCanvasRunText(run).length;
            const end = cursor + length;
            const isMath = String(run?.type || '').toLowerCase() === 'math' || !!run?.math;
            const mathId = String(run?.math?.mathId || run?.math?.MathId || run?.id || '');
            if (isMath
                && ((requestedMathId && mathId === requestedMathId)
                    || (requestedRunId && String(run?.id || '') === requestedRunId)
                    || (!requestedMathId && !requestedRunId && String(block?.id || '') === requestedBlockId && requestedOffset >= cursor && requestedOffset <= end))) {
                return {
                    model,
                    block,
                    run,
                    runIndex: index,
                    runStart: cursor,
                    runEnd: end,
                    runLength: length,
                    mathId,
                    selection,
                };
            }

            cursor = end;
        }
    }

    return null;
}

function resolveMathSlot(run, selection, payload) {
    const slots = collectMathSlots(run, { includeRoot: true });
    const path = payload?.slotPath || payload?.SlotPath || payload?.path || payload?.Path || selection?.math?.slotPath || selection?.Math?.SlotPath;
    const slotName = payload?.slotName || payload?.SlotName || selection?.math?.slotName || selection?.Math?.SlotName || '';
    const pathKey = JSON.stringify(normalizePath(path));
    if (path != null) {
        const byPath = slots.find(slot => JSON.stringify(slot.path) === pathKey);
        if (byPath) {
            return byPath;
        }
    }

    if (slotName) {
        const compactName = compact(slotName);
        const byName = slots.find(slot => compact(slot.slotName) === compactName);
        if (byName) {
            return byName;
        }
    }

    return slots.find(slot => slot.path.length > 0) || slots[0] || null;
}

function resolveSlotOffset(slot, selection, payload) {
    const value = payload?.offset ?? payload?.Offset ?? selection?.math?.offset ?? selection?.Math?.Offset;
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) {
        return Math.max(0, Number(slot?.textLength || 0) || 0);
    }

    return Math.max(0, Math.min(Math.max(0, Number(slot?.textLength || 0) || 0), Math.trunc(parsed)));
}

function resolveMathSlotText(payload) {
    if (payload?.symbol != null || payload?.Symbol != null) {
        return mathToAccessibleText(createMathContentFromLinear(payload.symbol ?? payload.Symbol));
    }

    if (payload?.linear != null || payload?.Linear != null) {
        return mathToAccessibleText(createMathContentFromLinear(payload.linear ?? payload.Linear));
    }

    return String(payload?.text ?? payload?.Text ?? '');
}

function resolveMathSlotReplacement(payload) {
    if (payload?.content || payload?.Content) {
        return payload.content || payload.Content;
    }

    if (payload?.symbol != null || payload?.Symbol != null) {
        return createMathContentFromLinear(payload.symbol ?? payload.Symbol);
    }

    return createMathContentFromLinear(payload?.linear ?? payload?.Linear ?? payload?.text ?? payload?.Text ?? '');
}

function shouldReplaceSlotContent(payload) {
    return payload?.replace === true
        || payload?.Replace === true
        || payload?.mode === 'replace'
        || payload?.Mode === 'replace';
}

function shouldTargetActiveMathSlot(selection, payload) {
    if (!selection?.math && !selection?.Math) {
        return false;
    }

    return payload?.blockId == null
        && payload?.BlockId == null
        && payload?.offset == null
        && payload?.Offset == null
        && payload?.mathId == null
        && payload?.MathId == null
        && payload?.runId == null
        && payload?.RunId == null;
}

function replaceActiveMathSlotWithContent(model, selection, content, operation) {
    return editMathSlotText(model, selection, {
        content,
        replace: true,
    }, operation);
}

function mathSlotSelection(target, slot, offset) {
    const caretOffset = Math.max(target.runStart, Math.min(target.runEnd, target.runStart + target.runLength));
    const position = { blockId: String(target.block.id || ''), offset: caretOffset };
    return {
        anchor: position,
        focus: { ...position },
        math: {
            mathId: target.mathId,
            runId: target.run.id || '',
            slotPath: slot.path.slice(),
            slotName: slot.slotName,
            offset,
        },
    };
}

function preserveMathSelection(target, selection) {
    const existing = selection?.math || selection?.Math || null;
    if (existing) {
        const slot = resolveMathSlot(target.run, { math: existing }, existing);
        if (slot) {
            return mathSlotSelection(target, slot, resolveSlotOffset(slot, { math: existing }, existing));
        }
    }

    return collapsedSelection(target.block.id, target.runEnd);
}

function slotPayload(slot, offset) {
    return {
        mathId: slot.mathId || '',
        slotPath: slot.path.slice(),
        slotName: slot.slotName,
        offset,
        text: slot.text || '',
    };
}

function findFirstMatrixPath(run) {
    const math = normalizeMathRun(run || {});
    const elements = math.content?.elements || [];
    const index = elements.findIndex(element => String(element?.type || '').toLowerCase() === 'matrix');
    return index >= 0 ? ['elements', index] : null;
}

function normalizePath(value) {
    if (Array.isArray(value)) {
        return value.map(segment => numericOrString(segment));
    }

    if (typeof value === 'string') {
        const trimmed = value.trim();
        if (!trimmed) {
            return [];
        }

        try {
            const parsed = JSON.parse(trimmed);
            if (Array.isArray(parsed)) {
                return parsed.map(segment => numericOrString(segment));
            }
        } catch {
            return trimmed.split(/[./]/).filter(Boolean).map(segment => numericOrString(segment));
        }
    }

    return [];
}

function numericOrString(value) {
    if (typeof value === 'number') {
        return Math.max(0, Math.trunc(value));
    }

    const text = String(value ?? '');
    return /^\d+$/.test(text) ? Number(text) : text;
}

function resolveInsertionTarget(model, selection, payload) {
    const blockId = String(payload?.blockId ?? payload?.BlockId ?? selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    const block = findEditableBlock(model, blockId) || firstEditableBlock(model);
    if (!block) {
        return null;
    }

    const requestedOffset = payload?.offset ?? payload?.Offset ?? selection?.focus?.offset ?? selection?.anchor?.offset;
    return { block, offset: clampOffset(block, requestedOffset) };
}

function insertRunAtOffset(block, run, offset) {
    const runs = runsOrEmpty(block);
    const textLength = runs.reduce((total, item) => total + createCanvasRunText(item).length, 0);
    let targetOffset = Math.max(0, Math.min(textLength, Number(offset || 0) || 0));
    let cursor = 0;
    for (let index = 0; index < runs.length; index += 1) {
        const current = runs[index];
        const text = createCanvasRunText(current);
        const end = cursor + text.length;
        if (targetOffset <= end) {
            if (targetOffset === cursor) {
                runs.splice(index, 0, run);
                return;
            }

            if (targetOffset === end) {
                runs.splice(index + 1, 0, run);
                return;
            }

            const local = targetOffset - cursor;
            const left = { ...current, id: current.id ? `${current.id}-l` : createId('text'), text: text.slice(0, local) };
            const right = { ...current, id: current.id ? `${current.id}-r` : createId('text'), text: text.slice(local) };
            runs.splice(index, 1, left, run, right);
            return;
        }

        cursor = end;
    }

    runs.push(run);
}

function findMathAtSelection(model, selection) {
    const selectedMathId = String(selection?.math?.mathId || selection?.Math?.MathId || '');
    const selectedRunId = String(selection?.math?.runId || selection?.Math?.RunId || '');
    if (selectedMathId || selectedRunId) {
        for (const block of allBodyBlocks(model)) {
            for (const run of runsOrEmpty(block)) {
                const mathId = String(run?.math?.mathId || run?.math?.MathId || run?.id || '');
                if ((selectedMathId && mathId === selectedMathId) || (selectedRunId && String(run?.id || '') === selectedRunId)) {
                    return run;
                }
            }
        }
    }

    const block = findEditableBlock(model, selection?.focus?.blockId || selection?.anchor?.blockId) || firstEditableBlock(model);
    if (!block) {
        return null;
    }

    const offset = Number(selection?.focus?.offset ?? selection?.anchor?.offset ?? 0) || 0;
    let cursor = 0;
    for (const run of runsOrEmpty(block)) {
        const text = createCanvasRunText(run);
        const end = cursor + text.length;
        if ((String(run?.type || '').toLowerCase() === 'math' || run?.math) && offset >= cursor && offset <= end) {
            return run;
        }

        cursor = end;
    }

    return null;
}

function ensureModelCollections(model) {
    if (!model) {
        return;
    }

    model.body = model.body || { blocks: [] };
    model.body.blocks = Array.isArray(model.body.blocks) ? model.body.blocks : [];
    model.sections = Array.isArray(model.sections) ? model.sections : [];
}

function findEditableBlock(model, blockId) {
    const id = String(blockId || '');
    return allBodyBlocks(model).find(block => String(block?.id || '') === id && Array.isArray(block?.content?.runs)) || null;
}

function firstEditableBlock(model) {
    return allBodyBlocks(model).find(block => Array.isArray(block?.content?.runs)) || null;
}

function allBodyBlocks(model) {
    const stack = Array.isArray(model?.body?.blocks) ? [...model.body.blocks].reverse() : [];
    const result = [];
    while (stack.length > 0) {
        const block = stack.pop();
        if (!block) {
            continue;
        }

        result.push(block);
        const rows = block?.content?.table?.rows;
        if (Array.isArray(rows)) {
            for (let rowIndex = rows.length - 1; rowIndex >= 0; rowIndex -= 1) {
                for (const cell of [...(rows[rowIndex]?.cells || [])].reverse()) {
                    for (const nested of [...(cell?.blocks || [])].reverse()) {
                        stack.push(nested);
                    }
                }
            }
        }
    }

    return result;
}

function runsOrEmpty(block) {
    block.content = block.content || { type: 'paragraph', runs: [] };
    block.content.runs = Array.isArray(block.content.runs) ? block.content.runs : [];
    return block.content.runs;
}

function clampOffset(block, offset) {
    const length = runsOrEmpty(block).reduce((total, run) => total + createCanvasRunText(run).length, 0);
    return Math.max(0, Math.min(length, Number(offset || 0) || 0));
}

function collapsedSelection(blockId, offset) {
    const position = { blockId: String(blockId || ''), offset: Math.max(0, Number(offset || 0) || 0) };
    return { anchor: position, focus: { ...position } };
}

function normalizeDisplayMode(value) {
    if (typeof value === 'number') {
        return value === 1 ? 1 : 0;
    }

    return compact(value) === 'display' ? 1 : 0;
}

function normalizeLimitsPlacement(value) {
    const text = compact(value);
    return text === 'side' || text === 'inline' || text === 'right' ? 'side' : 'aboveBelow';
}

function commandState(enabled, active = false) {
    return { disabled: !enabled, active: !!active, mixed: false, value: null, state: active ? 'active' : 'inactive' };
}

function unchanged(model, selection, operation) {
    return { changed: false, model, selection, operation, dirtyBlockIds: [] };
}

function integer(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? Math.trunc(parsed) : fallback;
}

function createId(prefix) {
    const random = Math.random().toString(36).slice(2, 10);
    const time = Date.now().toString(36);
    return `${prefix}-${time}-${random}`;
}

function compact(value) {
    return String(value == null ? '' : value).replace(/[\s_-]/g, '').toLowerCase();
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
