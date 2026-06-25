import {
    clampLevel,
    formatName,
    formatNumberingLabel,
    levelForList,
    normalizeNumberingDefinitions,
    resolveDefinitionForList,
    suffixGap,
} from './numbering-definition.mjs';

export function resolveNumberingState(model = {}, blocks = []) {
    const definitions = normalizeNumberingDefinitions(model);
    const contexts = new Map();
    const labels = new Map();
    const items = new Map();

    for (const block of Array.isArray(blocks) ? blocks : []) {
        if (!isListBlock(block)) {
            continue;
        }

        const list = block.content?.list || {};
        const definition = resolveDefinitionForList(model, list, definitions);
        const level = clampLevel(list.indentLevel ?? list.IndentLevel);
        const levelDefinition = levelForList(definition, list);
        const key = numberingContextKey(definition, list);
        const context = contexts.get(key) || { counters: Array(9).fill(0), lastLevel: -1 };
        if (list.restartNumbering === true || list.RestartNumbering === true) {
            context.counters = Array(9).fill(0);
            context.lastLevel = -1;
        }

        for (let index = level + 1; index < context.counters.length; index += 1) {
            context.counters[index] = 0;
        }

        const ordered = list.ordered === true || list.Ordered === true;
        if (ordered && formatName(levelDefinition.format) !== 'bullet') {
            const explicit = integerOrNull(list.numberingValue ?? list.NumberingValue);
            const startNumber = integerOrNull(list.startNumber ?? list.StartNumber);
            if (explicit) {
                context.counters[level] = explicit;
            } else if (!context.counters[level]) {
                context.counters[level] = startNumber || Math.max(1, Number(levelDefinition.startAt) || 1);
            } else {
                context.counters[level] += 1;
            }
        } else {
            context.counters[level] = 0;
        }

        contexts.set(key, context);
        const label = formatNumberingLabel(levelDefinition, context.counters, definition.levels);
        const item = {
            blockId: String(block.id || ''),
            label,
            ordered,
            level,
            definitionId: definition.id,
            abstractId: definition.abstractId,
            numberFormat: levelDefinition.format,
            levelText: levelDefinition.text,
            suffix: levelDefinition.suffix,
            labelIndent: nonNegativeNumber(list.labelIndent ?? list.LabelIndent, levelDefinition.indent),
            hangingIndent: nonNegativeNumber(list.hangingIndent ?? list.HangingIndent, levelDefinition.hanging),
            gap: suffixGap(levelDefinition.suffix),
            counters: [...context.counters],
        };
        labels.set(item.blockId, label);
        items.set(item.blockId, item);
        context.lastLevel = level;
    }

    return {
        definitions,
        labels,
        items,
    };
}

export function isListBlock(block) {
    const type = String(block?.type || block?.content?.type || '').replace(/[\s_-]/g, '').toLowerCase();
    return type === 'list' && block?.content?.list;
}

function numberingContextKey(definition, list) {
    const requested = String(list.numberingId ?? list.NumberingId ?? '').trim();
    if (requested) {
        return requested;
    }

    const styleId = String(list.listStyleId ?? list.ListStyleId ?? '').trim();
    if (styleId) {
        return `style:${styleId}`;
    }

    return definition?.id || 'default';
}

function integerOrNull(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? Math.trunc(parsed) : null;
}

function nonNegativeNumber(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : Math.max(0, Number(fallback) || 0);
}
