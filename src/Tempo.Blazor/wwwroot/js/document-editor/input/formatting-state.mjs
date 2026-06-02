// Phase D — input/formatting-state.mjs
// Read-only derivation of the toolbar formatting state from a model + selection.
//
// `createFormattingStateModule({findBlock, buildIndexes, validateStableSelectionToken?})`
//   returns:
//   - `selectionDisabledReason(model, selection, commandId)` — '' when the command is
//     enabled for the selection, otherwise a reason code (selection-not-text, etc.).
//   - `collectFormattingState(model, selection, pendingTypingMarks)` — the raw inline/
//     paragraph/image/table state with active/mixed maps, commandValues and
//     disabledReasons.
//   - `resolveFormattingSelection(model, selectionOrToken, inst)` — resolves a raw
//     selection or a serialized selection token into a selection snapshot.
//   - `computeFormattingState(model, selectionOrToken, pendingTypingMarks, inst)` — the
//     flattened scalar state consumed by the runtime.
//   - `formattingScalarValue(formatting, commandId, fallback)` helper.
//
// (The Pascal/numeric-cased C# DTO shape lives in core/blazor-formatting-state.mjs.)
//
// All pure sub-helpers are imported directly; only the index-based `findBlock`,
// `buildIndexes`, and (optionally) the instance-bound `validateStableSelectionToken`
// are injected so the module stays free of engine/instance state.

import { asArray, clone, sortObject } from '../core/helpers.mjs';
import { markValue } from '../core/marks.mjs';
import { createSelectionSnapshot, createLogicalPosition } from '../core/selection-snapshot.mjs';
import { createSelectionTextRange } from '../core/selection-range.mjs';
import { runsForRange } from '../core/runs-for-range.mjs';
import {
    inlineCommandTypes,
    paragraphCommandTypes,
    markMatchesCommand,
} from './command-classifiers.mjs';
import { pendingMarkForCommand } from './pending-marks.mjs';
import { findInheritedTextColor } from '../core/inherited-style.mjs';
import { parseSelectionTokenData, readSelectionTokenData } from '../core/selection-token.mjs';
import { firstModelSelection } from '../core/first-block.mjs';
import { findTableInfoByBlockId } from '../core/model-finders.mjs';

const TABLE_CELL_COMMANDS = [
    'insertRowAbove', 'insertRowBelow', 'insertColumnLeft', 'insertColumnRight',
    'deleteRow', 'deleteColumn', 'mergeCells', 'splitCell', 'cellBackground', 'cellBorder',
];
const INLINE_AND_PARAGRAPH_IDS = [
    'bold', 'italic', 'underline', 'strike', 'fontFamily', 'fontSize',
    'textColor', 'backgroundColor', 'link',
];
const DISABLED_REASON_IDS = [
    'bold', 'italic', 'underline', 'strike', 'fontFamily', 'fontSize', 'textColor',
    'backgroundColor', 'link', 'clearFormatting', 'alignment', 'lineSpacing',
    'spacingBefore', 'spacingAfter', 'list', 'indent', 'outdent',
];

export function createFormattingStateModule(options) {
    const opts = options || {};
    if (typeof opts.findBlock !== 'function') {
        throw new TypeError('createFormattingStateModule requires options.findBlock (function)');
    }
    if (typeof opts.buildIndexes !== 'function') {
        throw new TypeError('createFormattingStateModule requires options.buildIndexes (function)');
    }
    const { findBlock, buildIndexes } = opts;
    const validateStableSelectionToken = typeof opts.validateStableSelectionToken === 'function'
        ? opts.validateStableSelectionToken
        : null;

    const { selectionTextRange } = createSelectionTextRange({
        createSelectionSnapshot, createLogicalPosition,
    });

    function selectionDisabledReason(model, selection, commandId) {
        const snapshot = createSelectionSnapshot(selection || {});
        const block = findBlock(model, snapshot.blockId);
        if (!block) return 'missing-selection';
        if (inlineCommandTypes().indexOf(commandId) >= 0 || commandId === 'clearFormatting') {
            return block.type === 'paragraph' ? '' : 'selection-not-text';
        }
        if (paragraphCommandTypes().indexOf(commandId) >= 0) {
            return block.type === 'paragraph' ? '' : 'selection-not-paragraph';
        }
        if (TABLE_CELL_COMMANDS.indexOf(commandId) >= 0) {
            return (selection && selection.cellId) || findTableInfoByBlockId(model, selection && selection.blockId)
                ? '' : 'selection-not-table-cell';
        }
        if (commandId === 'insertTable') return block.type === 'paragraph' ? '' : 'selection-not-paragraph';
        if (commandId === 'resizeTable') return block.type === 'table' || (selection && selection.tableId) ? '' : 'selection-not-table';
        return '';
    }

    function collectFormattingState(model, selection, pendingTypingMarks) {
        buildIndexes(model);
        const snapshot = createSelectionSnapshot(selection || {});
        const range = selectionTextRange(snapshot);
        const block = findBlock(model, snapshot.blockId);
        const runs = runsForRange(block, range);
        const active = {
            bold: false, italic: false, underline: false, strike: false,
            fontFamily: null, fontSize: null, textColor: null, backgroundColor: null, link: null,
        };
        const mixed = {
            bold: false, italic: false, underline: false, strike: false,
            fontFamily: false, fontSize: false, textColor: false, backgroundColor: false, link: false,
        };
        function valuesFor(id) {
            return runs.map(function (run) {
                const found = asArray(run.marks).find(function (mark) { return markMatchesCommand(mark, id); });
                if (id === 'fontFamily') return found ? markValue(found) : (run.style && (run.style.fontFamily || run.style.FontFamily) || null);
                if (id === 'fontSize') return found ? markValue(found) : (run.style && (run.style.fontSize || run.style.FontSize) || null);
                if (id === 'textColor') return found ? markValue(found) : (run.style && (run.style.color || run.style.Color) || null);
                if (id === 'backgroundColor') return found ? markValue(found) : (run.style && (run.style.backgroundColor || run.style.BackgroundColor) || null);
                if (id === 'link') return found ? markValue(found) : null;
                return !!found;
            });
        }
        INLINE_AND_PARAGRAPH_IDS.forEach(function (id) {
            let values = valuesFor(id);
            if (values.length === 0 && id === 'textColor' && block) values = [findInheritedTextColor(block, snapshot.offset)];
            if (id === 'textColor' && values.every(function (value) { return value === null || value === undefined || value === ''; }) && block) {
                values = [findInheritedTextColor(block, snapshot.offset)];
            }
            const first = values.length ? values[0] : (id === 'bold' || id === 'italic' || id === 'underline' || id === 'strike' ? false : null);
            active[id] = first === undefined ? null : first;
            mixed[id] = values.some(function (value) { return JSON.stringify(value) !== JSON.stringify(first); });
        });
        if (range.collapsed) {
            INLINE_AND_PARAGRAPH_IDS.forEach(function (id) {
                const pending = pendingMarkForCommand(pendingTypingMarks, id);
                if (!pending) return;
                active[id] = id === 'bold' || id === 'italic' || id === 'underline' || id === 'strike'
                    ? true
                    : markValue(pending);
                mixed[id] = false;
            });
        }
        const paragraph = block && block.type === 'paragraph' ? {
            alignment: block.content && block.content.alignment || 'left',
            lineSpacing: block.content && block.content.lineSpacing || 1,
            spacingBefore: block.content && (block.content.spacingBefore ?? block.content.SpacingBefore) || 0,
            spacingAfter: block.content && (block.content.spacingAfter ?? block.content.SpacingAfter) || 0,
            listType: block.content && (block.content.listType || block.content.ListType) || null,
            indentLevel: block.content && Number(block.content.indentLevel || block.content.IndentLevel || 0),
        } : {};
        const image = block && block.type === 'image' ? {
            isSelected: snapshot.isObjectSelection === true || !!snapshot.objectId,
            blockId: block.id,
            objectId: block.content && block.content.objectId || block.id,
            layout: clone(block.content && block.content.layout || {}),
        } : { isSelected: false };
        const table = block && block.type === 'table' ? {
            isSelected: snapshot.isObjectSelection === true || !!snapshot.objectId,
            blockId: block.id,
        } : { isSelected: false };
        const commandValues = {
            bold: active.bold === true && mixed.bold !== true,
            italic: active.italic === true && mixed.italic !== true,
            underline: active.underline === true && mixed.underline !== true,
            strike: active.strike === true && mixed.strike !== true,
            fontFamily: mixed.fontFamily ? null : active.fontFamily,
            fontSize: mixed.fontSize ? null : active.fontSize,
            textColor: mixed.textColor ? null : active.textColor,
            backgroundColor: mixed.backgroundColor ? null : active.backgroundColor,
            link: mixed.link ? null : active.link,
            alignment: paragraph.alignment || null,
            lineSpacing: paragraph.lineSpacing || null,
            spacingBefore: paragraph.spacingBefore || 0,
            spacingAfter: paragraph.spacingAfter || 0,
            list: paragraph.listType || null,
            indent: paragraph.indentLevel || 0,
        };
        const disabledReasons = {};
        DISABLED_REASON_IDS.forEach(function (id) {
            const reason = selectionDisabledReason(model, snapshot, id);
            if (reason) disabledReasons[id] = reason;
        });
        return sortObject({
            selection: snapshot,
            inline: { active, mixed },
            paragraph,
            image,
            table,
            pendingTypingMarks: clone(pendingTypingMarks || []),
            commandValues,
            disabledReasons,
            fromRevisionDecoration: false,
        });
    }

    function resolveFormattingSelection(model, selectionOrToken, inst) {
        if (validateStableSelectionToken && inst && selectionOrToken) {
            const validation = validateStableSelectionToken(inst, selectionOrToken);
            if (validation && validation.ok === true && validation.selection) {
                return validation.selection;
            }
        }

        const tokenData = parseSelectionTokenData(selectionOrToken) || readSelectionTokenData(selectionOrToken);
        if (tokenData) {
            const anchor = tokenData.anchor || tokenData.Anchor || tokenData.start || tokenData.Start || {};
            const focus = tokenData.focus || tokenData.Focus || tokenData.end || tokenData.End || anchor;
            const anchorOffset = Number(anchor.logicalOffset ?? anchor.LogicalOffset ?? anchor.offset ?? anchor.Offset ?? tokenData.startOffset ?? tokenData.StartOffset ?? 0) || 0;
            const focusOffset = Number(focus.logicalOffset ?? focus.LogicalOffset ?? focus.offset ?? focus.Offset ?? tokenData.endOffset ?? tokenData.EndOffset ?? anchorOffset) || 0;
            return createSelectionSnapshot({
                region: tokenData.region || tokenData.Region || anchor.region || focus.region || 'Body',
                anchor: {
                    region: tokenData.region || anchor.region || 'Body',
                    blockId: anchor.blockId || anchor.BlockId || tokenData.blockId || tokenData.BlockId || '',
                    inlineId: anchor.inlineId || anchor.InlineId || anchor.runId || anchor.RunId || null,
                    offset: anchorOffset,
                    affinity: anchor.affinity || anchor.Affinity || 'after',
                    tableId: anchor.tableId || anchor.TableId || tokenData.tableId || tokenData.TableId || null,
                    cellId: anchor.cellId || anchor.CellId || tokenData.cellId || tokenData.CellId || null,
                    headerFooterId: anchor.headerFooterId || anchor.HeaderFooterId || null,
                },
                focus: {
                    region: tokenData.region || focus.region || 'Body',
                    blockId: focus.blockId || focus.BlockId || tokenData.blockId || tokenData.BlockId || '',
                    inlineId: focus.inlineId || focus.InlineId || focus.runId || focus.RunId || null,
                    offset: focusOffset,
                    affinity: focus.affinity || focus.Affinity || 'after',
                    tableId: focus.tableId || focus.TableId || tokenData.tableId || tokenData.TableId || null,
                    cellId: focus.cellId || focus.CellId || tokenData.cellId || tokenData.CellId || null,
                    headerFooterId: focus.headerFooterId || focus.HeaderFooterId || null,
                },
                direction: tokenData.direction || tokenData.Direction || 'forward',
                isCollapsed: tokenData.isCollapsed ?? tokenData.IsCollapsed ?? anchorOffset === focusOffset,
                activeTableCellId: tokenData.cellId || tokenData.CellId || null,
                activeTableId: tokenData.tableId || tokenData.TableId || null,
                activeObjectId: tokenData.activeObjectId || tokenData.ActiveObjectId || null,
            });
        }

        if (selectionOrToken) return createSelectionSnapshot(selectionOrToken);
        return firstModelSelection(model);
    }

    function formattingScalarValue(formatting, commandId, fallback) {
        const inline = (formatting && formatting.inline) || {};
        const mixedMap = inline.mixed || {};
        const commandValues = (formatting && formatting.commandValues) || {};
        if (mixedMap[commandId] === true) return 'mixed';
        const value = commandValues[commandId];
        return value === undefined || value === null ? fallback : value;
    }

    function computeFormattingState(model, selectionOrToken, pendingTypingMarks, inst) {
        const selection = resolveFormattingSelection(model, selectionOrToken, inst);
        const state = collectFormattingState(model, selection, pendingTypingMarks || []);
        const block = findBlock(model, state.selection && state.selection.blockId);
        const disabledReason = !block
            ? 'missing-selection'
            : (state.disabledReasons && (state.disabledReasons.bold || state.disabledReasons.fontSize || state.disabledReasons.textColor || '')) || '';
        return sortObject(Object.assign({}, state, {
            isDisabled: !!disabledReason,
            disabled: !!disabledReason,
            disabledReason: disabledReason || '',
            bold: formattingScalarValue(state, 'bold', false),
            italic: formattingScalarValue(state, 'italic', false),
            underline: formattingScalarValue(state, 'underline', false),
            strike: formattingScalarValue(state, 'strike', false),
            fontFamily: formattingScalarValue(state, 'fontFamily', null),
            fontSize: formattingScalarValue(state, 'fontSize', null),
            textColor: formattingScalarValue(state, 'textColor', null),
            highlightColor: formattingScalarValue(state, 'backgroundColor', null),
            backgroundColor: formattingScalarValue(state, 'backgroundColor', null),
        }));
    }

    return Object.freeze({
        selectionDisabledReason,
        collectFormattingState,
        resolveFormattingSelection,
        computeFormattingState,
        formattingScalarValue,
    });
}
