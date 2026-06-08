import { applyCanvasTextEdit } from './text-editing.mjs';
import { applyAutocorrectAfterTextInput, normalizeAutocorrectOptions } from './autocorrect.mjs';
import { mathToAccessibleText } from '../math/math-model.mjs';

const INSERT_INPUT_TYPES = new Set([
    'insertText',
    'insertReplacementText',
    'insertFromPaste',
    'insertFromDrop',
]);

export function createCanvasInputController(options = {}) {
    const inputBridge = options.inputBridge;
    const selectionController = options.selectionController;
    const getModel = requiredFunction(options.getModel, 'Canvas input controller requires getModel.');
    const commit = requiredFunction(options.commit, 'Canvas input controller requires commit.');
    const afterCommit = typeof options.afterCommit === 'function' ? options.afterCommit : null;
    const autocorrectOptions = normalizeAutocorrectOptions(options.autocorrect || options.autocorrectOptions || {});
    const getPendingMarks = typeof options.getPendingMarks === 'function' ? options.getPendingMarks : () => [];
    const getTrackChangesState = typeof options.getTrackChangesState === 'function'
        ? options.getTrackChangesState
        : () => ({ enabled: false, author: null });
    const executeCommand = typeof options.executeCommand === 'function' ? options.executeCommand : null;
    const now = typeof options.now === 'function'
        ? options.now
        : () => globalThis.performance?.now?.() ?? Date.now();

    let mounted = false;
    let unsubscribeBeforeInput = null;
    let composition = null;
    let revision = 0;
    let lastEdit = null;

    const input = inputBridge?.input || null;
    const onKeyDown = event => handleKeyDown(event);
    const onInput = event => handleInput(event);
    const onCompositionStart = event => handleCompositionStart(event);
    const onCompositionUpdate = event => handleCompositionUpdate(event);
    const onCompositionEnd = event => handleCompositionEnd(event);

    function mount() {
        if (mounted) {
            return api;
        }

        unsubscribeBeforeInput = inputBridge?.subscribe?.((payload, event) => handleBeforeInput(payload, event)) || null;
        input?.addEventListener?.('keydown', onKeyDown);
        input?.addEventListener?.('input', onInput);
        input?.addEventListener?.('compositionstart', onCompositionStart);
        input?.addEventListener?.('compositionupdate', onCompositionUpdate);
        input?.addEventListener?.('compositionend', onCompositionEnd);
        mounted = true;
        return api;
    }

    function destroy() {
        unsubscribeBeforeInput?.();
        unsubscribeBeforeInput = null;
        input?.removeEventListener?.('keydown', onKeyDown);
        input?.removeEventListener?.('input', onInput);
        input?.removeEventListener?.('compositionstart', onCompositionStart);
        input?.removeEventListener?.('compositionupdate', onCompositionUpdate);
        input?.removeEventListener?.('compositionend', onCompositionEnd);
        composition = null;
        mounted = false;
    }

    function handleBeforeInput(payload = {}, event = null) {
        if (payload.isComposing || composition?.active) {
            return false;
        }

        if (isMathSlotActive()) {
            return handleMathBeforeInput(payload, event);
        }

        if (isTextBoxEditActive()) {
            return handleTextBoxBeforeInput(payload, event);
        }

        const inputType = String(payload.inputType || '');
        if (INSERT_INPUT_TYPES.has(inputType)) {
            const text = String(payload.data ?? '');
            if (!text) {
                return false;
            }

            preventAndClear(event);
            return applyEdit({ type: 'insertText', text, source: inputType });
        }

        if (inputType === 'insertLineBreak') {
            preventAndClear(event);
            return applyEdit({ type: 'insertLineBreak', source: inputType });
        }

        if (inputType === 'insertParagraph') {
            preventAndClear(event);
            return applyEdit({ type: 'insertParagraph', source: inputType });
        }

        if (inputType === 'deleteContentBackward') {
            preventAndClear(event);
            return applyEdit({ type: 'deleteBackward', source: inputType });
        }

        if (inputType === 'deleteContentForward') {
            preventAndClear(event);
            return applyEdit({ type: 'deleteForward', source: inputType });
        }

        return false;
    }

    function handleKeyDown(event = {}) {
        if (composition?.active || event.isComposing || event.keyCode === 229 || event.which === 229) {
            return false;
        }

        if (event.defaultPrevented === true) {
            return false;
        }

        if (isMathSlotActive() && handleMathKeyDown(event)) {
            return true;
        }

        if (isTextBoxEditActive() && handleTextBoxKeyDown(event)) {
            return true;
        }

        if (event.key === 'Enter') {
            preventAndClear(event);
            return applyEdit({ type: event.shiftKey ? 'insertLineBreak' : 'insertParagraph', source: 'keydown' });
        }

        if (event.key === 'Tab') {
            const inTable = selectionController?.getState?.()?.table?.inTable === true;
            const handled = executeCommand && ((inTable && executeCommand('navigateTableCell', { direction: event.shiftKey ? 'previous' : 'next', shift: event.shiftKey === true }))
                || executeCommand(event.shiftKey ? 'previousContentControl' : 'nextContentControl', { direction: event.shiftKey ? 'previous' : 'next', shift: event.shiftKey === true, source: 'keyboardTab' })
                || executeCommand(event.shiftKey ? 'decreaseListLevel' : 'increaseListLevel'));
            if (handled) {
                preventAndClear(event);
                return true;
            }

            if (!event.shiftKey) {
                preventAndClear(event);
                return applyEdit({ type: 'insertText', text: '\t', source: 'keydown' });
            }
        }

        if (event.key === 'Backspace') {
            preventAndClear(event);
            return applyEdit({ type: 'deleteBackward', source: 'keydown' });
        }

        if (event.key === 'Delete') {
            preventAndClear(event);
            return applyEdit({ type: 'deleteForward', source: 'keydown' });
        }

        return false;
    }

    function handleInput(event = {}) {
        if (composition?.active || String(event.inputType || '').toLowerCase().includes('composition')) {
            clearInput();
            return false;
        }

        const value = String(input?.value || '');
        if (!value) {
            return false;
        }

        clearInput();
        if (isMathSlotActive()) {
            return executeMathSlotCommand('insertMathSlotText', { text: value, source: 'inputFallback' });
        }

        if (isTextBoxEditActive()) {
            return executeTextBoxCommand('insertTextBoxText', { text: value, source: 'inputFallback' });
        }

        return applyEdit({ type: 'insertText', text: value, source: 'inputFallback' });
    }

    function handleCompositionStart() {
        if (isMathSlotActive()) {
            composition = {
                active: true,
                mathSlot: true,
                baseSelection: selectionController?.getSelection?.() || null,
                text: '',
            };
            return true;
        }

        if (isTextBoxEditActive()) {
            composition = {
                active: true,
                textBox: true,
                baseSelection: selectionController?.getSelection?.() || null,
                text: '',
            };
            return true;
        }

        composition = {
            active: true,
            baseSelection: selectionController?.getSelection?.() || null,
            previewRange: null,
        };
        return true;
    }

    function handleCompositionUpdate(eventOrText = '') {
        const text = typeof eventOrText === 'string'
            ? eventOrText
            : String(eventOrText?.data ?? '');
        if (!composition?.active) {
            handleCompositionStart();
        }

        if (composition?.mathSlot === true) {
            composition.text = text;
            return true;
        }

        if (composition?.textBox === true) {
            composition.text = text;
            return true;
        }

        const targetRange = composition.previewRange || composition.baseSelection;
        if (!targetRange) {
            return false;
        }

        const result = applyEdit({
            type: 'replaceRange',
            range: targetRange,
            text,
            source: 'compositionUpdate',
        }, { compositionPreview: true });
        composition.previewRange = result?.selection
            ? {
                anchor: composition.baseSelection?.anchor || targetRange.anchor,
                focus: result.selection.focus,
            }
            : targetRange;
        selectionController?.setCompositionRange?.(composition.previewRange);
        return result?.changed === true;
    }

    function handleCompositionEnd(eventOrText = '') {
        const text = typeof eventOrText === 'string'
            ? eventOrText
            : String(eventOrText?.data ?? '');
        if (!composition?.active) {
            if (!text) {
                return false;
            }

            if (isMathSlotActive()) {
                return executeMathSlotCommand('insertMathSlotText', { text, source: 'compositionEnd' });
            }

            if (isTextBoxEditActive()) {
                return executeTextBoxCommand('insertTextBoxText', { text, source: 'compositionEnd' });
            }

            return applyEdit({ type: 'insertText', text, source: 'compositionEnd' });
        }

        if (composition?.mathSlot === true) {
            const finalText = text || composition.text || '';
            composition.active = false;
            composition = null;
            clearInput();
            return finalText
                ? executeMathSlotCommand('insertMathSlotText', { text: finalText, source: 'compositionEnd' })
                : false;
        }

        if (composition?.textBox === true) {
            const finalText = text || composition.text || '';
            composition.active = false;
            composition = null;
            clearInput();
            return finalText
                ? executeTextBoxCommand('insertTextBoxText', { text: finalText, source: 'compositionEnd' })
                : false;
        }

        const targetRange = composition.previewRange || composition.baseSelection;
        composition.active = false;
        composition = null;
        selectionController?.setCompositionRange?.(null);
        clearInput();
        if (!targetRange) {
            return false;
        }

        return applyEdit({
            type: 'replaceRange',
            range: targetRange,
            text,
            source: 'compositionEnd',
        })?.changed === true;
    }

    function handleMathBeforeInput(payload = {}, event = null) {
        const inputType = String(payload.inputType || '');
        if (INSERT_INPUT_TYPES.has(inputType)) {
            const text = String(payload.data ?? '');
            if (!text) {
                return false;
            }

            preventAndClear(event);
            const handled = executeMathSlotCommand('insertMathSlotText', { text, source: inputType });
            if (handled && text === ' ') {
                tryFinalizeLinearMathSlot();
            }

            return handled;
        }

        if (inputType === 'insertLineBreak' || inputType === 'insertParagraph') {
            preventAndClear(event);
            return addMathMatrixRowFromActiveSlot() || true;
        }

        if (inputType === 'deleteContentBackward') {
            preventAndClear(event);
            return executeMathSlotCommand('deleteMathSlotBackward', { source: inputType });
        }

        if (inputType === 'deleteContentForward') {
            preventAndClear(event);
            return executeMathSlotCommand('deleteMathSlotForward', { source: inputType });
        }

        return false;
    }

    function handleMathKeyDown(event = {}) {
        if (event.key === 'Enter') {
            preventAndClear(event);
            return addMathMatrixRowFromActiveSlot() || true;
        }

        if (event.key === 'Tab') {
            preventAndClear(event);
            return executeMathSlotCommand('moveMathSlot', {
                direction: event.shiftKey === true ? 'previous' : 'next',
                source: 'keyboardTab',
            });
        }

        if (event.key === 'Backspace') {
            preventAndClear(event);
            return executeMathSlotCommand('deleteMathSlotBackward', { source: 'keydown' });
        }

        if (event.key === 'Delete') {
            preventAndClear(event);
            return executeMathSlotCommand('deleteMathSlotForward', { source: 'keydown' });
        }

        return false;
    }

    function handleTextBoxBeforeInput(payload = {}, event = null) {
        const inputType = String(payload.inputType || '');
        if (INSERT_INPUT_TYPES.has(inputType)) {
            const text = String(payload.data ?? '');
            if (!text) {
                return false;
            }

            preventAndClear(event);
            return executeTextBoxCommand('insertTextBoxText', { text, source: inputType });
        }

        if (inputType === 'insertLineBreak' || inputType === 'insertParagraph') {
            preventAndClear(event);
            return executeTextBoxCommand('insertTextBoxParagraph', { source: inputType });
        }

        if (inputType === 'deleteContentBackward') {
            preventAndClear(event);
            return executeTextBoxCommand('deleteTextBoxTextBackward', { source: inputType });
        }

        if (inputType === 'deleteContentForward') {
            preventAndClear(event);
            return executeTextBoxCommand('deleteTextBoxTextForward', { source: inputType });
        }

        return false;
    }

    function handleTextBoxKeyDown(event = {}) {
        if (event.key === 'Enter') {
            preventAndClear(event);
            return executeTextBoxCommand('insertTextBoxParagraph', { source: 'keydown' });
        }

        if (event.key === 'Tab' && !event.shiftKey) {
            preventAndClear(event);
            return executeTextBoxCommand('insertTextBoxText', { text: '\t', source: 'keydown' });
        }

        if (event.key === 'Backspace') {
            preventAndClear(event);
            return executeTextBoxCommand('deleteTextBoxTextBackward', { source: 'keydown' });
        }

        if (event.key === 'Delete') {
            preventAndClear(event);
            return executeTextBoxCommand('deleteTextBoxTextForward', { source: 'keydown' });
        }

        return false;
    }

    function isMathSlotActive() {
        return !!(selectionController?.getSelection?.()?.math || selectionController?.getSelection?.()?.Math);
    }

    function executeMathSlotCommand(commandId, payload = {}) {
        return executeCommand?.(commandId, payload) === true;
    }

    function isTextBoxEditActive() {
        const object = selectionController?.getSelection?.()?.object || null;
        const textBox = object?.textBox || object?.TextBox || null;
        return textBox?.active === true || textBox?.Active === true;
    }

    function executeTextBoxCommand(commandId, payload = {}) {
        const object = selectionController?.getSelection?.()?.object || {};
        return executeCommand?.(commandId, {
            objectId: object.objectId || object.ObjectId || '',
            blockId: object.blockId || object.BlockId || '',
            runId: object.runId || object.RunId || '',
            ...payload,
        }) === true;
    }

    function addMathMatrixRowFromActiveSlot() {
        const selection = selectionController?.getSelection?.() || null;
        const matrixPath = matrixPathFromSlotPath(selection?.math?.slotPath || selection?.Math?.SlotPath || []);
        if (!matrixPath) {
            return false;
        }

        const rowIndex = matrixRowIndex(selection?.math?.slotPath || selection?.Math?.SlotPath || []);
        return executeMathSlotCommand('addMathMatrixRow', {
            matrixPath,
            afterRowIndex: rowIndex,
            source: 'keyboardEnter',
        });
    }

    function tryFinalizeLinearMathSlot() {
        const selection = selectionController?.getSelection?.() || null;
        const text = activeMathSlotText(getModel(), selection).trim();
        if (!shouldFinalizeLinearMath(text)) {
            return false;
        }

        return executeMathSlotCommand('insertMathSlotText', {
            linear: text,
            replace: true,
            source: 'linearInput',
        });
    }

    function activeMathSlotText(model, selection) {
        const math = selection?.math || selection?.Math || null;
        if (!math) {
            return '';
        }

        const run = findMathRun(model, math);
        const content = getAtPath(run?.math?.content || run?.Math?.Content, math.slotPath || math.SlotPath || []);
        return content ? mathToAccessibleText(content) : '';
    }

    function findMathRun(model, math) {
        const mathId = String(math?.mathId || math?.MathId || '');
        const runId = String(math?.runId || math?.RunId || '');
        const stack = Array.isArray(model?.body?.blocks) ? [...model.body.blocks] : [];
        while (stack.length > 0) {
            const block = stack.shift();
            const run = (block?.content?.runs || []).find(item =>
                (runId && String(item?.id || '') === runId)
                || (mathId && String(item?.math?.mathId || item?.Math?.MathId || '') === mathId));
            if (run) {
                return run;
            }

            for (const row of block?.content?.table?.rows || []) {
                for (const cell of row?.cells || []) {
                    stack.push(...(cell?.blocks || []));
                }
            }
        }

        return null;
    }

    function getAtPath(root, pathValue) {
        return normalizePath(pathValue).reduce((current, segment) => current?.[segment], root);
    }

    function shouldFinalizeLinearMath(text) {
        const value = String(text || '').trim();
        return /^\\[a-z]+$/i.test(value)
            || /^[^/\s]+\/[^/\s]+$/.test(value)
            || /^[^_\s]+_[^_\s]+$/.test(value)
            || /^[^^\s]+\^[^^\s]+$/.test(value)
            || /^sqrt\(.+\)$/i.test(value);
    }

    function matrixPathFromSlotPath(slotPath) {
        const path = normalizePath(slotPath);
        const rowsIndex = path.findIndex(segment => segment === 'rows');
        return rowsIndex > 0 ? path.slice(0, rowsIndex) : null;
    }

    function matrixRowIndex(slotPath) {
        const path = normalizePath(slotPath);
        const rowsIndex = path.findIndex(segment => segment === 'rows');
        return rowsIndex >= 0 ? Math.max(0, Number(path[rowsIndex + 1] || 0) || 0) : 0;
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

    function applyEdit(edit, flags = {}) {
        const startedAt = now();
        const effectiveEdit = withPendingMarks(edit);
        const beforeModel = getModel();
        const beforeSelection = selectionController?.getSelection?.() || null;
        const result = applyCanvasTextEdit(beforeModel, beforeSelection, effectiveEdit);
        if (!result.changed) {
            return result;
        }

        result.model = invalidateHeadingCachesForTextEdit(beforeModel, result.model, result);
        applyAutocorrect(beforeModel, beforeSelection, effectiveEdit, result);
        revision += 1;
        lastEdit = {
            revision,
            operation: result.operation || edit.type,
            source: edit.source || 'input',
            dirtyBlockIds: result.dirtyBlockIds || [],
            removedBlockIds: result.removedBlockIds || [],
            compositionPreview: flags.compositionPreview === true,
            durationMs: 0,
        };

        const commitResult = commit({
            before: {
                model: clone(result.undoBeforeModel || beforeModel),
                selection: clone(result.undoBeforeSelection || beforeSelection),
            },
            model: result.model,
            selection: result.selection,
            edit: effectiveEdit,
            result,
            input: lastEdit,
        });
        lastEdit.durationMs = Math.max(0, now() - startedAt);
        afterCommit?.({ ...lastEdit }, commitResult);
        clearInput();
        return {
            ...result,
            commitResult,
            input: lastEdit,
        };
    }

    function applyAutocorrect(beforeModel, beforeSelection, effectiveEdit, result) {
        if (effectiveEdit?.type !== 'insertText') {
            return;
        }

        const corrected = applyAutocorrectAfterTextInput({
            beforeModel,
            beforeSelection,
            model: result.model,
            selection: result.selection,
            edit: effectiveEdit,
            result,
            options: autocorrectOptions,
        });
        if (corrected.changed !== true) {
            return;
        }

        const autocorrectResult = {
            ...result,
            dirtyBlockIds: unique([
                ...(result.dirtyBlockIds || []),
                ...(corrected.dirtyBlockIds || []),
            ]),
            removedBlockIds: result.removedBlockIds || [],
            insertedBlockId: result.insertedBlockId || null,
        };
        result.model = invalidateHeadingCachesForTextEdit(result.model, corrected.model, autocorrectResult);
        result.selection = corrected.selection || result.selection;
        result.operation = corrected.operation || result.operation;
        result.dirtyBlockIds = autocorrectResult.dirtyBlockIds;
        result.autoCorrect = true;
        result.autoformat = (corrected.operations || []).some(operation => /^auto/i.test(operation));
        result.autocorrect = { operations: corrected.operations || [] };
        result.undoBeforeModel = corrected.undoBeforeModel;
        result.undoBeforeSelection = corrected.undoBeforeSelection;
    }

    function withPendingMarks(edit) {
        const trackChanges = getTrackChangesState() || {};
        const next = {
            ...edit,
            trackChanges: trackChanges.enabled === true,
            author: trackChanges.author || null,
        };
        if (edit?.type !== 'insertText' && edit?.type !== 'insertLineBreak') {
            return next;
        }

        const pendingMarks = getPendingMarks();
        return Array.isArray(pendingMarks) && pendingMarks.length > 0
            ? { ...next, marks: pendingMarks }
            : next;
    }

    function getState() {
        return {
            mounted,
            revision,
            isComposing: composition?.active === true,
            compositionRange: composition?.previewRange || null,
            lastEdit,
        };
    }

    const api = {
        mount,
        destroy,
        handleBeforeInput,
        handleKeyDown,
        handleInput,
        handleCompositionStart,
        handleCompositionUpdate,
        handleCompositionEnd,
        getState,
    };

    return api;

    function preventAndClear(event) {
        event?.preventDefault?.();
        clearEventTarget(event);
        clearInput();
    }

    function clearInput() {
        if (input && 'value' in input) {
            input.value = '';
        }
    }
}

function invalidateHeadingCachesForTextEdit(beforeModel, afterModel, result) {
    const beforeBlocks = blocksById(beforeModel);
    const afterBlocks = blocksById(afterModel);
    const affectedIds = new Set([
        ...(result?.dirtyBlockIds || []),
        ...(result?.removedBlockIds || []),
        result?.insertedBlockId,
    ].map(value => String(value || '')).filter(Boolean));

    let invalidates = false;
    for (const blockId of affectedIds) {
        const before = beforeBlocks.get(blockId);
        const after = afterBlocks.get(blockId);
        if (isHeadingBlock(before) && !after) {
            invalidates = true;
            break;
        }

        if ((isHeadingBlock(before) || isHeadingBlock(after)) && blockText(before) !== blockText(after)) {
            invalidates = true;
            break;
        }
    }

    if (!invalidates) {
        return afterModel;
    }

    return {
        ...afterModel,
        outlineRevision: Math.max(0, Number(afterModel?.outlineRevision || 0) || 0) + 1,
        tableOfContentsRevision: Math.max(0, Number(afterModel?.tableOfContentsRevision || 0) || 0) + 1,
    };
}

function blocksById(model) {
    return new Map((Array.isArray(model?.body?.blocks) ? model.body.blocks : [])
        .map(block => [String(block?.id || ''), block]));
}

function isHeadingBlock(block) {
    return String(block?.type || block?.content?.type || '').toLowerCase() === 'heading';
}

function blockText(block) {
    return (Array.isArray(block?.content?.runs) ? block.content.runs : [])
        .map(run => String(run?.text || ''))
        .join('');
}

function clearEventTarget(event) {
    if (event?.target && 'value' in event.target) {
        event.target.value = '';
    }
}

function requiredFunction(value, message) {
    if (typeof value !== 'function') {
        throw new Error(message);
    }

    return value;
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}

function unique(values) {
    return [...new Set(values.map(value => String(value || '')).filter(Boolean))];
}
