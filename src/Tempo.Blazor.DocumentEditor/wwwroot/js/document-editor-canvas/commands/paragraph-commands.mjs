import {
    applyBlockStyleToBlock,
    blockStyleState,
    invalidatesOutlineCache,
} from './heading-style.mjs';
import {
    DEFAULT_BULLET_NUMBERING_ID,
    DEFAULT_LEGAL_NUMBERING_ID,
    DEFAULT_NUMBERED_NUMBERING_ID,
    createDefaultBulletDefinition,
    createDefaultLegalDefinition,
    createDefaultNumberedDefinition,
    formatName,
} from '../lists/numbering-definition.mjs';
import {
    deleteStyle,
    ensureStyleStore,
    findStyle,
    renameStyle,
    upsertStyle,
} from '../styles/style-store.mjs';
import { resolveBlockStyleFormatting } from '../styles/style-resolver.mjs';

const PARAGRAPH_COMMANDS = new Set([
    'align',
    'alignleft',
    'aligncenter',
    'alignright',
    'alignjustify',
    'lineSpacing',
    'spacingBefore',
    'spacingAfter',
    'increaseIndent',
    'decreaseIndent',
    'setParagraphIndents',
    'setTabStop',
    'moveTabStop',
    'clearTabStops',
    'setDefaultTabWidth',
    'bulletList',
    'numberedList',
    'toggleBulletList',
    'toggleNumberedList',
    'increaseListLevel',
    'decreaseListLevel',
    'setListFormat',
    'restartNumbering',
    'continueNumbering',
    'setNumberingValue',
    'defineListStyle',
    'setListStyle',
    'blockStyle',
    'quoteStyle',
    'applyStyle',
    'modifyStyle',
    'defineStyle',
    'createStyle',
    'createStyleFromSelection',
    'deleteStyle',
    'renameStyle',
    'resetStyleFormatting',
]);

const VIEW_COMMANDS = new Set([
    'showRuler',
    'showBlocks',
    'toggleNonPrintingCharacters',
]);

export function isParagraphCommand(commandId) {
    return PARAGRAPH_COMMANDS.has(canonicalCommandId(commandId));
}

export function isCanvasViewCommand(commandId) {
    return VIEW_COMMANDS.has(canonicalCommandId(commandId));
}

export function createParagraphCommandState(initial = {}) {
    const source = initial && typeof initial === 'object' ? initial : {};
    return {
        showRuler: source.showRuler !== false,
        showBlocks: source.showBlocks === true,
        showNonPrintingCharacters: source.showNonPrintingCharacters === true,
    };
}

export function applyParagraphCommand(model, selection, commandId, argument = null, state = createParagraphCommandState()) {
    const canonical = canonicalCommandId(commandId);
    if (VIEW_COMMANDS.has(canonical)) {
        return applyViewCommand(model, selection, canonical, argument, state);
    }

    if (isStyleStoreOnlyCommand(canonical)) {
        return applyStyleStoreCommand(model, selection, canonical, argument, state);
    }

    const blocks = orderedBlocks(model);
    const selectedIndexes = selectedBlockIndexes(blocks, selection);
    if (selectedIndexes.length === 0) {
        return unchanged(model, selection, state);
    }

    const beforeBlocks = blocks.map(clone);
    const nextModel = clone(model);
    const nextEntries = orderedBlockEntries(nextModel);
    const nextBlocks = nextEntries.map(entry => entry.block);
    const dirtyBlockIds = [];
    let changed = false;

    const selectedIds = selectedIndexes.map(index => String(blocks[index]?.id || '')).filter(Boolean);
    for (const blockId of selectedIds) {
        const entry = nextEntries.find(item => String(item.block?.id || '') === blockId);
        if (!entry) {
            continue;
        }

        const block = entry.block;
        if (!isEditableTextBlock(block)) {
            continue;
        }

        const result = applyToBlock(block, canonical, argument, nextModel);
        if (result.changed) {
            entry.list[entry.index] = result.block;
            dirtyBlockIds.push(String(result.block.id || block.id || ''));
            changed = true;
        }
    }

    if (!changed) {
        return {
            changed: false,
            model,
            selection,
            state,
            dirtyBlockIds: [],
            formattingState: queryParagraphCommandState(model, selection, state),
        };
    }

    const afterBlocks = orderedBlockEntries(nextModel).map(entry => entry.block);
    if (invalidatesOutlineCache(beforeBlocks, afterBlocks)) {
        nextModel.outlineRevision = Math.max(0, Number(nextModel.outlineRevision || 0) || 0) + 1;
        nextModel.tableOfContentsRevision = Math.max(0, Number(nextModel.tableOfContentsRevision || 0) || 0) + 1;
    }

    syncSectionBlocks(nextModel, new Set(dirtyBlockIds));

    return {
        changed: true,
        model: nextModel,
        selection,
        state,
        dirtyBlockIds,
        formattingState: queryParagraphCommandState(nextModel, selection, state),
    };
}

export function queryParagraphCommandState(model, selection, state = createParagraphCommandState()) {
    const selectedBlocks = selectedBlocksForSelection(model, selection).filter(isEditableTextBlock);
    const alignment = aggregate(selectedBlocks, block => alignmentName(readParagraphProperties(block).alignment));
    const lineSpacing = aggregateNumber(selectedBlocks, block => positiveNumber(readParagraphProperties(block).lineSpacing, 1));
    const spacingBefore = aggregateNumber(selectedBlocks, block => nonNegativeNumber(readParagraphProperties(block).spacingBefore, 0));
    const spacingAfter = aggregateNumber(selectedBlocks, block => nonNegativeNumber(readParagraphProperties(block).spacingAfter, 0));
    const leftIndent = aggregateNumber(selectedBlocks, block => nonNegativeNumber(readParagraphProperties(block).leftIndent, 0));
    const rightIndent = aggregateNumber(selectedBlocks, block => nonNegativeNumber(readParagraphProperties(block).rightIndent, 0));
    const firstLineIndent = aggregateNumber(selectedBlocks, block => Number(readParagraphProperties(block).firstLineIndent) || 0);
    const defaultTabWidth = aggregateNumber(selectedBlocks, block => positiveNumber(readParagraphProperties(block).defaultTabWidth, 36));
    const tabStops = aggregate(selectedBlocks, block => JSON.stringify(readParagraphProperties(block).tabStops || []));
    const list = aggregate(selectedBlocks, block => listState(block));
    const listLevel = aggregateNumber(selectedBlocks, block => Number(block?.content?.list?.indentLevel ?? block?.content?.list?.IndentLevel ?? 0) || 0);
    const listFormat = aggregate(selectedBlocks, block => String(block?.content?.list?.numberFormat ?? block?.content?.list?.NumberFormat ?? '').trim());
    const listStyle = aggregate(selectedBlocks, block => String(block?.content?.list?.listStyleId ?? block?.content?.list?.ListStyleId ?? '').trim());
    const style = aggregate(selectedBlocks, block => blockStyleState(block, model).name);
    const directFormatting = aggregate(selectedBlocks, block => Object.keys(resolveBlockStyleFormatting(model, block).directFormatting || {}).sort().join('|'));

    return {
        commands: {
            align: commandValueState(alignment.value || 'left', alignment.mixed),
            alignleft: commandToggleState(alignment.value === 'left', alignment.mixed),
            aligncenter: commandToggleState(alignment.value === 'center', alignment.mixed),
            alignright: commandToggleState(alignment.value === 'right', alignment.mixed),
            alignjustify: commandToggleState(alignment.value === 'justify', alignment.mixed),
            lineSpacing: commandValueState(lineSpacing.value ?? 1, lineSpacing.mixed),
            spacingBefore: commandValueState(spacingBefore.value ?? 0, spacingBefore.mixed),
            spacingAfter: commandValueState(spacingAfter.value ?? 0, spacingAfter.mixed),
            increaseIndent: commandToggleState(false, false),
            decreaseIndent: commandValueState(leftIndent.value ?? 0, leftIndent.mixed),
            setParagraphIndents: commandValueState(leftIndent.value ?? 0, leftIndent.mixed || rightIndent.mixed || firstLineIndent.mixed),
            setTabStop: commandValueState(tabStops.value || '[]', tabStops.mixed),
            moveTabStop: commandValueState(tabStops.value || '[]', tabStops.mixed),
            clearTabStops: commandValueState(tabStops.value || '[]', tabStops.mixed),
            setDefaultTabWidth: commandValueState(defaultTabWidth.value ?? 36, defaultTabWidth.mixed),
            bulletList: commandToggleState(list.value === 'bullet', list.mixed),
            numberedList: commandToggleState(list.value === 'numbered', list.mixed),
            increaseListLevel: commandValueState(listLevel.value ?? 0, listLevel.mixed),
            decreaseListLevel: commandValueState(listLevel.value ?? 0, listLevel.mixed),
            setListFormat: commandValueState(listFormat.value || null, listFormat.mixed),
            restartNumbering: commandToggleState(false, false),
            continueNumbering: commandToggleState(false, false),
            setNumberingValue: commandValueState(null, false),
            defineListStyle: commandValueState(listStyle.value || null, listStyle.mixed),
            setListStyle: commandValueState(listStyle.value || null, listStyle.mixed),
            blockStyle: commandValueState(style.value || 'Normal', style.mixed),
            applyStyle: commandValueState(style.value || 'Normal', style.mixed),
            modifyStyle: commandToggleState(false, false),
            defineStyle: commandToggleState(false, false),
            createStyle: commandToggleState(false, false),
            createStyleFromSelection: commandToggleState(false, false),
            deleteStyle: commandToggleState(false, false),
            renameStyle: commandToggleState(false, false),
            resetStyleFormatting: commandToggleState(directFormatting.value !== '', directFormatting.mixed),
            quoteStyle: commandToggleState(style.value === 'Quote', style.mixed),
            showRuler: commandToggleState(state.showRuler === true, false),
            showBlocks: commandToggleState(state.showBlocks === true, false),
            toggleNonPrintingCharacters: commandToggleState(state.showNonPrintingCharacters === true, false),
        },
        paragraph: {
            alignment: alignment.value || 'left',
            alignmentMixed: alignment.mixed,
            lineSpacing: lineSpacing.value ?? 1,
            lineSpacingMixed: lineSpacing.mixed,
            spacingBefore: spacingBefore.value ?? 0,
            spacingBeforeMixed: spacingBefore.mixed,
            spacingAfter: spacingAfter.value ?? 0,
            spacingAfterMixed: spacingAfter.mixed,
            leftIndent: leftIndent.value ?? 0,
            leftIndentMixed: leftIndent.mixed,
            rightIndent: rightIndent.value ?? 0,
            rightIndentMixed: rightIndent.mixed,
            firstLineIndent: firstLineIndent.value ?? 0,
            firstLineIndentMixed: firstLineIndent.mixed,
            defaultTabWidth: defaultTabWidth.value ?? 36,
            defaultTabWidthMixed: defaultTabWidth.mixed,
            tabStops: tabStops.value ? JSON.parse(tabStops.value) : [],
            tabStopsMixed: tabStops.mixed,
            bulletList: list.value === 'bullet',
            numberedList: list.value === 'numbered',
            listMixed: list.mixed,
            listLevel: listLevel.value ?? 0,
            listLevelMixed: listLevel.mixed,
            listFormat: listFormat.value || null,
            listFormatMixed: listFormat.mixed,
            listStyleId: listStyle.value || null,
            listStyleMixed: listStyle.mixed,
            blockStyle: style.value || 'Normal',
            blockStyleMixed: style.mixed,
            directFormattingKeys: directFormatting.value || '',
            directFormattingMixed: directFormatting.mixed,
            quickStyles: ensureStyleStore(model).filter(item => item.isQuickStyle === true || item.isPrimary === true),
        },
        view: {
            showRuler: state.showRuler === true,
            showBlocks: state.showBlocks === true,
            showNonPrintingCharacters: state.showNonPrintingCharacters === true,
        },
    };
}

export function canonicalCommandId(commandId) {
    const compact = String(commandId || '').replace(/[\s_-]/g, '').toLowerCase();
    if (compact === 'alignleft') return 'alignleft';
    if (compact === 'aligncenter') return 'aligncenter';
    if (compact === 'alignright') return 'alignright';
    if (compact === 'alignjustify') return 'alignjustify';
    if (compact === 'paragraphalignment' || compact === 'setparagraphalignment') return 'align';
    if (compact === 'linespacing' || compact === 'setlinespacing') return 'lineSpacing';
    if (compact === 'spacingbefore') return 'spacingBefore';
    if (compact === 'spacingafter') return 'spacingAfter';
    if (compact === 'increaseindent') return 'increaseIndent';
    if (compact === 'decreaseindent') return 'decreaseIndent';
    if (compact === 'setparagraphindents' || compact === 'setparagraphindent' || compact === 'paragraphindents') return 'setParagraphIndents';
    if (compact === 'settabstop' || compact === 'addtabstop') return 'setTabStop';
    if (compact === 'movetabstop' || compact === 'updatetabstop') return 'moveTabStop';
    if (compact === 'cleartabstops' || compact === 'removetabstops' || compact === 'cleartabs') return 'clearTabStops';
    if (compact === 'setdefaulttabwidth' || compact === 'defaulttabwidth') return 'setDefaultTabWidth';
    if (compact === 'bulletlist' || compact === 'togglebulletlist') return 'bulletList';
    if (compact === 'numberedlist' || compact === 'togglenumberedlist') return 'numberedList';
    if (compact === 'increaselistlevel' || compact === 'indentlist' || compact === 'nestlist') return 'increaseListLevel';
    if (compact === 'decreaselistlevel' || compact === 'outdentlist' || compact === 'unnestlist') return 'decreaseListLevel';
    if (compact === 'setlistformat' || compact === 'numberformat' || compact === 'setnumberformat') return 'setListFormat';
    if (compact === 'restartnumbering' || compact === 'restartlist') return 'restartNumbering';
    if (compact === 'continuenumbering' || compact === 'continuelist') return 'continueNumbering';
    if (compact === 'setnumberingvalue' || compact === 'setlistvalue') return 'setNumberingValue';
    if (compact === 'defineliststyle' || compact === 'createliststyle') return 'defineListStyle';
    if (compact === 'setliststyle' || compact === 'applyliststyle') return 'setListStyle';
    if (compact === 'blockstyle' || compact === 'setparagraphstyle' || compact === 'paragraphstyle' || compact === 'applystyle') return 'blockStyle';
    if (compact === 'modifystyle' || compact === 'updatestyle') return 'modifyStyle';
    if (compact === 'definestyle') return 'defineStyle';
    if (compact === 'createstyle' || compact === 'createnewstyle') return 'createStyle';
    if (compact === 'createstylefromselection' || compact === 'newstylefromselection') return 'createStyleFromSelection';
    if (compact === 'deletestyle' || compact === 'removestyle') return 'deleteStyle';
    if (compact === 'renamestyle') return 'renameStyle';
    if (compact === 'resetstyleformatting' || compact === 'clearparagraphoverrides') return 'resetStyleFormatting';
    if (compact === 'quotestyle') return 'quoteStyle';
    if (compact === 'showruler') return 'showRuler';
    if (compact === 'showblocks') return 'showBlocks';
    if (compact === 'shownonprintingcharacters' || compact === 'togglenonprintingcharacters') return 'toggleNonPrintingCharacters';
    if (/^heading[1-6]$/.test(compact)) return 'blockStyle';
    return compact;
}

function applyToBlock(block, commandId, argument, model) {
    if (commandId === 'align' || commandId.startsWith('align')) {
        return setParagraphProperty(block, 'alignment', alignmentValue(commandId === 'align' ? argument : commandId.replace('align', '')));
    }

    if (commandId === 'lineSpacing') {
        return setParagraphProperty(block, 'lineSpacing', clampNumber(readCommandValue(argument, 'lineSpacing'), 0.8, 3));
    }

    if (commandId === 'spacingBefore') {
        return setParagraphProperty(block, 'spacingBefore', clampNumber(readCommandValue(argument, 'spacingBefore'), 0, 144));
    }

    if (commandId === 'spacingAfter') {
        return setParagraphProperty(block, 'spacingAfter', clampNumber(readCommandValue(argument, 'spacingAfter'), 0, 144));
    }

    if (commandId === 'increaseIndent' || commandId === 'decreaseIndent') {
        const props = readParagraphProperties(block);
        const current = nonNegativeNumber(props.leftIndent, 0);
        const direction = commandId === 'increaseIndent' ? 1 : -1;
        return setParagraphProperty(block, 'leftIndent', Math.max(0, current + (direction * 18)));
    }

    if (commandId === 'setParagraphIndents') {
        return setParagraphIndents(block, argument);
    }

    if (commandId === 'setTabStop' || commandId === 'moveTabStop') {
        return setTabStop(block, argument);
    }

    if (commandId === 'clearTabStops') {
        return clearTabStops(block, argument);
    }

    if (commandId === 'setDefaultTabWidth') {
        return setParagraphProperty(block, 'defaultTabWidth', clampNumber(readCommandValue(argument, 'defaultTabWidth'), 6, 288));
    }

    if (commandId === 'bulletList' || commandId === 'numberedList') {
        ensureDefaultNumberingDefinitions(model);
        return toggleList(block, commandId === 'numberedList');
    }

    if (commandId === 'increaseListLevel' || commandId === 'decreaseListLevel') {
        return changeListLevel(block, commandId === 'increaseListLevel' ? 1 : -1);
    }

    if (commandId === 'setListFormat') {
        ensureDefaultNumberingDefinitions(model);
        return setListFormat(block, argument);
    }

    if (commandId === 'restartNumbering') {
        return setNumberingRestart(block, argument);
    }

    if (commandId === 'continueNumbering') {
        return setNumberingContinue(block);
    }

    if (commandId === 'setNumberingValue') {
        return setNumberingValue(block, argument);
    }

    if (commandId === 'defineListStyle') {
        ensureDefaultNumberingDefinitions(model);
        const style = ensureListStyle(model, argument);
        return applyListStyle(block, style.id, style.numberingId);
    }

    if (commandId === 'setListStyle') {
        ensureDefaultNumberingDefinitions(model);
        const style = findListStyle(model, argument);
        return style ? applyListStyle(block, style.id, style.numberingId) : { changed: false, block };
    }

    if (commandId === 'blockStyle') {
        return applyBlockStyleToBlock(block, resolveStyleArgument(argument), model);
    }

    if (commandId === 'quoteStyle') {
        return applyBlockStyleToBlock(block, 'Quote', model);
    }

    if (commandId === 'resetStyleFormatting') {
        return resetBlockStyleFormatting(block);
    }

    if (commandId === 'createStyle' || commandId === 'createStyleFromSelection' || commandId === 'defineStyle') {
        const style = createStyleFromBlock(model, block, argument);
        return applyBlockStyleToBlock(block, style.id, model);
    }

    return { changed: false, block };
}

function applyStyleStoreCommand(model, selection, commandId, argument, state) {
    const nextModel = clone(model);
    ensureStyleStore(nextModel);
    const before = JSON.stringify(nextModel.styles || []);
    const dirtyBlockIds = [];
    let changed = false;
    let style = null;
    let previousName = null;

    if (commandId === 'modifyStyle' || commandId === 'defineStyle') {
        const result = upsertStyle(nextModel, stylePayload(argument));
        changed = result.changed;
        style = result.style;
    } else if (commandId === 'deleteStyle') {
        const result = deleteStyle(nextModel, styleIdArgument(argument));
        changed = result.changed;
        style = result.style;
        if (changed) {
            remapBlocksUsingDeletedStyle(nextModel, style);
        }
    } else if (commandId === 'renameStyle') {
        const id = styleIdArgument(argument);
        const nextName = argument?.name ?? argument?.Name ?? argument?.newName ?? argument?.NewName;
        const result = renameStyle(nextModel, id, nextName);
        changed = result.changed;
        style = result.style;
        previousName = result.previousName;
        if (changed) {
            renameBlocksUsingStyle(nextModel, style, previousName);
        }
    }

    if (changed) {
        for (const block of orderedBlocks(nextModel)) {
            if (style && blockUsesStyle(block, style, previousName)) {
                dirtyBlockIds.push(String(block.id || ''));
            }
        }

        if (style?.headingLevel || style?.outlineLevel || JSON.stringify(nextModel.styles || []) !== before) {
            nextModel.outlineRevision = Math.max(0, Number(nextModel.outlineRevision || 0) || 0) + 1;
            nextModel.tableOfContentsRevision = Math.max(0, Number(nextModel.tableOfContentsRevision || 0) || 0) + 1;
        }

        syncSectionBlocks(nextModel, new Set(dirtyBlockIds));
    }

    return {
        changed,
        model: changed ? nextModel : model,
        selection,
        state,
        dirtyBlockIds,
        formattingState: queryParagraphCommandState(changed ? nextModel : model, selection, state),
    };
}

function applyViewCommand(model, selection, commandId, argument, state) {
    const nextState = createParagraphCommandState(state);
    const value = typeof argument === 'boolean' ? argument : argument?.value;
    if (commandId === 'showRuler') {
        nextState.showRuler = typeof value === 'boolean' ? value : !nextState.showRuler;
    } else if (commandId === 'showBlocks') {
        nextState.showBlocks = typeof value === 'boolean' ? value : !nextState.showBlocks;
    } else if (commandId === 'toggleNonPrintingCharacters') {
        nextState.showNonPrintingCharacters = typeof value === 'boolean' ? value : !nextState.showNonPrintingCharacters;
    }

    const changed = JSON.stringify(nextState) !== JSON.stringify(createParagraphCommandState(state));
    return {
        changed,
        viewChanged: changed,
        model,
        selection,
        state: nextState,
        dirtyBlockIds: [],
        formattingState: queryParagraphCommandState(model, selection, nextState),
    };
}

function isStyleStoreOnlyCommand(commandId) {
    return commandId === 'modifyStyle'
        || commandId === 'deleteStyle'
        || commandId === 'renameStyle';
}

function stylePayload(argument) {
    if (typeof argument === 'string') {
        return { id: argument, name: argument };
    }

    const source = argument && typeof argument === 'object' ? argument : {};
    const id = source.id ?? source.Id ?? source.styleId ?? source.StyleId ?? source.value ?? source.Value ?? source.name ?? source.Name;
    const name = source.name ?? source.Name ?? source.styleName ?? source.StyleName ?? id;
    return {
        id,
        name,
        type: source.type ?? source.Type ?? 'paragraph',
        basedOn: source.basedOn ?? source.BasedOn ?? 'normal',
        next: source.next ?? source.Next ?? 'normal',
        isQuickStyle: source.isQuickStyle ?? source.IsQuickStyle ?? true,
        isPrimary: source.isPrimary ?? source.IsPrimary ?? false,
        headingLevel: source.headingLevel ?? source.HeadingLevel ?? null,
        outlineLevel: source.outlineLevel ?? source.OutlineLevel ?? source.headingLevel ?? source.HeadingLevel ?? null,
        paragraphFormat: source.paragraphFormat ?? source.ParagraphFormat ?? {},
        characterFormat: source.characterFormat ?? source.CharacterFormat ?? {},
        tableFormat: source.tableFormat ?? source.TableFormat ?? {},
        listFormat: source.listFormat ?? source.ListFormat ?? {},
    };
}

function styleIdArgument(argument) {
    return String(typeof argument === 'string'
        ? argument
        : argument?.id ?? argument?.Id ?? argument?.styleId ?? argument?.StyleId ?? argument?.value ?? argument?.Value ?? argument?.name ?? argument?.Name ?? '').trim();
}

function createStyleFromBlock(model, block, argument) {
    const payload = stylePayload(argument);
    const current = blockStyleState(block, model);
    const id = String(payload.id || payload.name || `style-${Date.now()}`).trim();
    const style = {
        ...payload,
        id,
        name: String(payload.name || id),
        type: 'paragraph',
        basedOn: payload.basedOn || current.id || 'normal',
        paragraphFormat: {
            ...(payload.paragraphFormat || {}),
            ...readParagraphProperties(block),
        },
        characterFormat: {
            ...(payload.characterFormat || {}),
        },
        isQuickStyle: payload.isQuickStyle !== false,
    };
    const result = upsertStyle(model, style);
    return result.style || style;
}

function resetBlockStyleFormatting(block) {
    const nextBlock = clone(block);
    const before = JSON.stringify(nextBlock.paragraphProperties || {});
    nextBlock.paragraphProperties = {};
    return { changed: before !== '{}', block: nextBlock };
}

function blockUsesStyle(block, style, previousName = null) {
    const content = block?.content || {};
    const styleId = String(style?.id || '');
    const styleName = String(style?.name || '');
    return String(content.styleId || '') === styleId
        || String(content.styleName || '') === styleName
        || (previousName && String(content.styleName || '') === String(previousName));
}

function renameBlocksUsingStyle(model, style, previousName) {
    for (const block of orderedBlocks(model)) {
        if (blockUsesStyle(block, style, previousName)) {
            block.content = { ...(block.content || {}) };
            block.content.styleId = style.id;
            block.content.styleName = style.name;
        }
    }
}

function remapBlocksUsingDeletedStyle(model, style) {
    const fallback = findStyle(model, style?.basedOn || 'normal') || findStyle(model, 'normal');
    for (const block of orderedBlocks(model)) {
        if (!blockUsesStyle(block, style)) {
            continue;
        }

        block.content = { ...(block.content || {}) };
        block.content.styleId = fallback?.id || 'normal';
        block.content.styleName = fallback?.name || 'Normal';
        if (!fallback?.headingLevel) {
            block.type = 'paragraph';
            block.content.type = 'paragraph';
            block.content.headingLevel = null;
            block.content.outlineLevel = null;
        }
    }
}

function setParagraphProperty(block, key, value) {
    const nextBlock = clone(block);
    const props = readParagraphProperties(nextBlock);
    const before = normalizeParagraphPropertyValue(props[key]);
    props[key] = value;
    nextBlock.paragraphProperties = props;
    return {
        changed: normalizeParagraphPropertyValue(value) !== before,
        block: nextBlock,
    };
}

function setParagraphIndents(block, argument) {
    const nextBlock = clone(block);
    const props = readParagraphProperties(nextBlock);
    const before = JSON.stringify({
        leftIndent: props.leftIndent,
        rightIndent: props.rightIndent,
        firstLineIndent: props.firstLineIndent,
    });
    const source = argument && typeof argument === 'object' ? argument : {};
    if (source.leftIndent != null || source.LeftIndent != null) {
        props.leftIndent = clampNumber(source.leftIndent ?? source.LeftIndent, 0, 432);
    }

    if (source.rightIndent != null || source.RightIndent != null) {
        props.rightIndent = clampNumber(source.rightIndent ?? source.RightIndent, 0, 432);
    }

    if (source.firstLineIndent != null || source.FirstLineIndent != null) {
        props.firstLineIndent = clampNumber(source.firstLineIndent ?? source.FirstLineIndent, -216, 216);
    }

    nextBlock.paragraphProperties = props;
    const after = JSON.stringify({
        leftIndent: props.leftIndent,
        rightIndent: props.rightIndent,
        firstLineIndent: props.firstLineIndent,
    });
    return { changed: before !== after, block: nextBlock };
}

function setTabStop(block, argument) {
    const nextBlock = clone(block);
    const props = readParagraphProperties(nextBlock);
    const before = JSON.stringify(props.tabStops || []);
    const stop = normalizeCommandTabStop(argument);
    if (!stop) {
        return { changed: false, block };
    }

    const removePosition = Number(argument?.fromPosition ?? argument?.FromPosition ?? argument?.previousPosition ?? argument?.PreviousPosition);
    const nextStops = (props.tabStops || [])
        .filter(existing => Math.abs(Number(existing.position || 0) - stop.position) > 0.5)
        .filter(existing => !Number.isFinite(removePosition) || Math.abs(Number(existing.position || 0) - removePosition) > 0.5);
    nextStops.push(stop);
    props.tabStops = nextStops.sort((left, right) => left.position - right.position);
    nextBlock.paragraphProperties = props;
    return { changed: before !== JSON.stringify(props.tabStops), block: nextBlock };
}

function clearTabStops(block, argument) {
    const nextBlock = clone(block);
    const props = readParagraphProperties(nextBlock);
    const before = JSON.stringify(props.tabStops || []);
    const position = Number(argument?.position ?? argument?.Position);
    props.tabStops = Number.isFinite(position)
        ? (props.tabStops || []).filter(stop => Math.abs(Number(stop.position || 0) - position) > 0.5)
        : [];
    nextBlock.paragraphProperties = props;
    return { changed: before !== JSON.stringify(props.tabStops), block: nextBlock };
}

function normalizeCommandTabStop(argument) {
    const source = typeof argument === 'number' ? { position: argument } : (argument || {});
    const position = Number(source.position ?? source.Position ?? source.value ?? source.Value);
    if (!Number.isFinite(position) || position < 0) {
        return null;
    }

    return {
        position: clampNumber(position, 0, 720),
        alignment: normalizeTabAlignment(source.alignment ?? source.Alignment ?? 'left'),
        leader: normalizeTabLeader(source.leader ?? source.Leader ?? 'none'),
    };
}

function normalizeTabAlignment(value) {
    if (typeof value === 'number') {
        return ['left', 'center', 'right', 'decimal', 'bar'][Math.max(0, Math.min(4, Math.trunc(value)))] || 'left';
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'center' || normalized === 'centre' || normalized === 'middle') return 'center';
    if (normalized === 'right' || normalized === 'end') return 'right';
    if (normalized === 'decimal') return 'decimal';
    if (normalized === 'bar') return 'bar';
    return 'left';
}

function normalizeTabLeader(value) {
    if (typeof value === 'number') {
        return ['none', 'dots', 'dash', 'underline'][Math.max(0, Math.min(3, Math.trunc(value)))] || 'none';
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'dot' || normalized === 'dots' || normalized === 'dotted') return 'dots';
    if (normalized === 'dash' || normalized === 'dashes' || normalized === 'dashed') return 'dash';
    if (normalized === 'underline' || normalized === 'line') return 'underline';
    return 'none';
}

function toggleList(block, ordered) {
    const nextBlock = clone(block);
    const isSameList = String(nextBlock.type || nextBlock.content?.type || '').toLowerCase() === 'list'
        && Boolean(nextBlock.content?.list?.ordered) === ordered;
    const before = JSON.stringify({ type: nextBlock.type, contentType: nextBlock.content?.type, list: nextBlock.content?.list ?? null });
    nextBlock.content = nextBlock.content && typeof nextBlock.content === 'object' ? { ...nextBlock.content } : { runs: [] };

    if (isSameList) {
        nextBlock.type = 'paragraph';
        nextBlock.content.type = 'paragraph';
        nextBlock.content.list = null;
        nextBlock.content.styleId = null;
        nextBlock.content.styleName = null;
        nextBlock.content.outlineLevel = null;
        nextBlock.content.headingLevel = null;
    } else {
        const previousLevel = Math.max(0, Number(nextBlock.content.list?.indentLevel || 0) || 0);
        const existing = nextBlock.content.list || {};
        const numberingId = ordered ? DEFAULT_NUMBERED_NUMBERING_ID : DEFAULT_BULLET_NUMBERING_ID;
        nextBlock.type = 'list';
        nextBlock.content.type = 'list';
        nextBlock.content.headingLevel = null;
        nextBlock.content.styleId = ordered ? 'numbered-list' : 'bullet-list';
        nextBlock.content.styleName = ordered ? 'Numbered List' : 'Bullet List';
        nextBlock.content.outlineLevel = null;
        nextBlock.content.list = {
            ordered,
            indentLevel: previousLevel,
            startNumber: Math.max(1, Number(existing.startNumber || existing.StartNumber || 1) || 1),
            numberingId,
            abstractNumberingId: numberingId,
            listStyleId: ordered ? 'numbered-list' : 'bullet-list',
            numberFormat: ordered ? 'decimal' : 'bullet',
            levelText: ordered ? `%${previousLevel + 1}.` : '',
            suffix: 'tab',
            labelIndent: previousLevel * 24,
            hangingIndent: 24,
            restartNumbering: false,
            continueNumbering: false,
        };
    }

    return {
        changed: before !== JSON.stringify({ type: nextBlock.type, contentType: nextBlock.content.type, list: nextBlock.content.list ?? null }),
        block: nextBlock,
    };
}

function setListFormat(block, argument) {
    const format = listFormatArgument(argument);
    const ordered = format !== 'bullet';
    const source = String(block?.type || block?.content?.type || '').toLowerCase() === 'list'
        ? block
        : toggleList(block, ordered).block;
    const nextBlock = clone(source);
    nextBlock.type = 'list';
    nextBlock.content = nextBlock.content && typeof nextBlock.content === 'object' ? { ...nextBlock.content } : { runs: [] };
    nextBlock.content.type = 'list';
    nextBlock.content.styleId = ordered ? (format === 'legal' ? 'legal-numbered-list' : 'numbered-list') : 'bullet-list';
    nextBlock.content.styleName = ordered ? (format === 'legal' ? 'Legal Numbering' : 'Numbered List') : 'Bullet List';
    nextBlock.content.list = normalizeCommandList(nextBlock.content.list, ordered);
    const level = Math.max(0, Number(nextBlock.content.list.indentLevel || 0) || 0);
    nextBlock.content.list.ordered = ordered;
    nextBlock.content.list.numberingId = format === 'legal'
        ? DEFAULT_LEGAL_NUMBERING_ID
        : ordered ? DEFAULT_NUMBERED_NUMBERING_ID : DEFAULT_BULLET_NUMBERING_ID;
    nextBlock.content.list.abstractNumberingId = nextBlock.content.list.numberingId;
    nextBlock.content.list.numberFormat = format;
    nextBlock.content.list.levelText = format === 'legal'
        ? Array.from({ length: level + 1 }, (_unused, index) => `%${index + 1}`).join('.') + '.'
        : ordered ? `%${level + 1}.` : '';
    nextBlock.content.list.suffix = 'tab';
    nextBlock.content.list.labelIndent = level * 24;
    nextBlock.content.list.hangingIndent = 24;
    return { changed: JSON.stringify(source) !== JSON.stringify(nextBlock), block: nextBlock };
}

function setNumberingRestart(block, argument) {
    if (String(block?.type || block?.content?.type || '').toLowerCase() !== 'list') {
        return { changed: false, block };
    }

    const nextBlock = clone(block);
    nextBlock.content = { ...(nextBlock.content || {}) };
    nextBlock.content.list = normalizeCommandList(nextBlock.content.list, nextBlock.content.list?.ordered === true || nextBlock.content.list?.Ordered === true);
    const value = numberingValueArgument(argument);
    nextBlock.content.list.restartNumbering = true;
    nextBlock.content.list.continueNumbering = false;
    if (value) {
        nextBlock.content.list.numberingValue = value;
        nextBlock.content.list.startNumber = value;
    }

    return { changed: JSON.stringify(block.content?.list || {}) !== JSON.stringify(nextBlock.content.list), block: nextBlock };
}

function setNumberingContinue(block) {
    if (String(block?.type || block?.content?.type || '').toLowerCase() !== 'list') {
        return { changed: false, block };
    }

    const nextBlock = clone(block);
    nextBlock.content = { ...(nextBlock.content || {}) };
    nextBlock.content.list = normalizeCommandList(nextBlock.content.list, nextBlock.content.list?.ordered === true || nextBlock.content.list?.Ordered === true);
    nextBlock.content.list.restartNumbering = false;
    nextBlock.content.list.continueNumbering = true;
    delete nextBlock.content.list.numberingValue;
    return { changed: JSON.stringify(block.content?.list || {}) !== JSON.stringify(nextBlock.content.list), block: nextBlock };
}

function setNumberingValue(block, argument) {
    if (String(block?.type || block?.content?.type || '').toLowerCase() !== 'list') {
        return { changed: false, block };
    }

    const value = numberingValueArgument(argument);
    if (!value) {
        return { changed: false, block };
    }

    const nextBlock = clone(block);
    nextBlock.content = { ...(nextBlock.content || {}) };
    nextBlock.content.list = normalizeCommandList(nextBlock.content.list, nextBlock.content.list?.ordered === true || nextBlock.content.list?.Ordered === true);
    nextBlock.content.list.numberingValue = value;
    nextBlock.content.list.startNumber = value;
    nextBlock.content.list.restartNumbering = true;
    nextBlock.content.list.continueNumbering = false;
    return { changed: JSON.stringify(block.content?.list || {}) !== JSON.stringify(nextBlock.content.list), block: nextBlock };
}

function applyListStyle(block, styleId, numberingId) {
    const source = String(block?.type || block?.content?.type || '').toLowerCase() === 'list'
        ? block
        : toggleList(block, true).block;
    const nextBlock = clone(source);
    nextBlock.content = { ...(nextBlock.content || {}) };
    nextBlock.content.list = normalizeCommandList(nextBlock.content.list, true);
    nextBlock.content.list.listStyleId = styleId;
    nextBlock.content.list.numberingId = numberingId || nextBlock.content.list.numberingId || DEFAULT_NUMBERED_NUMBERING_ID;
    nextBlock.content.list.abstractNumberingId = nextBlock.content.list.numberingId;
    nextBlock.content.styleId = styleId;
    return { changed: JSON.stringify(source) !== JSON.stringify(nextBlock), block: nextBlock };
}

function ensureDefaultNumberingDefinitions(model) {
    if (!model || typeof model !== 'object') {
        return;
    }

    const definitions = Array.isArray(model.numberingDefinitions) ? model.numberingDefinitions : [];
    const byId = new Map(definitions.map(definition => [String(definition?.id || definition?.Id || ''), definition]));
    for (const definition of [
        createDefaultNumberedDefinition(),
        createDefaultBulletDefinition(),
        createDefaultLegalDefinition(),
    ]) {
        if (!byId.has(definition.id)) {
            definitions.push(definition);
        }
    }

    model.numberingDefinitions = definitions;
    if (!Array.isArray(model.listStyles)) {
        model.listStyles = [];
    }

    ensureBuiltInListStyle(model, 'numbered-list', 'Numbered List', DEFAULT_NUMBERED_NUMBERING_ID);
    ensureBuiltInListStyle(model, 'bullet-list', 'Bullet List', DEFAULT_BULLET_NUMBERING_ID);
    ensureBuiltInListStyle(model, 'legal-numbered-list', 'Legal Numbering', DEFAULT_LEGAL_NUMBERING_ID);
}

function ensureBuiltInListStyle(model, id, name, numberingId) {
    if (model.listStyles.some(style => String(style?.id || style?.Id || '') === id)) {
        return;
    }

    model.listStyles.push({ id, name, numberingId, isQuickStyle: true });
}

function ensureListStyle(model, argument) {
    if (!Array.isArray(model.listStyles)) {
        model.listStyles = [];
    }

    const raw = typeof argument === 'string' ? { id: argument, name: argument } : (argument || {});
    const requestedId = String(raw.id ?? raw.Id ?? raw.styleId ?? raw.StyleId ?? raw.value ?? '').trim();
    const id = requestedId || `list-style-${model.listStyles.length + 1}`;
    const existing = model.listStyles.find(style => String(style?.id || style?.Id || '') === id);
    if (existing) {
        return {
            id: String(existing.id || existing.Id || id),
            numberingId: String(existing.numberingId || existing.NumberingId || DEFAULT_NUMBERED_NUMBERING_ID),
        };
    }

    const format = listFormatArgument(raw.format ?? raw.Format ?? raw.numberFormat ?? raw.NumberFormat ?? 'decimal');
    const numberingId = format === 'legal' ? DEFAULT_LEGAL_NUMBERING_ID : format === 'bullet' ? DEFAULT_BULLET_NUMBERING_ID : DEFAULT_NUMBERED_NUMBERING_ID;
    const style = {
        id,
        name: String(raw.name ?? raw.Name ?? id),
        numberingId,
        isQuickStyle: raw.isQuickStyle === true || raw.IsQuickStyle === true,
    };
    model.listStyles.push(style);
    return style;
}

function findListStyle(model, argument) {
    const id = String(typeof argument === 'string' ? argument : (argument?.id ?? argument?.Id ?? argument?.styleId ?? argument?.StyleId ?? argument?.value ?? '')).trim();
    if (!id || !Array.isArray(model?.listStyles)) {
        return null;
    }

    const style = model.listStyles.find(item => String(item?.id || item?.Id || '') === id);
    return style
        ? {
            id: String(style.id || style.Id || id),
            numberingId: String(style.numberingId || style.NumberingId || DEFAULT_NUMBERED_NUMBERING_ID),
        }
        : null;
}

function normalizeCommandList(list, ordered) {
    const source = list && typeof list === 'object' ? list : {};
    const level = Math.max(0, Math.min(8, Number(source.indentLevel ?? source.IndentLevel ?? 0) || 0));
    const numberingId = ordered
        ? String(source.numberingId ?? source.NumberingId ?? DEFAULT_NUMBERED_NUMBERING_ID)
        : String(source.numberingId ?? source.NumberingId ?? DEFAULT_BULLET_NUMBERING_ID);
    return {
        ...source,
        ordered,
        indentLevel: level,
        startNumber: Math.max(1, Number(source.startNumber ?? source.StartNumber ?? 1) || 1),
        numberingId,
        abstractNumberingId: String(source.abstractNumberingId ?? source.AbstractNumberingId ?? numberingId),
        listStyleId: String(source.listStyleId ?? source.ListStyleId ?? (ordered ? 'numbered-list' : 'bullet-list')),
        numberFormat: String(source.numberFormat ?? source.NumberFormat ?? (ordered ? 'decimal' : 'bullet')),
        levelText: String(source.levelText ?? source.LevelText ?? (ordered ? `%${level + 1}.` : '')),
        suffix: String(source.suffix ?? source.Suffix ?? 'tab'),
        labelIndent: Math.max(0, Number(source.labelIndent ?? source.LabelIndent ?? (level * 24)) || 0),
        hangingIndent: Math.max(0, Number(source.hangingIndent ?? source.HangingIndent ?? 24) || 24),
    };
}

function listFormatArgument(argument) {
    const raw = typeof argument === 'string' || typeof argument === 'number'
        ? String(argument)
        : String(argument?.format ?? argument?.Format ?? argument?.numberFormat ?? argument?.NumberFormat ?? argument?.value ?? '');
    const normalized = formatName(raw);
    if (normalized === 'bullet' || normalized === 'bullets' || normalized === 'unordered') return 'bullet';
    if (normalized === 'legal' || normalized === 'legalnumbering') return 'legal';
    if (normalized === 'lowerletter' || normalized === 'loweralpha') return 'lowerLetter';
    if (normalized === 'upperletter' || normalized === 'upperalpha') return 'upperLetter';
    if (normalized === 'lowerroman') return 'lowerRoman';
    if (normalized === 'upperroman') return 'upperRoman';
    if (normalized === 'decimalzero' || normalized === 'decimalleadingzero') return 'decimalZero';
    return 'decimal';
}

function numberingValueArgument(argument) {
    const value = typeof argument === 'number'
        ? argument
        : Number(argument?.value ?? argument?.Value ?? argument?.numberingValue ?? argument?.NumberingValue ?? argument?.startNumber ?? argument?.StartNumber ?? argument);
    return Number.isFinite(value) && value > 0 ? Math.trunc(value) : null;
}

function changeListLevel(block, direction) {
    if (String(block?.type || block?.content?.type || '').toLowerCase() !== 'list') {
        return { changed: false, block };
    }

    const nextBlock = clone(block);
    nextBlock.content = nextBlock.content && typeof nextBlock.content === 'object' ? { ...nextBlock.content } : { runs: [] };
    nextBlock.content.list = nextBlock.content.list && typeof nextBlock.content.list === 'object'
        ? { ...nextBlock.content.list }
        : { ordered: false, indentLevel: 0, startNumber: 1 };
    const before = Math.max(0, Number(nextBlock.content.list.indentLevel || 0) || 0);
    nextBlock.content.list.indentLevel = Math.max(0, Math.min(8, before + direction));
    nextBlock.content.list.labelIndent = nextBlock.content.list.indentLevel * 24;
    if (nextBlock.content.list.ordered === true) {
        nextBlock.content.list.levelText = `%${nextBlock.content.list.indentLevel + 1}.`;
    }

    return {
        changed: nextBlock.content.list.indentLevel !== before,
        block: nextBlock,
    };
}

function selectedBlocksForSelection(model, selection) {
    const blocks = orderedBlocks(model);
    return selectedBlockIndexes(blocks, selection).map(index => blocks[index]).filter(Boolean);
}

function selectedBlockIndexes(blocks, selection) {
    if (!Array.isArray(blocks) || blocks.length === 0) {
        return [];
    }

    const anchorId = selection?.anchor?.blockId || selection?.focus?.blockId || blocks[0]?.id;
    const focusId = selection?.focus?.blockId || anchorId;
    const anchorIndex = blocks.findIndex(block => String(block?.id || '') === String(anchorId || ''));
    const focusIndex = blocks.findIndex(block => String(block?.id || '') === String(focusId || ''));
    if (anchorIndex < 0 && focusIndex < 0) {
        return [0];
    }

    const start = Math.min(anchorIndex < 0 ? focusIndex : anchorIndex, focusIndex < 0 ? anchorIndex : focusIndex);
    const end = Math.max(anchorIndex < 0 ? focusIndex : anchorIndex, focusIndex < 0 ? anchorIndex : focusIndex);
    const indexes = [];
    for (let index = start; index <= end; index += 1) {
        indexes.push(index);
    }

    return indexes;
}

function orderedBlocks(model) {
    return orderedBlockEntries(model).map(item => item.block);
}

function orderedBlockEntries(model) {
    const entries = [];
    appendEditableEntries(Array.isArray(model?.body?.blocks) ? model.body.blocks : [], entries);
    return entries
        .sort((left, right) => {
            const order = (Number(left.block?.order) || 0) - (Number(right.block?.order) || 0);
            return order !== 0 ? order : String(left.block?.id || '').localeCompare(String(right.block?.id || ''));
        });
}

function appendEditableEntries(blocks, entries) {
    if (!Array.isArray(blocks)) {
        return;
    }

    blocks.forEach((block, index) => {
        if (isEditableTextBlock(block)) {
            entries.push({ block, list: blocks, index });
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

function syncSectionBlocks(model, dirtyBlockIds) {
    if (!Array.isArray(model?.sections) || !Array.isArray(model?.body?.blocks)) {
        return;
    }

    const byId = new Map(model.body.blocks.map(block => [String(block?.id || ''), block]));
    for (const section of model.sections) {
        if (!Array.isArray(section.blocks)) {
            continue;
        }

        section.blocks = section.blocks.map(block => dirtyBlockIds.has(String(block?.id || ''))
            ? clone(byId.get(String(block?.id || '')) || block)
            : block);
    }
}

function readParagraphProperties(block) {
    const source = block?.paragraphProperties && typeof block.paragraphProperties === 'object'
        ? block.paragraphProperties
        : {};
    return {
        ...source,
        alignment: source.alignment ?? source.Alignment ?? 0,
        lineSpacing: source.lineSpacing ?? source.LineSpacing ?? 1,
        spacingBefore: source.spacingBefore ?? source.SpacingBefore ?? 0,
        spacingAfter: source.spacingAfter ?? source.SpacingAfter ?? 0,
        leftIndent: source.leftIndent ?? source.LeftIndent ?? 0,
        rightIndent: source.rightIndent ?? source.RightIndent ?? 0,
        firstLineIndent: source.firstLineIndent ?? source.FirstLineIndent ?? 0,
        defaultTabWidth: source.defaultTabWidth ?? source.DefaultTabWidth ?? 36,
        tabStops: normalizeCommandTabStops(source.tabStops ?? source.TabStops ?? []),
    };
}

function normalizeCommandTabStops(stops) {
    return Array.isArray(stops)
        ? stops.map(normalizeCommandTabStop).filter(Boolean).sort((left, right) => left.position - right.position)
        : [];
}

function alignmentValue(argument) {
    return alignmentEnum(readCommandText(argument, 'alignment'));
}

function alignmentEnum(value) {
    const normalized = alignmentName(value);
    return { left: 0, center: 1, right: 2, justify: 3 }[normalized] ?? 0;
}

function alignmentName(value) {
    if (typeof value === 'number') {
        return ['left', 'center', 'right', 'justify'][Math.max(0, Math.min(3, Math.trunc(value)))] || 'left';
    }

    const normalized = String(value || '').toLowerCase();
    if (normalized === 'center' || normalized === 'middle') return 'center';
    if (normalized === 'right' || normalized === 'end') return 'right';
    if (normalized === 'justify' || normalized === 'justified' || normalized === 'block') return 'justify';
    return 'left';
}

function resolveStyleArgument(argument) {
    if (typeof argument === 'string') {
        return argument;
    }

    const value = argument?.styleName || argument?.style || argument?.value || argument?.commandId || argument;
    if (typeof value === 'string' && /^heading[1-6]$/i.test(value.replace(/[\s_-]/g, ''))) {
        return `Heading ${value.match(/[1-6]/)?.[0] || 1}`;
    }

    return String(value || 'Normal');
}

function readCommandValue(argument, key) {
    if (typeof argument === 'number') {
        return argument;
    }

    if (typeof argument === 'string') {
        return Number(argument);
    }

    return Number(argument?.[key] ?? argument?.value ?? argument?.Value ?? 0);
}

function readCommandText(argument, key) {
    if (typeof argument === 'string' || typeof argument === 'number') {
        return String(argument);
    }

    return String(argument?.[key] ?? argument?.value ?? argument?.Value ?? '');
}

function listState(block) {
    const type = String(block?.type || block?.content?.type || '').toLowerCase();
    if (type !== 'list') {
        return 'none';
    }

    return block?.content?.list?.ordered ? 'numbered' : 'bullet';
}

function aggregate(blocks, selector) {
    if (!Array.isArray(blocks) || blocks.length === 0) {
        return { value: null, mixed: false };
    }

    const values = blocks.map(selector);
    const first = values[0];
    return {
        value: first,
        mixed: values.some(value => value !== first),
    };
}

function aggregateNumber(blocks, selector) {
    const result = aggregate(blocks, selector);
    return {
        value: result.value == null ? null : Number(result.value),
        mixed: result.mixed,
    };
}

function commandToggleState(active, mixed) {
    return {
        disabled: false,
        active: active === true,
        mixed: mixed === true,
        value: null,
        state: mixed ? 'mixed' : active ? 'active' : 'inactive',
    };
}

function commandValueState(value, mixed) {
    return {
        disabled: false,
        active: value != null && value !== false,
        mixed: mixed === true,
        value,
        state: mixed ? 'mixed' : value != null && value !== false ? 'active' : 'inactive',
    };
}

function isEditableTextBlock(block) {
    const type = String(block?.type || block?.content?.type || '').toLowerCase();
    return type === 'paragraph' || type === 'heading' || type === 'list' || type === 'quote';
}

function positiveNumber(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function nonNegativeNumber(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
}

function clampNumber(value, min, max) {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) {
        return min;
    }

    return Math.max(min, Math.min(max, parsed));
}

function normalizeParagraphPropertyValue(value) {
    return Number.isFinite(Number(value)) ? Number(value) : String(value ?? '');
}

function unchanged(model, selection, state) {
    return {
        changed: false,
        model,
        selection,
        state,
        dirtyBlockIds: [],
        formattingState: queryParagraphCommandState(model, selection, state),
    };
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
