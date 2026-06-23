// Phase D — core/selection-snapshot.mjs
// Selection snapshot normalisers — the most complex pure-function family in the engine.
// Take user-provided selection input in many shapes (browser DOM, restored snapshot,
// command payload, etc.) and produce a canonical `{ region, range, mode, …, dual-case
// Pascal/camel accessor properties }` object that the rest of the engine consumes.
//
// All pure — no closure state, no DOM access. Extracted from the legacy IIFE.

import { asText, sortObject } from './helpers.mjs';
import { normalizeTextExclusionColumnIndex } from './normalize-target.mjs';

export function createLogicalPosition(input) {
    const value = input || {};
    return sortObject({
        region: asText(value.region || value.Region || 'Body'),
        blockId: asText(value.blockId || value.BlockId || ''),
        inlineId: value.inlineId || value.InlineId || null,
        offset: Number(value.offset ?? value.Offset ?? 0),
        affinity: value.affinity || value.Affinity || 'after',
        visualHintLineId: value.visualHintLineId || value.VisualHintLineId || null,
        layoutIntervalId: value.layoutIntervalId || value.LayoutIntervalId || null,
        virtualCaret: value.virtualCaret === true || value.VirtualCaret === true,
        limitId: value.limitId || value.LimitId || null,
        objectId: value.objectId || value.ObjectId || null,
        cellId: value.cellId || value.CellId || null,
        tableId: value.tableId || value.TableId || null,
        columnIndex: normalizeTextExclusionColumnIndex(value.columnIndex ?? value.ColumnIndex),
        headerFooterId: value.headerFooterId || value.HeaderFooterId || null,
    });
}

export function createLogicalRange(anchor, focus, direction) {
    return sortObject({
        anchor: createLogicalPosition(anchor),
        focus: createLogicalPosition(focus || anchor),
        direction: direction || 'none',
        isCollapsed: !focus || (
            (anchor.blockId || anchor.BlockId) === (focus.blockId || focus.BlockId)
            && Number(anchor.offset ?? anchor.Offset ?? 0) === Number(focus.offset ?? focus.Offset ?? 0)),
    });
}

export function normalizeSelectionModeValue(value) {
    const raw = asText(value || '').trim().toLowerCase();
    if (raw === 'object' || raw === 'image') return 'Object';
    return 'Text';
}

export function normalizeTextSelectionPayload(input, fallbackRange, fallbackRegion) {
    const value = input || {};
    let range = value.range || value.Range || fallbackRange || null;
    if (!range) {
        if (value.anchor || value.Anchor || value.focus || value.Focus) {
            const anchor = createLogicalPosition(value.anchor || value.Anchor
                || value.position || value.Position || value);
            const focus = createLogicalPosition(value.focus || value.Focus
                || value.anchor || value.Anchor
                || value.position || value.Position || value);
            range = createLogicalRange(anchor, focus,
                value.direction || value.Direction
                || (anchor.offset <= focus.offset ? 'forward' : 'backward'));
        } else if (value.anchorBlockId || value.AnchorBlockId
            || value.focusBlockId || value.FocusBlockId) {
            const anchorPosition = createLogicalPosition({
                region: value.region || value.Region || fallbackRegion || 'Body',
                blockId: value.anchorBlockId || value.AnchorBlockId
                    || value.focusBlockId || value.FocusBlockId || '',
                inlineId: value.anchorInlineId || value.AnchorInlineId
                    || value.anchorNodeId || value.AnchorNodeId || null,
                offset: value.anchorOffset ?? value.AnchorOffset
                    ?? value.anchorBlockOffset ?? value.AnchorBlockOffset ?? 0,
                headerFooterId: value.headerFooterId || value.HeaderFooterId || null,
                tableId: value.tableId || value.TableId
                    || value.activeTableId || value.ActiveTableId || null,
                cellId: value.cellId || value.CellId
                    || value.activeTableCellId || value.ActiveTableCellId || null,
                columnIndex: value.columnIndex ?? value.ColumnIndex ?? null,
            });
            const focusPosition = createLogicalPosition({
                region: value.region || value.Region || fallbackRegion || 'Body',
                blockId: value.focusBlockId || value.FocusBlockId
                    || value.anchorBlockId || value.AnchorBlockId || '',
                inlineId: value.focusInlineId || value.FocusInlineId
                    || value.focusNodeId || value.FocusNodeId || null,
                offset: value.focusOffset ?? value.FocusOffset
                    ?? value.focusBlockOffset ?? value.FocusBlockOffset
                    ?? value.anchorOffset ?? value.AnchorOffset ?? 0,
                headerFooterId: value.headerFooterId || value.HeaderFooterId || null,
                tableId: value.tableId || value.TableId
                    || value.activeTableId || value.ActiveTableId || null,
                cellId: value.cellId || value.CellId
                    || value.activeTableCellId || value.ActiveTableCellId || null,
                columnIndex: value.columnIndex ?? value.ColumnIndex ?? null,
            });
            range = createLogicalRange(anchorPosition, focusPosition,
                value.direction || value.Direction
                || (anchorPosition.offset <= focusPosition.offset ? 'forward' : 'backward'));
        } else {
            const position = createLogicalPosition(value.position || value.Position || value);
            range = createLogicalRange(position, position, 'none');
        }
    }

    const anchorPos = createLogicalPosition(range.anchor || range.Anchor || range.start || range.Start || range);
    const focusPos = createLogicalPosition(range.focus || range.Focus || range.end || range.End || anchorPos);
    const region = asText(value.region || value.Region
        || anchorPos.region || focusPos.region || fallbackRegion || 'Body');
    const direction = range.direction || range.Direction
        || value.direction || value.Direction || 'none';
    const isCollapsed = range.isCollapsed !== false
        && value.isCollapsed !== false && value.IsCollapsed !== false;
    return sortObject({
        mode: 'Text',
        selectionMode: 'Text',
        region,
        range: createLogicalRange(anchorPos, focusPos, direction),
        anchor: anchorPos,
        focus: focusPos,
        blockId: focusPos.blockId,
        inlineId: focusPos.inlineId,
        offset: focusPos.offset,
        affinity: focusPos.affinity,
        visualHintLineId: focusPos.visualHintLineId,
        layoutIntervalId: focusPos.layoutIntervalId,
        virtualCaret: focusPos.virtualCaret === true,
        anchorBlockId: anchorPos.blockId,
        anchorInlineId: anchorPos.inlineId,
        anchorOffset: anchorPos.offset,
        focusBlockId: focusPos.blockId,
        focusInlineId: focusPos.inlineId,
        focusOffset: focusPos.offset,
        headerFooterId: value.headerFooterId || value.HeaderFooterId
            || focusPos.headerFooterId || anchorPos.headerFooterId || null,
        isCollapsed,
        direction,
        cellId: value.cellId || value.CellId || focusPos.cellId || null,
        tableId: value.tableId || value.TableId
            || focusPos.tableId || anchorPos.tableId || null,
        columnIndex: normalizeTextExclusionColumnIndex(value.columnIndex ?? value.ColumnIndex
            ?? focusPos.columnIndex ?? anchorPos.columnIndex),
    });
}

export function normalizeObjectSelectionPayload(input, range, textSelection) {
    const value = input || {};
    const source = value.objectSelection || value.ObjectSelection || value;
    const anchor = createLogicalPosition((range && (range.anchor || range.Anchor)) || {});
    const focus = createLogicalPosition((range && (range.focus || range.Focus)) || anchor);
    const objectId = asText(source.objectId || source.ObjectId
        || value.activeObjectId || value.ActiveObjectId
        || value.objectId || value.ObjectId
        || focus.objectId || anchor.objectId || '');
    if (!objectId) return null;
    const anchorBlockId = asText(source.anchorBlockId || source.AnchorBlockId
        || source.blockId || source.BlockId
        || value.activeImageBlockId || value.ActiveImageBlockId
        || focus.blockId || anchor.blockId || '');
    const anchorOffset = Number(source.anchorOffset ?? source.AnchorOffset
        ?? source.offset ?? source.Offset
        ?? anchor.offset ?? focus.offset ?? 0) || 0;
    const region = asText(source.region || source.Region
        || value.region || value.Region
        || anchor.region || focus.region || 'Body');
    const preservedTextSelection = normalizeTextSelectionPayload(
        source.textSelection || source.TextSelection || textSelection || null,
        null, region);
    return sortObject({
        mode: 'Object',
        selectionMode: 'Object',
        region,
        kind: source.kind || source.Kind || value.kind || value.Kind
            || value.hitTargetKind || value.HitTargetKind || 'image',
        objectId,
        blockId: asText(source.blockId || source.BlockId || anchorBlockId),
        anchorBlockId,
        anchorInlineId: source.anchorInlineId || source.AnchorInlineId
            || source.inlineId || source.InlineId || null,
        anchorInlineIndex: Number(source.anchorInlineIndex ?? source.AnchorInlineIndex
            ?? source.inlineIndex ?? source.InlineIndex ?? -1),
        anchorOffset,
        inlineIndex: Number(source.inlineIndex ?? source.InlineIndex
            ?? source.anchorInlineIndex ?? source.AnchorInlineIndex ?? -1),
        runId: source.runId || source.RunId || source.inlineId || source.InlineId || null,
        headerFooterId: source.headerFooterId || source.HeaderFooterId
            || value.headerFooterId || value.HeaderFooterId
            || focus.headerFooterId || anchor.headerFooterId || null,
        tableId: source.tableId || source.TableId
            || value.tableId || value.TableId
            || value.activeTableId || value.ActiveTableId
            || focus.tableId || anchor.tableId || null,
        cellId: source.cellId || source.CellId
            || value.cellId || value.CellId
            || value.activeTableCellId || value.ActiveTableCellId
            || focus.cellId || anchor.cellId || null,
        columnIndex: normalizeTextExclusionColumnIndex(source.columnIndex ?? source.ColumnIndex
            ?? value.columnIndex ?? value.ColumnIndex
            ?? focus.columnIndex ?? anchor.columnIndex),
        textSelection: preservedTextSelection,
    });
}

export function isObjectSelectionSnapshot(selection) {
    const value = selection || {};
    const explicitMode = value.selectionMode || value.SelectionMode
        || value.mode || value.Mode || '';
    const mode = normalizeSelectionModeValue(explicitMode);
    if (asText(explicitMode) && mode === 'Text') return false;
    return mode === 'Object'
        || value.isObjectSelection === true || value.IsObjectSelection === true
        || !!(value.objectSelection || value.ObjectSelection)
        || !!(value.activeObjectId || value.ActiveObjectId || value.objectId || value.ObjectId);
}

// Top-level snapshot — combines text and object selection variants into a dual-case
// (Pascal + camel) record that survives JS interop boundaries with C#.
export function createSelectionSnapshot(input) {
    const value = input || {};
    let range = value.range || value.Range || null;
    if (!range) {
        if (value.anchor || value.Anchor || value.focus || value.Focus) {
            const anchor = createLogicalPosition(value.anchor || value.Anchor
                || value.position || value.Position || value);
            const focus = createLogicalPosition(value.focus || value.Focus
                || value.anchor || value.Anchor
                || value.position || value.Position || value);
            range = createLogicalRange(anchor, focus,
                value.direction || value.Direction
                || (anchor.offset <= focus.offset ? 'forward' : 'backward'));
        } else if (value.anchorBlockId || value.AnchorBlockId
            || value.focusBlockId || value.FocusBlockId) {
            const anchorPosition = createLogicalPosition({
                region: value.region || value.Region || 'Body',
                blockId: value.anchorBlockId || value.AnchorBlockId
                    || value.focusBlockId || value.FocusBlockId || '',
                inlineId: value.anchorInlineId || value.AnchorInlineId
                    || value.anchorNodeId || value.AnchorNodeId || null,
                offset: value.anchorOffset ?? value.AnchorOffset
                    ?? value.anchorBlockOffset ?? value.AnchorBlockOffset ?? 0,
                headerFooterId: value.headerFooterId || value.HeaderFooterId || null,
                tableId: value.tableId || value.TableId
                    || value.activeTableId || value.ActiveTableId || null,
                cellId: value.cellId || value.CellId
                    || value.activeTableCellId || value.ActiveTableCellId || null,
                columnIndex: value.columnIndex ?? value.ColumnIndex ?? null,
            });
            const focusPosition = createLogicalPosition({
                region: value.region || value.Region || 'Body',
                blockId: value.focusBlockId || value.FocusBlockId
                    || value.anchorBlockId || value.AnchorBlockId || '',
                inlineId: value.focusInlineId || value.FocusInlineId
                    || value.focusNodeId || value.FocusNodeId || null,
                offset: value.focusOffset ?? value.FocusOffset
                    ?? value.focusBlockOffset ?? value.FocusBlockOffset
                    ?? value.anchorOffset ?? value.AnchorOffset ?? 0,
                headerFooterId: value.headerFooterId || value.HeaderFooterId || null,
                tableId: value.tableId || value.TableId
                    || value.activeTableId || value.ActiveTableId || null,
                cellId: value.cellId || value.CellId
                    || value.activeTableCellId || value.ActiveTableCellId || null,
                columnIndex: value.columnIndex ?? value.ColumnIndex ?? null,
            });
            range = createLogicalRange(anchorPosition, focusPosition,
                value.direction || value.Direction
                || (anchorPosition.offset <= focusPosition.offset ? 'forward' : 'backward'));
        } else {
            const position = createLogicalPosition(value.position || value.Position || value);
            range = createLogicalRange(position, position, 'none');
        }
    }
    const anchorPos = createLogicalPosition(range.anchor);
    const focusPos = createLogicalPosition(range.focus);
    const explicitMode = value.selectionMode || value.SelectionMode
        || value.mode || value.Mode || '';
    const normalizedMode = normalizeSelectionModeValue(explicitMode);
    const hasExplicitMode = !!asText(explicitMode);
    const hasObjectInput = !!(value.objectSelection || value.ObjectSelection);
    const hasObjectId = !!(value.activeObjectId || value.ActiveObjectId
        || value.objectId || value.ObjectId
        || focusPos.objectId || anchorPos.objectId);
    const isObjectSelection = hasExplicitMode
        ? normalizedMode === 'Object'
        : (value.isObjectSelection === true || value.IsObjectSelection === true
            || hasObjectInput || (hasObjectId && focusPos.blockId === anchorPos.blockId));
    const mode = isObjectSelection ? 'Object' : 'Text';
    const region = asText(value.region || value.Region || range.anchor.region || 'Body');
    const fallbackTextRange = isObjectSelection
        ? createLogicalRange(
            {
                region,
                blockId: anchorPos.blockId || focusPos.blockId,
                offset: anchorPos.offset ?? focusPos.offset ?? 0,
                headerFooterId: anchorPos.headerFooterId || focusPos.headerFooterId || null,
                tableId: anchorPos.tableId || focusPos.tableId || null,
                cellId: anchorPos.cellId || focusPos.cellId || null,
                columnIndex: anchorPos.columnIndex ?? focusPos.columnIndex ?? null,
            },
            {
                region,
                blockId: anchorPos.blockId || focusPos.blockId,
                offset: anchorPos.offset ?? focusPos.offset ?? 0,
                headerFooterId: anchorPos.headerFooterId || focusPos.headerFooterId || null,
                tableId: anchorPos.tableId || focusPos.tableId || null,
                cellId: anchorPos.cellId || focusPos.cellId || null,
                columnIndex: anchorPos.columnIndex ?? focusPos.columnIndex ?? null,
            }, 'none')
        : range;
    const textSelection = isObjectSelection
        ? normalizeTextSelectionPayload(value.textSelection || value.TextSelection
            || value.previousTextSelection || value.PreviousTextSelection || null, fallbackTextRange, region)
        : normalizeTextSelectionPayload(value, range, region);
    const objectSelection = isObjectSelection
        ? normalizeObjectSelectionPayload(value, range, textSelection)
        : null;
    const activeObjectId = value.activeObjectId || value.ActiveObjectId
        || value.objectId || value.ObjectId
        || focusPos.objectId || anchorPos.objectId
        || (objectSelection && objectSelection.objectId) || null;
    const activeImageBlockId = value.activeImageBlockId || value.ActiveImageBlockId
        || (objectSelection && (objectSelection.anchorBlockId || objectSelection.blockId))
        || (isObjectSelection ? focusPos.blockId : null);
    return sortObject({
        region,
        range,
        anchor: anchorPos,
        focus: focusPos,
        mode,
        selectionMode: mode,
        SelectionMode: mode,
        textSelection,
        TextSelection: textSelection,
        objectSelection,
        ObjectSelection: objectSelection,
        anchorOffset: anchorPos.offset,
        focusOffset: focusPos.offset,
        AnchorOffset: anchorPos.offset,
        FocusOffset: focusPos.offset,
        AnchorBlockOffset: anchorPos.offset,
        FocusBlockOffset: focusPos.offset,
        AnchorBlockId: anchorPos.blockId,
        FocusBlockId: focusPos.blockId,
        blockId: focusPos.blockId,
        inlineId: focusPos.inlineId,
        offset: focusPos.offset,
        affinity: focusPos.affinity,
        visualHintLineId: focusPos.visualHintLineId,
        layoutIntervalId: focusPos.layoutIntervalId,
        virtualCaret: focusPos.virtualCaret === true,
        limitId: focusPos.limitId,
        headerFooterId: value.headerFooterId || value.HeaderFooterId
            || focusPos.headerFooterId || anchorPos.headerFooterId || null,
        isCollapsed: isObjectSelection ? false : range.isCollapsed !== false,
        direction: range.direction || 'none',
        objectId: activeObjectId,
        cellId: value.cellId || value.CellId || focusPos.cellId || null,
        tableId: value.tableId || value.TableId || focusPos.tableId || null,
        columnIndex: normalizeTextExclusionColumnIndex(value.columnIndex ?? value.ColumnIndex
            ?? focusPos.columnIndex ?? anchorPos.columnIndex),
        isCellSelection: value.isCellSelection === true || value.IsCellSelection === true
            || !!(value.cellId || value.CellId || focusPos.cellId),
        isObjectSelection,
        activeImageBlockId: activeImageBlockId || null,
        activeObjectId: activeObjectId || null,
        activeTableCellId: value.activeTableCellId || value.ActiveTableCellId
            || value.cellId || value.CellId || focusPos.cellId || null,
        activeTableId: value.activeTableId || value.ActiveTableId
            || value.tableId || value.TableId
            || focusPos.tableId || anchorPos.tableId || null,
        activeCommentId: value.activeCommentId || value.ActiveCommentId || null,
        activeRevisionId: value.activeRevisionId || value.ActiveRevisionId || null,
        hitTargetKind: value.hitTargetKind || value.HitTargetKind || (isObjectSelection ? 'image' : null),
    });
}
