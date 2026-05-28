// Phase D — core/blazor-formatting-state.mjs
// `toBlazorFormattingState(formatting)` — adapts the engine's formatting-state
// object into the Pascal-cased shape the Blazor toolbar consumes. Boolean marks
// become tri-state (0=off, 1=on, 2=mixed), aliased command ids unwrap to a single
// state, paragraph alignment maps to 0..3 numeric, and Pascal mirrors are added.

import { sortObject } from './helpers.mjs';
import { normalizeParagraphAlignment } from '../layout/paragraph-alignment.mjs';

export function toBlazorFormattingState(formatting) {
    const state = formatting || {};
    const commandValues = state.commandValues || {};
    const inline = state.inline || {};
    const mixed = inline.mixed || {};
    const paragraph = state.paragraph || {};

    function triState(commandId) {
        if (mixed[commandId] === true) return 2;
        return commandValues[commandId] === true ? 1 : 0;
    }

    function alignmentValue(value) {
        const normalized = normalizeParagraphAlignment(value);
        if (normalized === 'center') return 1;
        if (normalized === 'right' || normalized === 'end') return 2;
        if (normalized === 'justify') return 3;
        return 0;
    }

    const boldState = triState('bold');
    const italicState = triState('italic');
    const underlineState = triState('underline');
    const strikeState = triState('strike');

    return sortObject(Object.assign({}, state, {
        bold: boldState,
        italic: italicState,
        underline: underlineState,
        strike: strikeState,
        strikethrough: strikeState,
        Bold: boldState,
        Italic: italicState,
        Underline: underlineState,
        Strikethrough: strikeState,
        ParagraphAlignment: alignmentValue(commandValues.alignment || paragraph.alignment),
        ParagraphAlignmentMixed: false,
        FontFamily: commandValues.fontFamily || null,
        FontFamilyMixed: mixed.fontFamily === true,
        FontSize: commandValues.fontSize || null,
        FontSizeMixed: mixed.fontSize === true,
        TextColor: commandValues.textColor || null,
        TextColorMixed: mixed.textColor === true,
        HighlightColor: commandValues.backgroundColor || null,
        HighlightColorMixed: mixed.backgroundColor === true,
        LineSpacing: Number(commandValues.lineSpacing || paragraph.lineSpacing || 1) || 1,
        SpacingBefore: Number(commandValues.spacingBefore ?? paragraph.spacingBefore ?? 0) || 0,
        SpacingAfter: Number(commandValues.spacingAfter ?? paragraph.spacingAfter ?? 0) || 0,
        LeftIndent: Number(commandValues.indent ?? paragraph.indentLevel ?? 0) || 0,
        IsBulletList: String(
            commandValues.list || paragraph.listType || '').toLowerCase() === 'bullet',
        IsNumberedList: String(
            commandValues.list || paragraph.listType || '').toLowerCase() === 'numbered',
        ListMixed: false,
        ActiveRegion: state.selection && state.selection.region || 'Body',
        CurrentSelection: state.selection || null,
        IsDisabled: state.isDisabled === true || state.disabled === true,
        DisabledReason: state.disabledReason || null,
    }));
}
