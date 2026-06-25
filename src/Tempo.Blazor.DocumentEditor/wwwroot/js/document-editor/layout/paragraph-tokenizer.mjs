// Phase D — layout/paragraph-tokenizer.mjs
// Tokenization pipeline for the line breaker.
//
// Layers (bottom-up):
//   - char classifiers: `isCjkCharacter`, `isTokenDelimiter`
//   - unit converters: `cssLengthToPixels` (px / pt / number)
//   - style resolver: `mergeTextStyle` (folds run.style + marks → CSS-ish style)
//   - text tokenizer: `tokenizeText` (splits a string into newline/tab/nbsp/space/cjk/word/longToken tokens)
//   - run flattener: `flattenParagraphRuns` (paragraph → flat runs with absolute offsets, drawing objects)
//   - paragraph tokenizer: `tokensForParagraph` (paragraph → { text, runs, tokens })
//   - offset lookup: `runForOffset`
//
// `flattenParagraphRuns` / `tokensForParagraph` depend on `normalizeImageObject` —
// passed via injection because the legacy IIFE may still own the canonical normaliser
// during incremental migration.

import { asArray, asText, sortObject, clone } from '../core/helpers.mjs';
import { markType } from '../core/marks.mjs';
import { uppercasePreservingLength } from '../core/text-transform.mjs';

export function isCjkCharacter(ch) {
    return /[぀-ヿ㐀-䶿一-鿿豈-﫿]/.test(ch || '');
}

export function isTokenDelimiter(ch) {
    return ch === '\r' || ch === '\n' || ch === '\t'
        || ch === '­' || ch === ' ' || /[ ]/.test(ch)
        || isCjkCharacter(ch);
}

export function cssLengthToPixels(value, fallback) {
    if (typeof value === 'number') {
        return Number.isFinite(value) && value > 0 ? value : fallback;
    }
    const text = asText(value).trim().toLowerCase();
    const number = parseFloat(text);
    if (!Number.isFinite(number) || number <= 0) return fallback;
    if (text.endsWith('pt')) return number * 4 / 3;
    return number;
}

export function mergeTextStyle(baseStyle, run) {
    const style = Object.assign({}, baseStyle || {}, run && run.style || run && run.Style || {});
    let verticalScript = '';
    asArray(run && (run.marks || run.Marks)).forEach(function (mark) {
        const type = markType(mark);
        const value = mark && (mark.value ?? mark.Value ?? mark.color ?? mark.Color ?? null);
        if (type === 'bold') style.fontWeight = style.fontWeight || '700';
        if (type === 'italic') style.fontStyle = style.fontStyle || 'italic';
        if (type === 'fontfamily' && value) style.fontFamily = value;
        if (type === 'fontsize' && value) {
            style.fontSize = cssLengthToPixels(value, style.fontSize || 16);
        }
        if ((type === 'textcolor' || type === 'fontcolor' || type === 'foregroundcolor') && value) {
            style.color = value;
        }
        if ((type === 'highlight' || type === 'backgroundcolor') && value) {
            style.backgroundColor = value;
        }
        if (type === 'superscript') {
            verticalScript = 'superscript';
        }
        if (type === 'subscript') {
            verticalScript = 'subscript';
        }
        if (type === 'smallcaps') style.fontVariantCaps = 'small-caps';
        if (type === 'allcaps') style.textTransform = 'uppercase';
        if (type === 'characterspacing' && value != null) style.letterSpacing = Number(value) || 0;
        if (type === 'characterscale' && value != null) style.characterScale = Math.max(0.1, (Number(value) || 100) / 100);
        if (type === 'kerning' && value != null) style.kerning = String(value).toLowerCase() !== 'false';
    });
    if (verticalScript === 'superscript') {
        const originalSize = cssLengthToPixels(style.fontSize || 16, 16);
        style.fontSize = originalSize * 0.65;
        style.baselineShift = -originalSize * 0.34;
    }
    if (verticalScript === 'subscript') {
        const originalSize = cssLengthToPixels(style.fontSize || 16, 16);
        style.fontSize = originalSize * 0.65;
        style.baselineShift = originalSize * 0.22;
    }
    return style;
}

export function tokenizeText(text, options) {
    const source = asText(text);
    const opts = options || {};
    const longThreshold = Number(opts.longTokenThreshold || opts.LongTokenThreshold || 32) || 32;
    const tokens = [];
    let index = 0;
    function push(type, value, start, end, extra) {
        // Layout tokens are consumed by field name, never by key order; canonical key sorting here
        // (sortObject) dominated cold layout (~55% of the time / GC pressure on long documents).
        tokens.push(Object.assign({
            type: type,
            text: value,
            start: start,
            end: end,
            length: Math.max(0, end - start),
            breakBefore: false,
            breakAfter: false,
            hardBreak: false,
            unbreakable: false,
        }, extra || {}));
    }
    while (index < source.length) {
        const ch = source[index];
        if (ch === '\r' || ch === '\n') {
            const startNewline = index;
            if (ch === '\r' && source[index + 1] === '\n') index += 2;
            else index++;
            push('newline', source.slice(startNewline, index), startNewline, index,
                { breakBefore: true, breakAfter: true, hardBreak: true });
            continue;
        }
        if (ch === '\t') {
            push('tab', ch, index, index + 1, { breakAfter: true });
            index++;
            continue;
        }
        if (ch === '­') {
            push('softHyphen', ch, index, index + 1, { breakAfter: true });
            index++;
            continue;
        }
        if (ch === ' ') {
            push('nbsp', ch, index, index + 1, { unbreakable: true });
            index++;
            continue;
        }
        if (ch === ' ') {
            const spaceStart = index;
            while (source[index] === ' ') index++;
            push('space', source.slice(spaceStart, index), spaceStart, index,
                { breakBefore: true, breakAfter: true });
            continue;
        }
        if (isCjkCharacter(ch)) {
            const cjkStart = index;
            const codePoint = Array.from(source.slice(index))[0] || ch;
            index += codePoint.length;
            push('cjk', codePoint, cjkStart, index, { breakBefore: true, breakAfter: true });
            continue;
        }
        const wordStart = index;
        while (index < source.length && !isTokenDelimiter(source[index])) index++;
        const word = source.slice(wordStart, index);
        push(word.length > longThreshold ? 'longToken' : 'word', word, wordStart, index,
            { unbreakable: word.length > longThreshold });
    }
    return tokens;
}

export function runForOffset(runs, offset) {
    const fallback = runs[0] || { style: {} };
    for (let i = 0; i < runs.length; i++) {
        if (offset >= runs[i].start && offset < runs[i].end) return runs[i];
    }
    return runs[runs.length - 1] || fallback;
}

// Paragraph-level functions depend on `normalizeImageObject` (because drawing runs need
// canonicalisation before they can be tokenised). Provided via injection so the legacy
// IIFE's copy can be wired in until the canonical version is migrated.
export function createParagraphTokenizer(options) {
    const opts = options || {};
    if (typeof opts.normalizeImageObject !== 'function') {
        throw new TypeError('createParagraphTokenizer requires options.normalizeImageObject (function)');
    }
    const { normalizeImageObject } = opts;

    function flattenParagraphRuns(paragraph) {
        const source = paragraph || {};
        let runs = asArray(source.runs || source.Runs
            || source.content && source.content.runs
            || source.Content && source.Content.Runs);
        if (runs.length === 0) {
            runs = [{ text: asText(source.text || source.Text || '') }];
        }
        const baseStyle = source.style || source.Style || {};
        let cursor = 0;
        const result = [];
        runs.forEach(function (run, index) {
            const rawKind = String(run.kind || run.Kind || run.type || run.Type || 'text').toLowerCase();
            const kind = rawKind.indexOf('signingfield') >= 0
                ? 'signingField'
                : rawKind.indexOf('drawing') >= 0
                    ? 'drawing'
                    : rawKind.indexOf('field') >= 0
                        ? 'field'
                        : rawKind.indexOf('math') >= 0
                            ? 'math'
                            : rawKind.indexOf('contentcontrol') >= 0
                                ? 'contentControl'
                                : (rawKind.indexOf('token') >= 0 ? 'token' : 'text');
            const rawText = kind === 'drawing' || kind === 'signingField'
                ? ''
                : asText(run.text || run.Text || run.fallbackText || run.FallbackText || '');
            const style = mergeTextStyle(baseStyle, run);
            const text = style.textTransform === 'uppercase'
                ? uppercasePreservingLength(rawText)
                : rawText;
            const object = kind === 'drawing'
                ? normalizeImageObject(run, {
                    blockId: source.id || source.Id || source.blockId || source.BlockId || '',
                    inlineIndex: index,
                })
                : null;
            result.push({
                id: run.id || run.Id || ('run-' + index),
                kind: kind,
                text: text,
                start: cursor,
                end: cursor + rawText.length,
                style: style,
                marks: asArray(run.marks || run.Marks),
                object: object,
                math: run.math || run.Math || null,
                mathLayoutWidth: run.mathLayoutWidth ?? run.MathLayoutWidth ?? run.width ?? run.Width ?? null,
                mathLayoutHeight: run.mathLayoutHeight ?? run.MathLayoutHeight ?? run.height ?? run.Height ?? null,
                mathLayoutAscent: run.mathLayoutAscent ?? run.MathLayoutAscent ?? null,
                mathLayoutDescent: run.mathLayoutDescent ?? run.MathLayoutDescent ?? null,
                contentControl: run.contentControl || run.ContentControl || null,
                signingField: run.signingField || run.SigningField || null,
                signingFieldWidth: run.signingFieldWidth ?? run.SigningFieldWidth ?? null,
                signingFieldHeight: run.signingFieldHeight ?? run.SigningFieldHeight ?? null,
                objectId: object && object.objectId || run.objectId || run.ObjectId || null,
            });
            cursor += rawText.length;
        });
        return result;
    }

    function tokensForParagraph(paragraph) {
        const runs = flattenParagraphRuns(paragraph);
        const text = runs.map(function (run) {
            return run.kind === 'drawing' ? '' : run.text;
        }).join('');
        const tokens = [];
        runs.forEach(function (run, runIndex) {
            if (run.kind === 'signingField') {
                const fontSize = cssLengthToPixels(run.style && (run.style.fontSize ?? run.style.FontSize), 16);
                const width = Math.max(1, Number(run.signingFieldWidth || (run.signingField && run.signingField.boxWidth) || 0) || 1);
                const height = Math.max(1, Number(run.signingFieldHeight || (run.signingField && run.signingField.boxHeight) || 0) || fontSize * 1.25);
                tokens.push(sortObject({
                    type: 'inlineObject',
                    kind: 'signingField',
                    text: '',
                    start: run.start,
                    end: run.end,
                    length: 0,
                    breakBefore: true,
                    breakAfter: true,
                    hardBreak: false,
                    unbreakable: true,
                    runId: run.id || null,
                    signingField: run.signingField || null,
                    width: width,
                    height: height,
                    style: clone(run.style || {}),
                    marks: clone(run.marks || []),
                }));
                return;
            }

            if (run.kind === 'drawing') {
                const object = run.object || normalizeImageObject(run, {
                    blockId: paragraph && (paragraph.id || paragraph.Id
                        || paragraph.blockId || paragraph.BlockId) || '',
                    inlineIndex: runIndex,
                });
                if (object && object.isInline !== true) return;
                tokens.push(sortObject({
                    type: 'inlineObject',
                    kind: 'drawing',
                    text: '',
                    start: run.start,
                    end: run.end,
                    length: 0,
                    breakBefore: true,
                    breakAfter: true,
                    hardBreak: false,
                    unbreakable: true,
                    runId: run.id || null,
                    objectId: object && object.objectId || run.objectId || null,
                    object: object,
                    width: Math.max(1, Number(object && object.width || 1) || 1),
                    height: Math.max(1, Number(object && object.height || 1) || 1),
                    style: clone(run.style || {}),
                    marks: clone(run.marks || []),
                }));
                return;
            }

            if (run.kind === 'math') {
                const fontSize = cssLengthToPixels(run.style && (run.style.fontSize ?? run.style.FontSize), 16);
                const width = Math.max(1, Number(run.mathLayoutWidth || run.width || 0) || Math.max(1, run.text.length * fontSize * 0.55));
                const height = Math.max(1, Number(run.mathLayoutHeight || run.height || 0) || fontSize * 1.25);
                tokens.push(sortObject({
                    type: 'math',
                    text: run.text,
                    start: run.start,
                    end: run.end,
                    length: run.text.length,
                    breakBefore: true,
                    breakAfter: true,
                    hardBreak: false,
                    unbreakable: true,
                    runId: run.id || null,
                    kind: run.kind,
                    math: run.math ? clone(run.math) : null,
                    width,
                    height,
                    mathLayoutAscent: Number(run.mathLayoutAscent || 0) || null,
                    mathLayoutDescent: Number(run.mathLayoutDescent || 0) || null,
                    style: clone(run.style || {}),
                    marks: clone(run.marks || []),
                }));
                return;
            }

            if (run.kind === 'contentControl') {
                tokens.push(sortObject({
                    type: 'word',
                    text: run.text,
                    start: run.start,
                    end: run.end,
                    length: run.text.length,
                    breakBefore: true,
                    breakAfter: true,
                    hardBreak: false,
                    unbreakable: true,
                    runId: run.id || null,
                    kind: run.kind,
                    contentControl: run.contentControl ? clone(run.contentControl) : null,
                    style: clone(run.style || {}),
                    marks: clone(run.marks || []),
                }));
                return;
            }

            const runStyle = clone(run.style || {});
            const runMarks = clone(run.marks || []);
            const runMath = run.math ? clone(run.math) : null;
            const runContentControl = run.contentControl ? clone(run.contentControl) : null;
            tokenizeText(run.text).forEach(function (token) {
                const normalized = Object.assign({}, token, {
                    start: token.start + run.start,
                    end: token.end + run.start,
                    runId: run.id || null,
                    kind: run.kind,
                    math: runMath,
                    contentControl: runContentControl,
                });
                normalized.style = runStyle;
                normalized.marks = runMarks;
                tokens.push(normalized);
            });
        });
        return { text: text, runs: runs, tokens: tokens };
    }

    return Object.freeze({
        flattenParagraphRuns,
        tokensForParagraph,
    });
}
