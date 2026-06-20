import { applyCanvasTextEdit, canvasBlockText } from './text-editing.mjs';
import {
    DEFAULT_BULLET_NUMBERING_ID,
    DEFAULT_NUMBERED_NUMBERING_ID,
    createDefaultBulletDefinition,
    createDefaultNumberedDefinition,
} from '../lists/numbering-definition.mjs';

const WORD_BOUNDARY = /[\s.,;:!?()[\]{}"']/u;
const AUTOCORRECT_REPLACEMENTS = new Map([
    ['(c)', '©'],
    ['(r)', '®'],
    ['(tm)', '™'],
    ['...', '…'],
]);
const FRACTION_REPLACEMENTS = new Map([
    ['1/2', '½'],
    ['1/4', '¼'],
    ['3/4', '¾'],
]);
const ORDINAL_SUFFIXES = new Map([
    ['st', 'ˢᵗ'],
    ['nd', 'ⁿᵈ'],
    ['rd', 'ʳᵈ'],
    ['th', 'ᵗʰ'],
]);
const URL_PATTERN = /\bhttps?:\/\/[^\s<>"']+$/i;

export function applyAutocorrectAfterTextInput(input = {}) {
    const options = normalizeAutocorrectOptions(input.options || input.autocorrect || {});
    if (!options.enabled) {
        return unchanged(input);
    }

    const edit = input.edit || {};
    if (edit.type !== 'insertText') {
        return unchanged(input);
    }

    const text = String(edit.text ?? '');
    if (!text) {
        return unchanged(input);
    }

    // Fast path for plain typing: the eager clones below copy the WHOLE model 2x per keystroke and were
    // all wasted whenever no rule fires — the common case. The precheck mirrors the rule guards read-only
    // against the original model and skips the clones entirely when no rule can match here.
    if (!couldAutocorrect(input.model, input.selection, text, options)) {
        return unchanged(input);
    }

    let model = clone(input.model);
    let selection = clone(input.selection);
    const undoBeforeModel = clone(input.model);
    const undoBeforeSelection = clone(input.selection);
    const operations = [];

    const quote = options.smartQuotes ? maybeSmartQuote(model, selection, text) : unchanged({ model, selection });
    if (quote.changed) {
        model = quote.model;
        selection = quote.selection;
        operations.push(quote.operation);
    }

    const caseChange = options.autoCapitalize ? maybeAutoCapitalize(model, selection, text) : unchanged({ model, selection });
    if (caseChange.changed) {
        model = caseChange.model;
        selection = caseChange.selection;
        operations.push(caseChange.operation);
    }

    const dash = options.emDash ? maybeEmDash(model, selection) : unchanged({ model, selection });
    if (dash.changed) {
        model = dash.model;
        selection = dash.selection;
        operations.push(dash.operation);
    }

    const replacement = (options.replacementTable || options.fractions || options.ordinals)
        ? maybeBoundaryReplacement(model, selection, text, options)
        : unchanged({ model, selection });
    if (replacement.changed) {
        model = replacement.model;
        selection = replacement.selection;
        operations.push(replacement.operation);
    }

    const autoformat = maybeAutoformat(model, selection, text, options);
    if (autoformat.changed) {
        model = autoformat.model;
        selection = autoformat.selection;
        operations.push(autoformat.operation);
    }

    if (operations.length === 0) {
        return unchanged(input);
    }

    return {
        changed: true,
        model,
        selection,
        undoBeforeModel,
        undoBeforeSelection,
        operation: operations.at(-1) || 'autocorrect',
        operations,
        dirtyBlockIds: [selection?.focus?.blockId].filter(Boolean),
    };
}

export function normalizeAutocorrectOptions(options = {}) {
    return {
        enabled: options.enabled !== false,
        smartQuotes: options.smartQuotes !== false,
        autoCapitalize: options.autoCapitalize !== false,
        replacementTable: options.replacementTable !== false,
        fractions: options.fractions !== false,
        ordinals: options.ordinals !== false,
        emDash: options.emDash !== false,
        autoLists: options.autoLists !== false,
        autoLinks: options.autoLinks !== false,
        horizontalRule: options.horizontalRule !== false,
    };
}

function unchanged(input) {
    return {
        changed: false,
        model: input.model,
        selection: input.selection,
        operation: null,
        operations: [],
        dirtyBlockIds: [],
    };
}

/// Read-only mirror of the rule guards below — returns false only when NO rule can fire for this typed
/// text at the caret, so the caller may skip the expensive model clones. Must stay conservative: any rule
/// trigger change below needs a matching update here (the parity test in autocorrect tests pins this).
function couldAutocorrect(model, selection, typedText, options) {
    // maybeBoundaryReplacement triggers on boundary characters; maybeAutoformat on whitespace. Multi-char
    // inserts (IME commit, programmatic) containing either also take the slow path.
    if (WORD_BOUNDARY.test(typedText) || /\s/u.test(typedText)) {
        return true;
    }

    // maybeSmartQuote triggers only on a lone quote character (covered by WORD_BOUNDARY above, kept for
    // clarity should the boundary set ever change).
    if (typedText === '"' || typedText === "'") {
        return true;
    }

    const blockId = selection?.focus?.blockId || '';
    const offset = Number(selection?.focus?.offset ?? 0) || 0;
    if (!blockId || offset <= 0) {
        return false;
    }

    const needsEmDash = options.emDash && offset >= 2;
    const needsCapitalize = options.autoCapitalize && /^[a-z]$/u.test(typedText);
    if (!needsEmDash && !needsCapitalize) {
        return false;
    }

    const blockText = canvasBlockText(model, blockId);
    if (needsEmDash && blockText.slice(offset - 2, offset) === '--') {
        return true;
    }

    if (needsCapitalize) {
        const beforeLetter = blockText.slice(0, Math.max(0, offset - 1));
        if (beforeLetter.length === 0 || /[.!?]\s+$/u.test(beforeLetter)) {
            return true;
        }
    }

    return false;
}

function maybeSmartQuote(model, selection, typedText) {
    if (typedText !== '"' && typedText !== "'") {
        return unchanged({ model, selection });
    }

    const blockId = selection?.focus?.blockId || '';
    const offset = Number(selection?.focus?.offset ?? 0) || 0;
    if (!blockId || offset <= 0) {
        return unchanged({ model, selection });
    }

    const text = canvasBlockText(model, blockId);
    const beforeQuote = text.slice(0, Math.max(0, offset - 1));
    const opening = beforeQuote.length === 0 || /[\s([{"'“‘]$/u.test(beforeQuote);
    const replacement = typedText === '"'
        ? opening ? '“' : '”'
        : opening ? '‘' : '’';

    return replaceWithinBlock(model, blockId, offset - 1, offset, replacement, 'smartQuote');
}

function maybeAutoCapitalize(model, selection, typedText) {
    if (!/^[a-z]$/u.test(typedText)) {
        return unchanged({ model, selection });
    }

    const blockId = selection?.focus?.blockId || '';
    const offset = Number(selection?.focus?.offset ?? 0) || 0;
    if (!blockId || offset <= 0) {
        return unchanged({ model, selection });
    }

    const text = canvasBlockText(model, blockId);
    const beforeLetter = text.slice(0, Math.max(0, offset - 1));
    if (beforeLetter.length > 0 && !/[.!?]\s+$/u.test(beforeLetter)) {
        return unchanged({ model, selection });
    }

    return replaceWithinBlock(model, blockId, offset - 1, offset, typedText.toLocaleUpperCase(), 'autoCapitalize');
}

function maybeEmDash(model, selection) {
    const blockId = selection?.focus?.blockId || '';
    const offset = Number(selection?.focus?.offset ?? 0) || 0;
    if (!blockId || offset < 2) {
        return unchanged({ model, selection });
    }

    const text = canvasBlockText(model, blockId);
    if (text.slice(offset - 2, offset) !== '--') {
        return unchanged({ model, selection });
    }

    return replaceWithinBlock(model, blockId, offset - 2, offset, '—', 'emDash');
}

function maybeBoundaryReplacement(model, selection, typedText, options) {
    if (!WORD_BOUNDARY.test(typedText)) {
        return unchanged({ model, selection });
    }

    const blockId = selection?.focus?.blockId || '';
    const offset = Number(selection?.focus?.offset ?? 0) || 0;
    if (!blockId || offset <= typedText.length) {
        return unchanged({ model, selection });
    }

    const text = canvasBlockText(model, blockId);
    const boundaryStart = offset - typedText.length;
    const beforeBoundary = text.slice(0, boundaryStart);
    const tokenMatch = beforeBoundary.match(/([^\s]+)$/u);
    const token = tokenMatch?.[1] || '';
    if (!token) {
        return unchanged({ model, selection });
    }

    const replacement = (options.replacementTable ? AUTOCORRECT_REPLACEMENTS.get(token.toLowerCase()) : null)
        || (options.fractions ? FRACTION_REPLACEMENTS.get(token) : null)
        || (options.ordinals ? ordinalReplacement(token) : null);
    if (!replacement || replacement === token) {
        return unchanged({ model, selection });
    }

    return replaceWithinBlock(model, blockId, boundaryStart - token.length, boundaryStart, replacement, 'replaceAsYouType');
}

function maybeAutoformat(model, selection, typedText, options) {
    if (!/\s/u.test(typedText)) {
        return unchanged({ model, selection });
    }

    const blockId = selection?.focus?.blockId || '';
    const offset = Number(selection?.focus?.offset ?? 0) || 0;
    if (!blockId) {
        return unchanged({ model, selection });
    }

    const text = canvasBlockText(model, blockId);
    const prefix = text.slice(0, offset);
    if (options.autoLists && /^(\d+)\.\s$/u.test(prefix)) {
        return convertMarkerToList(model, blockId, prefix.length, true);
    }

    if (options.autoLists && /^[-*]\s$/u.test(prefix)) {
        return convertMarkerToList(model, blockId, prefix.length, false);
    }

    const link = options.autoLinks ? maybeAutoLink(model, selection, text, offset) : unchanged({ model, selection });
    if (link.changed) {
        return link;
    }

    if (options.horizontalRule && /^---\s$/u.test(prefix)) {
        return convertMarkerToHorizontalRule(model, blockId, prefix.length);
    }

    return unchanged({ model, selection });
}

function maybeAutoLink(model, selection, text, offset) {
    const boundaryStart = Math.max(0, offset - 1);
    const beforeBoundary = text.slice(0, boundaryStart);
    const match = beforeBoundary.match(URL_PATTERN);
    const url = match?.[0] || '';
    if (!url) {
        return unchanged({ model, selection });
    }

    const start = boundaryStart - url.length;
    const linked = replaceWithinBlock(model, selection.focus.blockId, start, boundaryStart, url, 'autoHyperlink', [{ type: 'link', value: url, link: { href: url } }]);
    return linked.changed
        ? { ...linked, selection: { anchor: { blockId: selection.focus.blockId, offset }, focus: { blockId: selection.focus.blockId, offset } } }
        : linked;
}

function ordinalReplacement(token) {
    const match = String(token || '').match(/^(\d+)(st|nd|rd|th)$/iu);
    if (!match) {
        return null;
    }

    return `${match[1]}${ORDINAL_SUFFIXES.get(match[2].toLowerCase()) || match[2]}`;
}

function replaceWithinBlock(model, blockId, start, end, text, operation, marks = []) {
    const replaced = applyCanvasTextEdit(model, { anchor: { blockId, offset: start }, focus: { blockId, offset: end } }, {
        type: 'replaceRange',
        range: { anchor: { blockId, offset: start }, focus: { blockId, offset: end } },
        text,
        marks,
        source: operation,
    });
    if (!replaced.changed) {
        return unchanged({ model, selection: { anchor: { blockId, offset: end }, focus: { blockId, offset: end } } });
    }

    return {
        changed: true,
        model: replaced.model,
        selection: replaced.selection,
        operation,
        dirtyBlockIds: replaced.dirtyBlockIds || [blockId],
    };
}

function convertMarkerToList(model, blockId, markerLength, ordered) {
    const deleted = applyCanvasTextEdit(model, { anchor: { blockId, offset: 0 }, focus: { blockId, offset: markerLength } }, {
        type: 'replaceRange',
        range: { anchor: { blockId, offset: 0 }, focus: { blockId, offset: markerLength } },
        text: '',
        source: ordered ? 'autoNumberList' : 'autoBulletList',
    });
    if (!deleted.changed) {
        return unchanged({ model, selection: { anchor: { blockId, offset: markerLength }, focus: { blockId, offset: markerLength } } });
    }

    const next = clone(deleted.model);
    ensureDefaultListData(next);
    const block = findBlock(next, blockId);
    if (!block) {
        return unchanged({ model, selection: deleted.selection });
    }

    const numberingId = ordered ? DEFAULT_NUMBERED_NUMBERING_ID : DEFAULT_BULLET_NUMBERING_ID;
    block.type = 'list';
    block.content = block.content && typeof block.content === 'object' ? block.content : { runs: [] };
    block.content.type = 'list';
    block.content.headingLevel = null;
    block.content.outlineLevel = null;
    block.content.styleId = ordered ? 'numbered-list' : 'bullet-list';
    block.content.styleName = ordered ? 'Numbered List' : 'Bullet List';
    block.content.list = {
        ordered,
        indentLevel: 0,
        startNumber: 1,
        numberingId,
        abstractNumberingId: numberingId,
        listStyleId: ordered ? 'numbered-list' : 'bullet-list',
        numberFormat: ordered ? 'decimal' : 'bullet',
        levelText: ordered ? '%1.' : '',
        suffix: 'tab',
        labelIndent: 0,
        hangingIndent: 24,
    };
    syncSections(next, blockId);
    next.version = Number(next.version || 0) + 1;
    return {
        changed: true,
        model: next,
        selection: { anchor: { blockId, offset: 0 }, focus: { blockId, offset: 0 } },
        operation: ordered ? 'autoNumberList' : 'autoBulletList',
        dirtyBlockIds: [blockId],
    };
}

function convertMarkerToHorizontalRule(model, blockId, markerLength) {
    const deleted = applyCanvasTextEdit(model, { anchor: { blockId, offset: 0 }, focus: { blockId, offset: markerLength } }, {
        type: 'replaceRange',
        range: { anchor: { blockId, offset: 0 }, focus: { blockId, offset: markerLength } },
        text: '',
        source: 'autoHorizontalRule',
    });
    if (!deleted.changed) {
        return unchanged({ model, selection: { anchor: { blockId, offset: markerLength }, focus: { blockId, offset: markerLength } } });
    }

    const next = clone(deleted.model);
    const block = findBlock(next, blockId);
    if (!block) {
        return unchanged({ model, selection: deleted.selection });
    }

    block.paragraphProperties = {
        ...(block.paragraphProperties || {}),
        horizontalRule: true,
        borderBottom: {
            style: 'single',
            width: 1,
        },
    };
    syncSections(next, blockId);
    next.version = Number(next.version || 0) + 1;
    return {
        changed: true,
        model: next,
        selection: { anchor: { blockId, offset: 0 }, focus: { blockId, offset: 0 } },
        operation: 'autoHorizontalRule',
        dirtyBlockIds: [blockId],
    };
}

function ensureDefaultListData(model) {
    const definitions = Array.isArray(model.numberingDefinitions) ? model.numberingDefinitions : [];
    const byId = new Set(definitions.map(item => String(item?.id || item?.Id || '')));
    if (!byId.has(DEFAULT_NUMBERED_NUMBERING_ID)) {
        definitions.push(createDefaultNumberedDefinition());
    }
    if (!byId.has(DEFAULT_BULLET_NUMBERING_ID)) {
        definitions.push(createDefaultBulletDefinition());
    }
    model.numberingDefinitions = definitions;
    model.listStyles = Array.isArray(model.listStyles) ? model.listStyles : [];
    ensureListStyle(model, 'numbered-list', 'Numbered List', DEFAULT_NUMBERED_NUMBERING_ID);
    ensureListStyle(model, 'bullet-list', 'Bullet List', DEFAULT_BULLET_NUMBERING_ID);
}

function ensureListStyle(model, id, name, numberingId) {
    if (!model.listStyles.some(style => String(style?.id || style?.Id || '') === id)) {
        model.listStyles.push({ id, name, numberingId, isQuickStyle: true });
    }
}

function findBlock(model, blockId) {
    const id = String(blockId || '');
    const stack = [...(model?.body?.blocks || [])];
    while (stack.length > 0) {
        const block = stack.shift();
        if (String(block?.id || '') === id) {
            return block;
        }
        for (const row of block?.content?.table?.rows || []) {
            for (const cell of row?.cells || []) {
                stack.push(...(cell?.blocks || []));
            }
        }
        stack.push(...(block?.content?.contentControl?.blocks || []));
    }
    return null;
}

function syncSections(model, blockId) {
    const replacement = findBlock(model, blockId);
    if (!replacement || !Array.isArray(model?.sections)) {
        return;
    }
    for (const section of model.sections) {
        if (!Array.isArray(section?.blocks)) {
            continue;
        }
        for (let index = 0; index < section.blocks.length; index += 1) {
            if (String(section.blocks[index]?.id || '') === String(blockId)) {
                section.blocks[index] = clone(replacement);
            }
        }
    }
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
