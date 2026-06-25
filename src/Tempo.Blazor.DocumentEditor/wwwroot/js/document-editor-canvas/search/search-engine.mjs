import { applyCanvasTextEdit } from '../input/text-editing.mjs';
import { createCanvasRunText, orderedCanvasBlocks } from '../layout/canvas-text-style.mjs';

const EDITABLE_BLOCK_TYPES = new Set(['paragraph', 'heading', 'list', 'quote']);

export function findCanvasMatches(model, query = {}) {
    const text = String(query?.query ?? query?.text ?? '');
    if (!text) {
        return [];
    }

    const options = normalizeSearchOptions(query?.options || query);
    const matcher = createMatcher(text, options);
    if (!matcher) {
        return [];
    }

    const matches = [];
    const entries = collectSearchableBlocks(model);
    for (const entry of entries) {
        const value = blockText(entry.block);
        if (!value) {
            continue;
        }

        for (const match of matcher(value)) {
            if (options.wholeWord && !isWholeWordMatch(value, match.index, match.text.length)) {
                continue;
            }

            matches.push({
                index: matches.length,
                ordinal: entry.ordinal,
                blockId: entry.block.id || '',
                start: match.index,
                end: match.index + match.text.length,
                length: match.text.length,
                text: match.text,
                captures: match.captures,
                groups: match.groups,
                markerId: `canvas-search-${matches.length}-${entry.block.id || ''}-${match.index}-${match.text.length}`,
                preview: previewText(value, match.index, match.text.length),
            });
        }
    }

    return matches;
}

export function replaceCanvasMatch(model, selection, match, replacement) {
    if (!match?.blockId || Number(match.end) <= Number(match.start)) {
        return { changed: false, model, selection, dirtyBlockIds: [] };
    }

    return applyCanvasTextEdit(model, selection, {
        type: 'replaceRange',
        range: matchRange(match),
        text: expandReplacement(String(replacement ?? ''), match),
        source: 'findReplace',
    });
}

export function replaceAllCanvasMatches(model, selection, matches, replacement) {
    const ordered = Array.isArray(matches)
        ? matches
            .filter(match => match?.blockId && Number(match.end) > Number(match.start))
            .slice()
            .sort((left, right) => {
                const ordinal = Number(right.ordinal || 0) - Number(left.ordinal || 0);
                return ordinal !== 0 ? ordinal : Number(right.start || 0) - Number(left.start || 0);
            })
        : [];

    let current = model;
    let currentSelection = selection;
    const dirtyBlockIds = new Set();
    let replaceCount = 0;
    for (const match of ordered) {
        const result = replaceCanvasMatch(current, currentSelection, match, replacement);
        if (!result.changed) {
            continue;
        }

        current = result.model;
        currentSelection = result.selection || currentSelection;
        replaceCount += 1;
        for (const id of result.dirtyBlockIds || []) {
            dirtyBlockIds.add(id);
        }
    }

    return {
        changed: replaceCount > 0,
        model: current,
        selection: currentSelection,
        replaceCount,
        dirtyBlockIds: Array.from(dirtyBlockIds),
        operation: 'replaceAll',
    };
}

export function expandReplacement(replacement, match) {
    const captures = Array.isArray(match?.captures) ? match.captures : [];
    const groups = match?.groups || {};
    return String(replacement ?? '').replace(/\$(\$|&|\d{1,2}|<[^>]+>)/g, (token, key) => {
        if (key === '$') {
            return '$';
        }

        if (key === '&') {
            return String(match?.text ?? '');
        }

        if (key.startsWith('<') && key.endsWith('>')) {
            return String(groups[key.slice(1, -1)] ?? '');
        }

        const index = Number(key);
        if (!Number.isFinite(index) || index <= 0) {
            return token;
        }

        return String(captures[index - 1] ?? '');
    });
}

export function normalizeSearchOptions(options = {}) {
    return {
        caseSensitive: options.caseSensitive === true || options.CaseSensitive === true,
        wholeWord: options.wholeWord === true || options.WholeWord === true,
        regex: options.regex === true || options.useRegex === true || options.UseRegex === true,
    };
}

export function matchRange(match) {
    return {
        anchor: { blockId: match.blockId || '', offset: Math.max(0, Number(match.start || 0) || 0) },
        focus: { blockId: match.blockId || '', offset: Math.max(0, Number(match.end || 0) || 0) },
    };
}

function createMatcher(query, options) {
    if (!options.regex) {
        const needle = options.caseSensitive ? query : query.toLocaleLowerCase();
        return value => {
            const source = options.caseSensitive ? value : value.toLocaleLowerCase();
            const matches = [];
            let offset = 0;
            while (offset <= source.length - needle.length) {
                const index = source.indexOf(needle, offset);
                if (index < 0) {
                    break;
                }

                matches.push({
                    index,
                    text: value.slice(index, index + query.length),
                    captures: [],
                    groups: {},
                });
                offset = index + Math.max(1, query.length);
            }

            return matches;
        };
    }

    let expression;
    try {
        expression = new RegExp(query, options.caseSensitive ? 'gu' : 'giu');
    } catch {
        return null;
    }

    return value => {
        const matches = [];
        expression.lastIndex = 0;
        let match;
        while ((match = expression.exec(value)) !== null) {
            const found = String(match[0] ?? '');
            if (!found) {
                expression.lastIndex += 1;
                continue;
            }

            matches.push({
                index: match.index,
                text: found,
                captures: Array.from(match).slice(1).map(item => item ?? ''),
                groups: { ...(match.groups || {}) },
            });
        }

        return matches;
    };
}

function collectSearchableBlocks(model) {
    const entries = [];
    let ordinal = 0;
    for (const block of orderedCanvasBlocks(model)) {
        collectBlock(block, entries, () => ordinal++);
    }

    return entries;
}

function collectBlock(block, entries, nextOrdinal) {
    const type = String(block?.type || block?.content?.type || '').toLowerCase();
    if (EDITABLE_BLOCK_TYPES.has(type)) {
        entries.push({ block, ordinal: nextOrdinal() });
        return;
    }

    if (type === 'table') {
        for (const row of block?.content?.table?.rows || []) {
            for (const cell of row?.cells || []) {
                for (const child of cell?.blocks || []) {
                    collectBlock(child, entries, nextOrdinal);
                }
            }
        }
    }
}

function blockText(block) {
    return (block?.content?.runs || []).map(createCanvasRunText).join('');
}

function isWholeWordMatch(text, start, length) {
    const before = start > 0 ? text[start - 1] : '';
    const after = start + length < text.length ? text[start + length] : '';
    return !isWordChar(before) && !isWordChar(after);
}

function isWordChar(value) {
    return /[\p{L}\p{N}_]/u.test(String(value || ''));
}

function previewText(text, start, length) {
    const prefixStart = Math.max(0, start - 24);
    const suffixEnd = Math.min(text.length, start + length + 24);
    return text.slice(prefixStart, suffixEnd).replace(/\s+/g, ' ').trim().slice(0, 96);
}
