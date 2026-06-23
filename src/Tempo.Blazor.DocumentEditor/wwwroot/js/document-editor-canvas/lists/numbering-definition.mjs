export const MAX_NUMBERING_LEVEL = 8;
export const DEFAULT_BULLET_NUMBERING_ID = 'tm-default-bullet';
export const DEFAULT_NUMBERED_NUMBERING_ID = 'tm-default-numbered';
export const DEFAULT_LEGAL_NUMBERING_ID = 'tm-default-legal';

const BULLET_GLYPHS = ['\u2022', '\u25e6', '\u25aa'];
const SIMPLE_NUMBER_FORMATS = ['decimal', 'lowerLetter', 'lowerRoman'];

export function normalizeNumberingDefinitions(model = {}) {
    const sources = arrayValue(model.numberingDefinitions ?? model.NumberingDefinitions);
    const definitions = sources.map(normalizeNumberingDefinition).filter(Boolean);
    const byId = new Map(definitions.map(definition => [definition.id, definition]));
    for (const definition of [
        createDefaultNumberedDefinition(),
        createDefaultBulletDefinition(),
        createDefaultLegalDefinition(),
    ]) {
        if (!byId.has(definition.id)) {
            definitions.push(definition);
            byId.set(definition.id, definition);
        }
    }

    return definitions;
}

export function normalizeListStyles(model = {}) {
    return arrayValue(model.listStyles ?? model.ListStyles)
        .map(style => ({
            id: textValue(style?.id ?? style?.Id),
            name: textValue(style?.name ?? style?.Name),
            numberingId: textValue(style?.numberingId ?? style?.NumberingId),
            isQuickStyle: boolValue(style?.isQuickStyle ?? style?.IsQuickStyle),
        }))
        .filter(style => style.id);
}

export function normalizeNumberingDefinition(source = {}) {
    const id = textValue(source.id ?? source.Id);
    if (!id) {
        return null;
    }

    const levels = arrayValue(source.levels ?? source.Levels);
    const ordered = levels.some(level => formatName(level?.format ?? level?.Format) !== 'bullet');
    return {
        id,
        abstractId: textValue(source.abstractId ?? source.AbstractId) || id,
        name: textValue(source.name ?? source.Name) || id,
        styleId: textValue(source.styleId ?? source.StyleId),
        levels: completeLevels(levels.map(level => normalizeLevel(level, ordered)).filter(Boolean), ordered),
    };
}

export function createDefaultNumberedDefinition(id = DEFAULT_NUMBERED_NUMBERING_ID) {
    return {
        id,
        abstractId: id,
        name: 'Numbered List',
        styleId: 'numbered-list',
        levels: completeLevels([], true),
    };
}

export function createDefaultBulletDefinition(id = DEFAULT_BULLET_NUMBERING_ID) {
    return {
        id,
        abstractId: id,
        name: 'Bullet List',
        styleId: 'bullet-list',
        levels: completeLevels([], false),
    };
}

export function createDefaultLegalDefinition(id = DEFAULT_LEGAL_NUMBERING_ID) {
    return {
        id,
        abstractId: id,
        name: 'Legal Numbering',
        styleId: 'legal-numbered-list',
        levels: Array.from({ length: MAX_NUMBERING_LEVEL + 1 }, (_, level) => ({
            level,
            format: 'decimal',
            text: Array.from({ length: level + 1 }, (_unused, index) => `%${index + 1}`).join('.') + '.',
            startAt: 1,
            suffix: 'tab',
            indent: level * 24,
            hanging: 24,
            bullet: '',
        })),
    };
}

export function resolveDefinitionForList(model, list = {}, definitions = normalizeNumberingDefinitions(model)) {
    const styles = normalizeListStyles(model);
    const byId = new Map(definitions.map(definition => [definition.id, definition]));
    const byAbstractId = new Map(definitions.map(definition => [definition.abstractId, definition]));
    const styleId = textValue(list.listStyleId ?? list.ListStyleId);
    const style = styles.find(item => item.id === styleId);
    const requestedId = textValue(list.numberingId ?? list.NumberingId ?? style?.numberingId);
    const abstractId = textValue(list.abstractNumberingId ?? list.AbstractNumberingId);
    if (requestedId && byId.has(requestedId)) {
        return byId.get(requestedId);
    }

    if (abstractId && byAbstractId.has(abstractId)) {
        return byAbstractId.get(abstractId);
    }

    const format = formatName(list.numberFormat ?? list.NumberFormat);
    if (format === 'legal') {
        return byId.get(DEFAULT_LEGAL_NUMBERING_ID);
    }

    const ordered = list.ordered === true || list.Ordered === true;
    return byId.get(ordered ? DEFAULT_NUMBERED_NUMBERING_ID : DEFAULT_BULLET_NUMBERING_ID);
}

export function levelForList(definition, list = {}) {
    const indentLevel = clampLevel(list.indentLevel ?? list.IndentLevel);
    return definition?.levels?.[indentLevel] || completeLevels([], list.ordered === true)[indentLevel];
}

export function formatNumber(format, value) {
    const number = Math.max(1, Math.trunc(Number(value) || 1));
    switch (formatName(format)) {
        case 'lowerletter':
            return toAlpha(number).toLowerCase();
        case 'upperletter':
            return toAlpha(number).toUpperCase();
        case 'lowerroman':
            return toRoman(number).toLowerCase();
        case 'upperroman':
            return toRoman(number).toUpperCase();
        case 'decimalzero':
            return String(number).padStart(2, '0');
        default:
            return String(number);
    }
}

export function formatNumberingLabel(level, counters, levels) {
    if (!level || formatName(level.format) === 'bullet') {
        return level?.bullet || BULLET_GLYPHS[clampLevel(level?.level) % BULLET_GLYPHS.length];
    }

    const template = textValue(level.text) || `%${clampLevel(level.level) + 1}.`;
    return template.replace(/%([1-9])/g, (_match, token) => {
        const index = Math.max(0, Number(token) - 1);
        const sourceLevel = levels[index] || level;
        const value = Math.max(1, Number(counters[index]) || Number(sourceLevel.startAt) || 1);
        return formatNumber(sourceLevel.format, value);
    });
}

export function normalizeSuffix(value) {
    const normalized = textValue(value).replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'nothing' || normalized === 'none') return 'none';
    if (normalized === 'space') return 'space';
    return 'tab';
}

export function suffixGap(value, fallback = 12) {
    const suffix = normalizeSuffix(value);
    if (suffix === 'none') return 4;
    if (suffix === 'space') return 8;
    return Math.max(12, Number(fallback) || 12);
}

export function clampLevel(value) {
    const parsed = Number(value);
    return Math.max(0, Math.min(MAX_NUMBERING_LEVEL, Number.isFinite(parsed) ? Math.trunc(parsed) : 0));
}

export function formatName(value) {
    return textValue(value).replace(/[\s_-]/g, '').toLowerCase();
}

function completeLevels(levels, ordered) {
    const byLevel = new Map(levels.map(level => [clampLevel(level.level), level]));
    return Array.from({ length: MAX_NUMBERING_LEVEL + 1 }, (_, level) => {
        const existing = byLevel.get(level);
        return existing ? { ...defaultLevel(level, ordered), ...existing, level } : defaultLevel(level, ordered);
    });
}

function normalizeLevel(source, ordered) {
    const level = clampLevel(source?.level ?? source?.Level);
    return {
        level,
        format: textValue(source?.format ?? source?.Format) || (ordered ? SIMPLE_NUMBER_FORMATS[level % SIMPLE_NUMBER_FORMATS.length] : 'bullet'),
        text: textValue(source?.text ?? source?.Text),
        startAt: Math.max(1, Math.trunc(Number(source?.startAt ?? source?.StartAt ?? 1) || 1)),
        suffix: normalizeSuffix(source?.suffix ?? source?.Suffix),
        indent: nonNegativeNumber(source?.indent ?? source?.Indent, level * 24),
        hanging: nonNegativeNumber(source?.hanging ?? source?.Hanging, 24),
        bullet: textValue(source?.bullet ?? source?.Bullet),
    };
}

function defaultLevel(level, ordered) {
    if (!ordered) {
        return {
            level,
            format: 'bullet',
            text: BULLET_GLYPHS[level % BULLET_GLYPHS.length],
            startAt: 1,
            suffix: 'tab',
            indent: level * 24,
            hanging: 24,
            bullet: BULLET_GLYPHS[level % BULLET_GLYPHS.length],
        };
    }

    const format = SIMPLE_NUMBER_FORMATS[level % SIMPLE_NUMBER_FORMATS.length];
    return {
        level,
        format,
        text: `%${level + 1}.`,
        startAt: 1,
        suffix: 'tab',
        indent: level * 24,
        hanging: 24,
        bullet: '',
    };
}

function toAlpha(value) {
    let result = '';
    let number = Math.max(1, Math.trunc(Number(value) || 1));
    while (number > 0) {
        const remainder = (number - 1) % 26;
        result = String.fromCharCode(65 + remainder) + result;
        number = Math.floor((number - 1) / 26);
    }

    return result || 'A';
}

const ROMAN = [[1000, 'M'], [900, 'CM'], [500, 'D'], [400, 'CD'], [100, 'C'], [90, 'XC'], [50, 'L'], [40, 'XL'], [10, 'X'], [9, 'IX'], [5, 'V'], [4, 'IV'], [1, 'I']];

function toRoman(value) {
    let number = Math.max(1, Math.min(3999, Math.trunc(Number(value) || 1)));
    let result = '';
    for (const [unit, glyph] of ROMAN) {
        while (number >= unit) {
            result += glyph;
            number -= unit;
        }
    }

    return result || 'I';
}

function arrayValue(value) {
    return Array.isArray(value) ? value : [];
}

function textValue(value) {
    return String(value ?? '').trim();
}

function boolValue(value) {
    return value === true || String(value).toLowerCase() === 'true';
}

function nonNegativeNumber(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
}
