// Phase D — history/command-dispatcher.mjs
// `createCommandDispatcherFactory({ findBlock, buildIndexes, createOperation,
//   applyOperation, collectFormattingState, createTableController })` →
//   `createCommandDispatcher(model, options)` → the toolbar command engine. Owns the
//   active selection + pending typing marks, exposes inline/paragraph/table commands,
//   publishes formatting state to subscribers, and records committed operations.
//
// Pure helpers (marks, selection snapshot, command classifiers, run mutators) are
// imported directly; engine-state deps are injected.

import { asArray, clone, sortObject } from '../core/helpers.mjs';
import { normalizeTextRunForMerge } from '../core/inline-runs.mjs';
import { normalizeMarks } from '../core/marks.mjs';
import {
    createSelectionSnapshot,
    createLogicalPosition,
} from '../core/selection-snapshot.mjs';
import { createSelectionTextRange } from '../core/selection-range.mjs';
import { normalizeCommandId } from '../input/command-id.mjs';
import {
    commandSource,
    markMatchesCommand,
    paragraphCommandTypes,
} from '../input/command-classifiers.mjs';
import { commandMark, isClearValueCommand } from '../input/command-marks.mjs';
import { splitRunsForRange } from '../core/run-mutators.mjs';
import { normalizeParagraphAlignment } from '../layout/paragraph-alignment.mjs';
import { transformRunsInRange } from './revision-helpers.mjs';
import { OperationTypes } from './operation-types.mjs';

const INLINE_COMMAND_IDS = [
    'bold', 'italic', 'underline', 'strike', 'fontFamily', 'fontSize',
    'textColor', 'backgroundColor', 'link', 'clearFormatting',
];
const TABLE_COMMAND_IDS = [
    'insertTable', 'insertRowAbove', 'insertRowBelow', 'insertColumnLeft',
    'insertColumnRight', 'deleteRow', 'deleteColumn', 'mergeCells', 'splitCell',
    'cellBackground', 'cellBorder', 'resizeTable',
];

export function createCommandDispatcherFactory(options) {
    const opts = options || {};
    for (const key of ['findBlock', 'buildIndexes', 'createOperation', 'applyOperation',
        'collectFormattingState', 'createTableController']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createCommandDispatcherFactory requires options.${key} (function)`);
        }
    }
    const {
        findBlock, buildIndexes, createOperation, applyOperation,
        collectFormattingState, createTableController,
    } = opts;
    const { selectionTextRange } = createSelectionTextRange({ createSelectionSnapshot, createLogicalPosition });

    function findTableBlock(model, tableId) {
        const block = findBlock(model, tableId);
        return block && block.type === 'table' ? block : null;
    }

    function removeMarksForCommandInRange(block, range, commandId) {
        if (!block || block.type !== 'paragraph') return;
        transformRunsInRange(block, range.start, range.end, function (run) {
            run.marks = normalizeMarks(asArray(run.marks).filter(function (mark) { return !markMatchesCommand(mark, commandId); }));
            return run;
        });
    }

    function clearFormattingInRange(block, range) {
        if (!block || block.type !== 'paragraph') return;
        transformRunsInRange(block, range.start, range.end, function (run) {
            run.marks = [];
            run.style = {};
            return normalizeTextRunForMerge(run);
        });
    }

    return function createCommandDispatcher(model, dispatcherOptions) {
        const dopts = dispatcherOptions || {};
        let selection = createSelectionSnapshot(dopts.selection || dopts.Selection || {});
        let pendingTypingMarks = normalizeMarks(dopts.pendingTypingMarks || dopts.PendingTypingMarks || []);
        const debugLog = [];
        const committedOperations = [];
        let subscribers = [];
        let lastSnapshot = collectFormattingState(model, selection, pendingTypingMarks);

        function publish(snapshot) {
            lastSnapshot = snapshot || collectFormattingState(model, selection, pendingTypingMarks);
            subscribers.forEach(function (callback) {
                try { callback(clone(lastSnapshot)); }
                catch (error) {
                    debugLog.push({ code: 'subscriber-failed', message: String((error && error.message) || error), at: Date.now() });
                }
            });
            return lastSnapshot;
        }

        function refresh(nextSelection) {
            if (nextSelection) selection = createSelectionSnapshot(nextSelection);
            return publish(collectFormattingState(model, selection, pendingTypingMarks));
        }

        function getState(commandId) {
            const id = normalizeCommandId(commandId);
            const snapshot = refresh();
            const reason = snapshot.disabledReasons[id] || '';
            return sortObject({
                id,
                isEnabled: !reason && !!commands[id],
                value: snapshot.commandValues[id] ?? null,
                disabledReason: reason,
                refresh: true,
            });
        }

        function applyInlineCommand(id, payload) {
            const range = selectionTextRange(selection);
            const block = findBlock(model, range.blockId);
            if (!block || block.type !== 'paragraph') return { ok: false, errors: [{ code: 'selection-not-text' }] };
            const effectiveRange = range.collapsed ? { blockId: range.blockId, start: range.start, end: range.start, collapsed: true } : range;
            if (effectiveRange.collapsed) {
                const mark = commandMark(id, payload);
                if (isClearValueCommand(id, mark)) {
                    pendingTypingMarks = normalizeMarks(pendingTypingMarks.filter(function (item) { return !markMatchesCommand(item, id); }));
                    return { ok: true, operation: null, nextSelection: selection, pendingTyping: true };
                }
                if (mark) pendingTypingMarks = normalizeMarks(pendingTypingMarks.filter(function (item) { return !markMatchesCommand(item, id); }).concat([mark]));
                return { ok: true, operation: null, nextSelection: selection, pendingTyping: true };
            }
            if (id === 'clearFormatting') {
                clearFormattingInRange(block, effectiveRange);
                const removeOp = createOperation(OperationTypes.RemoveMark, { range: effectiveRange, mark: { type: 'AllFormatting' } }, { source: 'command' });
                committedOperations.push(removeOp.toJSON());
                buildIndexes(model);
                return { ok: true, operation: removeOp, nextSelection: selection };
            }
            const beforeSnapshot = collectFormattingState(model, selection, pendingTypingMarks);
            let isActive = beforeSnapshot.commandValues[id] === true;
            if (id === 'textColor' || id === 'backgroundColor' || id === 'link') isActive = false;
            const mark = commandMark(id, payload);
            if (isClearValueCommand(id, mark)) {
                removeMarksForCommandInRange(block, effectiveRange, id);
                const clearOp = createOperation(OperationTypes.RemoveMark, { range: effectiveRange, mark }, { source: 'command' });
                committedOperations.push(clearOp.toJSON());
                buildIndexes(model);
                return { ok: true, operation: clearOp, nextSelection: selection };
            }
            if ((id === 'fontFamily' || id === 'fontSize' || id === 'textColor' || id === 'backgroundColor')
                && mark && beforeSnapshot.commandValues[id] === mark.value) {
                return { ok: true, operation: null, nextSelection: selection, noop: true };
            }
            removeMarksForCommandInRange(block, effectiveRange, id);
            const opType = isActive ? OperationTypes.RemoveMark : OperationTypes.ApplyMark;
            const op = createOperation(opType, { range: effectiveRange, mark }, { source: 'command' });
            if (!isActive) splitRunsForRange(block, effectiveRange.start, effectiveRange.end, mark, false);
            buildIndexes(model);
            committedOperations.push(op.toJSON());
            return { ok: true, operation: op, nextSelection: selection };
        }

        function applyParagraphCommand(id, payload) {
            const body = payload || {};
            const snapshot = createSelectionSnapshot(selection);
            const block = findBlock(model, snapshot.blockId);
            if (!block || block.type !== 'paragraph') return { ok: false, errors: [{ code: 'selection-not-paragraph' }] };
            const values = [];
            if (id === 'alignment') values.push(['alignment', normalizeParagraphAlignment(body.value ?? body.Value ?? body.alignment ?? body.Alignment ?? 'left')]);
            if (id === 'lineSpacing') values.push(['lineSpacing', Number(body.value ?? body.Value ?? 1)]);
            if (id === 'spacingBefore') values.push(['spacingBefore', Number(body.value ?? body.Value ?? 0)]);
            if (id === 'spacingAfter') values.push(['spacingAfter', Number(body.value ?? body.Value ?? 0)]);
            if (id === 'list') values.push(['listType', body.value || body.Value || body.listType || body.ListType || null]);
            if (id === 'indent') values.push(['indentLevel', Math.max(0, Number(block.content && block.content.indentLevel || 0) + Number(body.delta ?? body.Delta ?? 1))]);
            if (id === 'outdent') values.push(['indentLevel', Math.max(0, Number(block.content && block.content.indentLevel || 0) - Number(body.delta ?? body.Delta ?? 1))]);
            const ops = values.map(function (entry) {
                const op = createOperation(OperationTypes.SetParagraphAttribute, {
                    target: { blockId: block.id, offset: snapshot.offset },
                    attributeName: entry[0],
                    value: entry[1],
                }, { source: 'command' });
                applyOperation(model, op, { selection: snapshot });
                committedOperations.push(op.toJSON());
                return op;
            });
            selection = createSelectionSnapshot(snapshot);
            return { ok: true, operations: ops, nextSelection: selection };
        }

        function applyTableCommand(id, payload) {
            const body = payload || {};
            const snapshot = createSelectionSnapshot(selection);
            const controller = createTableController(model);
            let result;
            if (id === 'insertTable') {
                const op = createOperation(OperationTypes.InsertTable, {
                    target: { blockId: snapshot.blockId, offset: snapshot.offset },
                    rows: Number(body.rows || body.Rows || 2),
                    columns: Number(body.columns || body.Columns || 2),
                    tableId: body.tableId || body.TableId || body.blockId || body.BlockId || null,
                    style: body.style || body.Style || {},
                }, { source: 'command' });
                const applied = applyOperation(model, op, { selection: snapshot });
                committedOperations.push(op.toJSON());
                const table = findTableBlock(model, applied.insertedBlockId || body.tableId || body.TableId || '');
                const firstCell = table && table.content.rows[0] && table.content.rows[0].cells[0];
                result = {
                    ok: applied.ok !== false,
                    operation: op,
                    nextSelection: firstCell ? createSelectionSnapshot({ blockId: firstCell.blocks[0].id, cellId: firstCell.id, tableId: table.id, offset: 0, isCollapsed: true }) : snapshot,
                };
            } else if (id === 'insertRowAbove') result = controller.insertRowAbove(snapshot);
            else if (id === 'insertRowBelow') result = controller.insertRowBelow(snapshot);
            else if (id === 'insertColumnLeft') result = controller.insertColumnLeft(snapshot);
            else if (id === 'insertColumnRight') result = controller.insertColumnRight(snapshot);
            else if (id === 'deleteRow') result = controller.deleteRow(snapshot, body.rowIndex ?? body.RowIndex);
            else if (id === 'deleteColumn') result = controller.deleteColumn(snapshot, body.columnIndex ?? body.ColumnIndex);
            else if (id === 'mergeCells') result = controller.mergeCells(snapshot, body.cellIds || body.CellIds || []);
            else if (id === 'splitCell') result = controller.splitCell(snapshot, body.cellId || body.CellId || null);
            else if (id === 'cellBackground') result = controller.setCellBackground(snapshot, body.color || body.Color || body.value || body.Value || null);
            else if (id === 'cellBorder') result = controller.setCellBorder(snapshot, body.border || body.Border || body.value || body.Value || null);
            else if (id === 'resizeTable') result = controller.resizeTable(snapshot.tableId || snapshot.blockId || body.tableId || body.TableId, body.width || body.Width);
            else result = { ok: false, errors: [{ code: 'unknown-table-command', commandId: id }] };
            controller.getCommittedOperations().forEach(function (operation) { committedOperations.push(operation); });
            return { ok: result.ok !== false, operation: result.operation || null, nextSelection: result.selection || result.nextSelection || snapshot };
        }

        const commands = {};
        INLINE_COMMAND_IDS.forEach(function (id) {
            commands[id] = {
                id,
                refresh: function () { return getState(id); },
                execute: function (payload) { return applyInlineCommand(id, payload); },
            };
        });
        paragraphCommandTypes().forEach(function (id) {
            commands[id] = {
                id,
                refresh: function () { return getState(id); },
                execute: function (payload) { return applyParagraphCommand(id, payload); },
            };
        });
        TABLE_COMMAND_IDS.forEach(function (id) {
            commands[id] = {
                id,
                refresh: function () { return getState(id); },
                execute: function (payload) { return applyTableCommand(id, payload); },
            };
        });

        function executeCommand(commandInput, payload) {
            const id = normalizeCommandId(commandInput);
            const source = commandSource(commandInput);
            if (!commands[id]) {
                const failure = { code: 'unknown-command', commandId: id, source, at: Date.now() };
                debugLog.push(failure);
                return sortObject({ ok: false, error: failure, source, commandId: id });
            }
            const state = getState(id);
            if (!state.isEnabled) {
                const disabled = { code: 'command-disabled', commandId: id, source, reason: state.disabledReason, at: Date.now() };
                debugLog.push(disabled);
                return sortObject({ ok: false, error: disabled, source, commandId: id });
            }
            const beforeSelection = createSelectionSnapshot(selection);
            const result = commands[id].execute(payload || {});
            const transaction = {
                ok: result.ok !== false,
                id: 'cmd-txn-' + Date.now() + '-' + Math.floor(Math.random() * 100000),
                commandId: id,
                beforeSelection,
                afterSelection: createSelectionSnapshot(result.nextSelection || selection),
                operationCount: asArray(result.operations).length + (result.operation ? 1 : 0),
            };
            selection = transaction.afterSelection;
            refresh(selection);
            return sortObject({
                ok: result.ok !== false,
                commandId: id,
                source,
                transaction,
                usedRuntimeSelection: true,
                readDomSelection: false,
                mutatedDomDirectly: false,
                state: getState(id),
            });
        }

        refresh(selection);

        return {
            normalizeCommandId,
            getRegisteredCommandIds: function () { return Object.keys(commands).sort(); },
            getCommand: function (id) { return commands[normalizeCommandId(id)] || null; },
            getState,
            refresh,
            executeCommand,
            setSelection: function (nextSelection) { selection = createSelectionSnapshot(nextSelection || {}); return refresh(selection); },
            getSelection: function () { return createSelectionSnapshot(selection); },
            getPendingTypingMarks: function () { return pendingTypingMarks.map(clone); },
            getFormattingSnapshot: function () { return refresh(selection); },
            subscribeFormattingState: function (callback) {
                if (typeof callback === 'function') subscribers.push(callback);
                if (callback) callback(clone(lastSnapshot));
                return function () { subscribers = subscribers.filter(function (item) { return item !== callback; }); };
            },
            getBlazorToolbarState: function () {
                const snapshot = refresh(selection);
                return sortObject({ ribbon: clone(snapshot), floating: clone(snapshot), sidePanel: clone(snapshot) });
            },
            getCommittedOperations: function () { return committedOperations.slice(); },
            getDebugLog: function () { return debugLog.slice(); },
        };
    };
}
