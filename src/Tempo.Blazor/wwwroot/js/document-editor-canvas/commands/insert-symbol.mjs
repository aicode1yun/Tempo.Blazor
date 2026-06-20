import { applyCanvasTextEdit } from '../input/text-editing.mjs';

const SYMBOL_COMMANDS = new Map([
    ['insertsymbol', 'insertSymbol'],
    ['insertspecialcharacter', 'insertSymbol'],
    ['insertemoji', 'insertEmoji'],
    ['insertemdash', 'insertEmDash'],
    ['inserten dash', 'insertEnDash'],
    ['insertendash', 'insertEnDash'],
    ['insertnonbreakingspace', 'insertNonBreakingSpace'],
    ['insertnbsp', 'insertNonBreakingSpace'],
    ['insertoptionalhyphen', 'insertOptionalHyphen'],
]);

const SYMBOLS = new Map([
    ['emdash', '—'],
    ['emDash', '—'],
    ['insertEmDash', '—'],
    ['endash', '–'],
    ['enDash', '–'],
    ['insertEnDash', '–'],
    ['nbsp', '\u00A0'],
    ['nonbreakingspace', '\u00A0'],
    ['nonBreakingSpace', '\u00A0'],
    ['insertNonBreakingSpace', '\u00A0'],
    ['optionalhyphen', '\u00AD'],
    ['optionalHyphen', '\u00AD'],
    ['insertOptionalHyphen', '\u00AD'],
]);

export function isInsertSymbolCommand(commandId) {
    return SYMBOL_COMMANDS.has(normalizeCommandId(commandId));
}

export function canonicalInsertSymbolCommandId(commandId) {
    return SYMBOL_COMMANDS.get(normalizeCommandId(commandId)) || 'insertSymbol';
}

export function applyInsertSymbolCommand(model, selection, commandId, argument = null) {
    const canonical = canonicalInsertSymbolCommandId(commandId);
    const text = resolveSymbolText(canonical, argument);
    if (!text) {
        return {
            changed: false,
            model,
            selection,
            operation: canonical,
            dirtyBlockIds: [],
        };
    }

    const result = applyCanvasTextEdit(model, selection, {
        type: 'insertText',
        text,
        source: canonical,
    });

    return {
        ...result,
        operation: canonical,
        insertedText: text,
        dirtyBlockIds: result.dirtyBlockIds || [],
    };
}

export function queryInsertSymbolCommandState(model, selection) {
    const disabled = !selection?.focus?.blockId || !Array.isArray(model?.body?.blocks);
    const commands = {};
    for (const command of ['insertSymbol', 'insertEmoji', 'insertEmDash', 'insertEnDash', 'insertNonBreakingSpace', 'insertOptionalHyphen']) {
        commands[command.toLowerCase()] = {
            disabled,
            active: false,
            mixed: false,
            value: null,
            state: disabled ? 'disabled' : 'inactive',
        };
    }

    return { commands };
}

function resolveSymbolText(canonical, argument) {
    if (typeof argument === 'string' && argument.length > 0) {
        return normalizeSymbolArgument(argument);
    }

    if (argument && typeof argument === 'object') {
        const explicit = argument.text ?? argument.symbol ?? argument.emoji ?? argument.value;
        if (explicit != null) {
            return normalizeSymbolArgument(explicit);
        }

        const codePoint = argument.codePoint ?? argument.codepoint;
        if (codePoint != null) {
            const parsed = typeof codePoint === 'number'
                ? codePoint
                : Number.parseInt(String(codePoint).replace(/^U\+/iu, ''), 16);
            if (Number.isFinite(parsed) && parsed > 0) {
                return String.fromCodePoint(parsed);
            }
        }
    }

    return SYMBOLS.get(canonical) || '';
}

function normalizeSymbolArgument(value) {
    const text = String(value);
    const mapped = SYMBOLS.get(text) || SYMBOLS.get(text.replace(/[\s_-]/g, ''));
    return mapped || text;
}

function normalizeCommandId(commandId) {
    return String(commandId == null ? '' : commandId).replace(/[\s_-]/g, '').toLowerCase();
}
