export const ADVANCED_TOGGLE_MARK_COMMANDS = new Map([
    ['superscript', 'superscript'],
    ['subscript', 'subscript'],
    ['smallcaps', 'smallCaps'],
    ['allcaps', 'allCaps'],
    ['doublestrikethrough', 'doubleStrikethrough'],
    ['doublestrike', 'doubleStrikethrough'],
]);

export const ADVANCED_VALUE_MARK_COMMANDS = new Map([
    ['characterspacing', 'characterSpacing'],
    ['setcharacterspacing', 'characterSpacing'],
    ['characterscale', 'characterScale'],
    ['setcharacterscale', 'characterScale'],
    ['kerning', 'kerning'],
    ['togglekerning', 'kerning'],
]);

export const ADVANCED_INLINE_MARK_TYPES = [
    'superscript',
    'subscript',
    'smallcaps',
    'allcaps',
    'doublestrikethrough',
    'characterspacing',
    'characterscale',
    'kerning',
];

const FONT_SIZE_STEPS = [6, 7, 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 32, 36, 40, 44, 48, 54, 60, 66, 72, 80, 88, 96];

export function createAdvancedCharacterMark(markType, argument) {
    const normalizedType = normalizeAdvancedMarkType(markType);
    if (normalizedType === 'kerning') {
        const value = normalizeKerningValue(argument);
        return value === 'true' ? null : { type: markType, value };
    }

    if (normalizedType === 'characterspacing') {
        const value = normalizeCharacterSpacingValue(argument);
        return value === null ? null : { type: markType, value: String(value) };
    }

    if (normalizedType === 'characterscale') {
        const value = normalizeCharacterScaleValue(argument);
        return value === null ? null : { type: markType, value: String(value) };
    }

    return null;
}

export function normalizeChangeCaseVariant(argument) {
    const raw = typeof argument === 'object'
        ? argument?.variant ?? argument?.case ?? argument?.value
        : argument;
    const variant = String(raw || '').replace(/[\s_-]/g, '').toLowerCase();
    if (variant === 'upper' || variant === 'uppercase' || variant === 'allcaps') {
        return 'uppercase';
    }

    if (variant === 'lower' || variant === 'lowercase') {
        return 'lowercase';
    }

    if (variant === 'sentence' || variant === 'sentencecase') {
        return 'sentencecase';
    }

    if (variant === 'title' || variant === 'titlecase' || variant === 'capitalize') {
        return 'titlecase';
    }

    if (variant === 'toggle' || variant === 'togglecase' || variant === 'swapcase') {
        return 'togglecase';
    }

    return '';
}

export function changeCharacterCase(text, variant, locale = undefined) {
    const source = String(text || '');
    switch (normalizeChangeCaseVariant(variant)) {
        case 'uppercase':
            return source.toLocaleUpperCase(locale);
        case 'lowercase':
            return source.toLocaleLowerCase(locale);
        case 'sentencecase':
            return toSentenceCase(source, locale);
        case 'titlecase':
            return toTitleCase(source, locale);
        case 'togglecase':
            return Array.from(source).map(ch => {
                const upper = ch.toLocaleUpperCase(locale);
                const lower = ch.toLocaleLowerCase(locale);
                return ch === upper && ch !== lower ? lower : upper;
            }).join('');
        default:
            return source;
    }
}

export function nextFontSizeStep(currentValue, direction) {
    const current = parseFontSizePoints(currentValue);
    const delta = Number(direction || 0) < 0 ? -1 : 1;
    const index = FONT_SIZE_STEPS.findIndex(size => size >= current);
    if (delta > 0) {
        const next = FONT_SIZE_STEPS.find(size => size > current + 0.001) ?? FONT_SIZE_STEPS[FONT_SIZE_STEPS.length - 1];
        return clampFontSize(next);
    }

    if (index <= 0) {
        return FONT_SIZE_STEPS[0];
    }

    const previous = FONT_SIZE_STEPS[index] >= current - 0.001 ? FONT_SIZE_STEPS[index - 1] : FONT_SIZE_STEPS[index];
    return clampFontSize(previous);
}

export function parseFontSizePoints(value, fallback = 11) {
    const raw = typeof value === 'object'
        ? value?.value ?? value?.fontSize ?? value?.size
        : value;
    const text = String(raw ?? '').trim().replace(/pt$/iu, '').trim();
    const parsed = Number(text);
    return Number.isFinite(parsed) && parsed > 0 ? clampFontSize(parsed) : fallback;
}

function normalizeAdvancedMarkType(markType) {
    return String(markType || '').replace(/[\s_-]/g, '').toLowerCase();
}

function normalizeCharacterSpacingValue(argument) {
    const raw = typeof argument === 'object'
        ? argument?.value ?? argument?.spacing ?? argument?.points ?? argument?.px
        : argument;
    const parsed = Number(String(raw ?? '').replace(/(pt|px)$/iu, '').trim());
    if (!Number.isFinite(parsed)) {
        return null;
    }

    return Math.max(-12, Math.min(36, Math.round(parsed * 100) / 100));
}

function normalizeCharacterScaleValue(argument) {
    const raw = typeof argument === 'object'
        ? argument?.value ?? argument?.scale ?? argument?.percent
        : argument;
    const parsed = Number(String(raw ?? '').replace(/%$/u, '').trim());
    if (!Number.isFinite(parsed) || parsed <= 0) {
        return null;
    }

    return Math.max(33, Math.min(300, Math.round(parsed * 100) / 100));
}

function normalizeKerningValue(argument) {
    const raw = typeof argument === 'object'
        ? argument?.value ?? argument?.enabled ?? argument?.kerning
        : argument;
    if (raw === false) {
        return 'false';
    }

    const text = String(raw ?? '').trim().toLowerCase();
    return text === 'false' || text === 'none' || text === 'off' || text === '0'
        ? 'false'
        : 'true';
}

function toSentenceCase(text, locale) {
    let seenLetter = false;
    let shouldUpper = true;
    return Array.from(text.toLocaleLowerCase(locale)).map(ch => {
        if (/\p{L}/u.test(ch)) {
            const result = shouldUpper ? ch.toLocaleUpperCase(locale) : ch;
            seenLetter = true;
            shouldUpper = false;
            return result;
        }

        if (/[.!?]/u.test(ch) && seenLetter) {
            shouldUpper = true;
        }

        return ch;
    }).join('');
}

function toTitleCase(text, locale) {
    let shouldUpper = true;
    return Array.from(text.toLocaleLowerCase(locale)).map(ch => {
        if (/\p{L}|\p{N}/u.test(ch)) {
            const result = shouldUpper ? ch.toLocaleUpperCase(locale) : ch;
            shouldUpper = false;
            return result;
        }

        shouldUpper = !/'/u.test(ch);
        return ch;
    }).join('');
}

function clampFontSize(value) {
    return Math.max(6, Math.min(96, Math.round(Number(value) * 100) / 100));
}
