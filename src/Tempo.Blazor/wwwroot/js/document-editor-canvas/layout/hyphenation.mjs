const SOFT_HYPHEN = '\u00AD';
const NON_BREAKING_HYPHEN = '\u2011';
const DEFAULT_MIN_PREFIX = 3;
const DEFAULT_MIN_SUFFIX = 3;

export function normalizeHyphenationOptions(options = {}, text = '') {
    const source = options || {};
    const mode = normalizeMode(source.mode ?? source.Mode);
    const hasManualBreaks = String(text || '').includes(SOFT_HYPHEN);
    const enabled = source.enabled === true
        || source.Enabled === true
        || mode === 'manual'
        || mode === 'auto'
        || hasManualBreaks;

    return {
        enabled,
        mode: mode || (hasManualBreaks ? 'manual' : 'off'),
        zone: clampNumber(source.zone ?? source.Zone, 0, 96, 0),
        consecutiveLimit: clampNumber(source.consecutiveLimit ?? source.ConsecutiveLimit, 1, 12, 2),
        minPrefix: clampNumber(source.minPrefix ?? source.MinPrefix, 2, 12, DEFAULT_MIN_PREFIX),
        minSuffix: clampNumber(source.minSuffix ?? source.MinSuffix, 2, 12, DEFAULT_MIN_SUFFIX),
        hyphen: String(source.hyphen ?? source.Hyphen ?? '-').slice(0, 2) || '-',
    };
}

export function hyphenationBreaksForWord(word, options = {}) {
    const text = String(word || '');
    const normalized = normalizeHyphenationOptions(options, text);
    if (!normalized.enabled || text.includes(NON_BREAKING_HYPHEN)) {
        return [];
    }

    const manual = manualBreaks(text, normalized);
    if (normalized.mode === 'manual') {
        return manual;
    }

    const auto = automaticBreaks(text, normalized);
    return mergeBreaks(manual, auto);
}

export function hyphenateTokenToFit(token, tokenText, tokenStyle, service, availableWidth, options = {}, state = {}) {
    const text = String(tokenText || '');
    const normalized = normalizeHyphenationOptions(options, text);
    const width = Math.max(0, Number(availableWidth || 0) || 0);
    if (!normalized.enabled || !text || width <= 0 || state.consecutiveCount >= normalized.consecutiveLimit) {
        return null;
    }

    const visibleLength = visibleTextLength(text);
    const breaks = hyphenationBreaksForWord(text, normalized)
        .filter(item => item.index >= normalized.minPrefix && visibleLength - item.index >= normalized.minSuffix)
        .sort((a, b) => b.index - a.index);

    for (const breakpoint of breaks) {
        const sourceIndex = Number(breakpoint.sourceIndex ?? sourceIndexForVisibleIndex(text, breakpoint.index)) || breakpoint.index;
        const prefix = text.slice(0, sourceIndex).replaceAll(SOFT_HYPHEN, '');
        const suffix = text.slice(sourceIndex).replaceAll(SOFT_HYPHEN, '');
        if (!prefix || !suffix) {
            continue;
        }

        const rendered = `${prefix}${normalized.hyphen}`;
        const measured = service.measureText(rendered, tokenStyle || {});
        if (measured.width <= width + 0.0001) {
            const baseStart = Number(token?.start || 0) || 0;
            return {
                text: rendered,
                remainderText: suffix,
                start: baseStart,
                end: baseStart + sourceIndex,
                remainderStart: baseStart + sourceIndex,
                width: measured.width,
                hyphenation: {
                    automatic: breakpoint.manual !== true,
                    manual: breakpoint.manual === true,
                    sourceIndex,
                },
            };
        }
    }

    return null;
}

function manualBreaks(text, options) {
    const breaks = [];
    let visibleIndex = 0;
    const totalVisible = visibleTextLength(text);
    for (let index = 0; index < text.length; index += 1) {
        const char = text[index];
        if (char === SOFT_HYPHEN) {
            if (visibleIndex >= options.minPrefix && totalVisible - visibleIndex >= options.minSuffix) {
                breaks.push({ index: visibleIndex, sourceIndex: index + 1, manual: true });
            }
            continue;
        }

        visibleIndex += 1;
    }

    return breaks;
}

function visibleTextLength(text) {
    return String(text || '').replaceAll(SOFT_HYPHEN, '').length;
}

function automaticBreaks(text, options) {
    const clean = text.replaceAll(SOFT_HYPHEN, '');
    if (clean.length < options.minPrefix + options.minSuffix + 1) {
        return [];
    }

    const breaks = [];
    for (let index = options.minPrefix; index <= clean.length - options.minSuffix; index += 1) {
        const before = clean[index - 1] || '';
        const current = clean[index] || '';
        const next = clean[index + 1] || '';
        if (isVowel(before) && isConsonant(current) && isVowel(next)) {
            breaks.push({ index, sourceIndex: sourceIndexForVisibleIndex(text, index), manual: false, weight: 3 });
        } else if (isConsonant(before) && isConsonant(current) && isVowel(next)) {
            breaks.push({ index, sourceIndex: sourceIndexForVisibleIndex(text, index), manual: false, weight: 2 });
        }
    }

    return breaks.sort((left, right) => right.weight - left.weight || right.index - left.index);
}

function sourceIndexForVisibleIndex(text, visibleTarget) {
    let visible = 0;
    for (let index = 0; index < text.length; index += 1) {
        if (text[index] === SOFT_HYPHEN) {
            continue;
        }

        visible += 1;
        if (visible >= visibleTarget) {
            return index + 1;
        }
    }

    return text.length;
}

function mergeBreaks(...groups) {
    const byIndex = new Map();
    for (const group of groups) {
        for (const item of group) {
            const existing = byIndex.get(item.index);
            if (!existing || item.manual === true) {
                byIndex.set(item.index, item);
            }
        }
    }

    return Array.from(byIndex.values()).sort((left, right) => left.index - right.index);
}

function normalizeMode(value) {
    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'manual') return 'manual';
    if (normalized === 'auto' || normalized === 'automatic') return 'auto';
    return '';
}

function clampNumber(value, min, max, fallback) {
    const number = Number(value);
    if (!Number.isFinite(number)) {
        return fallback;
    }

    return Math.max(min, Math.min(max, number));
}

function isVowel(char) {
    return /[aeiouyáéíóúůýěäöüAEIOUYÁÉÍÓÚŮÝĚÄÖÜ]/u.test(char);
}

function isConsonant(char) {
    return /[bcčdďfghjklmnňpqrřsštťvwxzžBCČDĎFGHJKLMNŇPQRŘSŠTŤVWXZŽ]/u.test(char);
}
