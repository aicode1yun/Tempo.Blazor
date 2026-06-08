// Phase D — layout/line-breaker.mjs
// Factory wrapping `createLineBreaker` from the legacy IIFE. The breaker turns a
// paragraph's runs into laid-out lines, segments and caret stops. It depends on
// 13+ small helpers (tokenizer, range resolver, draft/finalize, justification),
// which are injected so this module can be unit-tested in isolation.
//
// The shape of the returned breaker matches the legacy contract:
//   { breakParagraph(paragraph, opts), getMeasurementStats() }

import { asArray, clone } from '../core/helpers.mjs';
import { baseDirection } from './bidi.mjs';

// Cold-layout optimization: layout output is read by field name, so the deep canonical key sort is
// skipped here (it dominated layout time). Pass-through keeps call sites + determinism unchanged.
function sortObject(value) {
    return value;
}
import { hyphenateTokenToFit, normalizeHyphenationOptions } from '../../document-editor-canvas/layout/hyphenation.mjs';

const SOFT_HYPHEN = '\u00AD';

export function createLineBreakerModule(options) {
    const opts = options || {};
    const required = [
        'createTextMeasurementService',
        'normalizeLineBreakerOptions',
        'resolveLineRangesForBreaker',
        'lineRangesAreInvalid',
        'buildLineBreakerFallback',
        'tokensForParagraph',
        'coalesceNonBreakingTokens',
        'normalizeParagraphAlignment',
        'createLineDraft',
        'materializeLineDraft',
        'splitTokenIntoFittingPieces',
        'applyJustifyMetadata',
    ];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createLineBreakerModule requires options.${key} (function)`);
        }
    }
    const {
        createTextMeasurementService,
        normalizeLineBreakerOptions,
        resolveLineRangesForBreaker,
        lineRangesAreInvalid,
        buildLineBreakerFallback,
        tokensForParagraph,
        coalesceNonBreakingTokens,
        normalizeParagraphAlignment,
        createLineDraft,
        materializeLineDraft,
        splitTokenIntoFittingPieces,
        applyJustifyMetadata,
    } = opts;

    function createLineBreaker(measurementService, defaultOptions) {
        const service = measurementService || createTextMeasurementService();
        const defaults = defaultOptions || {};

        function breakParagraph(paragraph, callOptions) {
            const merged = Object.assign({}, defaults, callOptions || {});
            const normalizedOptions = normalizeLineBreakerOptions(merged);
            const firstRanges = resolveLineRangesForBreaker(normalizedOptions, normalizedOptions.y, 18);
            if (lineRangesAreInvalid(firstRanges, normalizedOptions.minReadableWidth)) {
                return buildLineBreakerFallback(paragraph, service, normalizedOptions, 'invalid-available-interval');
            }

            const paragraphData = tokensForParagraph(paragraph);
            const tokens = mergeSoftHyphenTokens(coalesceNonBreakingTokens(paragraphData.tokens));
            const lines = [];
            const segments = [];
            const caretStops = [];
            const paragraphAlignment = paragraph && (paragraph.alignment ?? paragraph.Alignment);
            const rawAlignment = paragraphAlignment ?? merged.alignment ?? merged.Alignment ?? 'left';
            let alignment = normalizeParagraphAlignment(rawAlignment);
            // R.5.16 — an RTL paragraph that didn't set its OWN alignment defaults to right-aligned
            // (matches Word / Google Docs: RTL text starts at the right margin). An inherited/default
            // 'left' does not count as explicit.
            if (paragraphAlignment == null && alignment === 'left' && baseDirection(paragraphData.text) === 'rtl') {
                alignment = 'right';
            }
            let y = normalizedOptions.y;
            let current = createLineDraft(0, firstRanges, y);
            let nextSegmentId = 0;
            const hyphenationOptions = normalizeHyphenationOptions(merged.hyphenation || merged.Hyphenation, paragraphData.text);
            const hyphenationState = { consecutiveCount: 0 };

            function activeRange() {
                return current.ranges[Math.max(0, Math.min(current.rangeIndex, current.ranges.length - 1))];
            }

            function activeRangeHasContent() {
                const range = activeRange();
                return !!(range && range.segments && range.segments.length);
            }

            function tokenCanFitInLaterRange(width) {
                for (let i = current.rangeIndex + 1; i < current.ranges.length; i++) {
                    if (width <= current.ranges[i].width + 0.0001) return true;
                }
                return false;
            }

            function moveToNextRangeOrLine() {
                if (current.rangeIndex + 1 < current.ranges.length) {
                    current.rangeIndex++;
                    current.movedAcrossRange = true;
                    return 'range';
                }
                finishCurrent(false);
                return 'line';
            }

            function addCaretStopsForSegment(segment) {
                const length = Math.max(1, segment.end - segment.start);
                // R.5.16 — caret x per offset from the SHAPED prefix advance (font + Arabic
                // cursive joining aware) rather than linear interpolation, which wrongly assumes
                // every glyph is the same width. measureText shapes each prefix, so the caret
                // tracks real advances for proportional fonts and Arabic/RTL alike. Falls back to
                // interpolation when the segment text / measurement service isn't available.
                const text = typeof segment.text === 'string' ? segment.text : '';
                const style = segment.style || {};
                const baseX = segment.rect.x;
                const totalW = segment.rect.width;
                const canMeasure = text.length === length && service && typeof service.measureText === 'function';
                for (let i = segment.start; i <= segment.end; i++) {
                    const k = i - segment.start;
                    let x;
                    if (k <= 0) {
                        x = baseX;
                    } else if (k >= length) {
                        x = baseX + totalW;
                    } else if (canMeasure) {
                        x = baseX + Math.min(totalW, service.measureText(text.slice(0, k), style).width);
                    } else {
                        x = baseX + totalW * (k / length);
                    }
                    const stop = {
                        blockId: paragraph && (paragraph.id || paragraph.Id) || 'paragraph',
                        offset: i,
                        rangeIndex: segment.rangeIndex || 0,
                        rect: {
                            x: x,
                            y: segment.rect.y,
                            width: 1,
                            height: segment.rect.height,
                        },
                        lineId: current.id,
                    };
                    caretStops.push(stop);
                    const range = activeRange();
                    if (range) range.caretStops.push(stop);
                }
            }

            function addCaretStopsForInlineObject(segment) {
                const objectRect = segment.objectRect || segment.rect || {};
                const sy = Number(segment.rect && segment.rect.y || objectRect.y || current.y) || 0;
                const height = Math.max(1, Number(segment.rect && segment.rect.height
                    || objectRect.height || current.lineHeight || 1) || 1);
                const before = {
                    blockId: paragraph && (paragraph.id || paragraph.Id) || 'paragraph',
                    offset: Number(segment.start || 0) || 0,
                    affinity: 'before',
                    objectBoundary: true,
                    objectId: segment.objectId || null,
                    runId: segment.runId || null,
                    rangeIndex: segment.rangeIndex || 0,
                    rect: {
                        x: Number(objectRect.x || segment.rect && segment.rect.x || 0) || 0,
                        y: sy,
                        width: 1,
                        height: height,
                    },
                    lineId: current.id,
                };
                const after = {
                    blockId: paragraph && (paragraph.id || paragraph.Id) || 'paragraph',
                    offset: Number(segment.end ?? segment.start ?? 0) || 0,
                    affinity: 'after',
                    objectBoundary: true,
                    objectId: segment.objectId || null,
                    runId: segment.runId || null,
                    rangeIndex: segment.rangeIndex || 0,
                    rect: {
                        x: Number(objectRect.x || segment.rect && segment.rect.x || 0)
                            + Number(objectRect.width || segment.rect && segment.rect.width || 0),
                        y: sy,
                        width: 1,
                        height: height,
                    },
                    lineId: current.id,
                };
                caretStops.push(before);
                caretStops.push(after);
                const range = activeRange();
                if (range) {
                    range.caretStops.push(before);
                    range.caretStops.push(after);
                }
            }

            function measureToken(token, tokenText, style) {
                const visibleText = String(tokenText || '').replaceAll(SOFT_HYPHEN, '');
                const measured = service.measureText(visibleText || ' ', style);
                if (token?.type !== 'math') {
                    return measured;
                }

                const width = Number(token.width ?? token.mathLayoutWidth ?? 0);
                const height = Number(token.height ?? token.mathLayoutHeight ?? 0);
                return {
                    width: width > 0 ? width : measured.width,
                    height: height > 0 ? height : measured.height,
                };
            }

            function pushSegment(token, tokenText, start, end, width, style, splitFromLongToken, metadata) {
                const range = activeRange();
                const measured = Number(metadata?.height || 0) > 0
                    ? { height: Number(metadata.height) }
                    : measureToken(token, tokenText, style);
                const height = Math.max(1, Number(measured.height || 0) || 1);
                current.lineHeight = Math.max(current.lineHeight, height);
                const segment = {
                    id: 'segment-' + nextSegmentId++,
                    type: token.type,
                    text: tokenText,
                    start: start,
                    end: end,
                    runId: token.runId || null,
                    kind: token.kind || token.type || 'text',
                    math: token.math || null,
                    contentControl: token.contentControl || null,
                    style: token.style || null,
                    marks: Array.isArray(token.marks) ? token.marks : [],
                    rangeIndex: range.index,
                    rangeId: range.id,
                    rect: {
                        x: range.x + range.usedWidth,
                        y: current.y,
                        width: width,
                        height: height,
                    },
                    splitFromLongToken: splitFromLongToken === true,
                    hyphenated: metadata?.hyphenated === true,
                    hyphenation: metadata?.hyphenation || null,
                };
                current.segments.push(segment);
                if (segment.hyphenated === true) {
                    current.hyphenated = true;
                }
                range.segments.push(segment);
                range.usedWidth += width;
                range.start = range.start === null ? start : Math.min(range.start, start);
                range.end = Math.max(range.end, end);
                current.width = Math.max(current.width, range.x + range.usedWidth - current.visualLeft);
                current.start = current.start === null ? start : Math.min(current.start, start);
                current.end = Math.max(current.end, end);
                segments.push(segment);
                addCaretStopsForSegment(segment);
            }

            function pushInlineObjectSegment(token) {
                const range = activeRange();
                const object = token.object || {};
                const width = Math.max(1, Number(token.width || object.width || 1) || 1);
                const height = Math.max(1, Number(token.height || object.height || 1) || 1);
                current.lineHeight = Math.max(current.lineHeight, height);
                const rect = {
                    x: range.x + range.usedWidth,
                    y: current.y,
                    width: width,
                    height: current.lineHeight,
                };
                const segment = {
                    id: 'segment-' + nextSegmentId++,
                    type: 'inlineObject',
                    kind: 'drawing',
                    text: '',
                    start: Number(token.start || 0) || 0,
                    end: Number(token.end ?? token.start ?? 0) || 0,
                    runId: token.runId || null,
                    objectId: token.objectId || object.objectId || null,
                    rangeIndex: range.index,
                    rangeId: range.id,
                    object: clone(object),
                    objectRect: { x: rect.x, y: rect.y, width: width, height: height },
                    rect: rect,
                    splitFromLongToken: false,
                    inlineObject: true,
                };
                current.segments.push(segment);
                range.segments.push(segment);
                range.usedWidth += width;
                range.start = range.start === null ? segment.start : Math.min(range.start, segment.start);
                range.end = Math.max(range.end, segment.end);
                current.width = Math.max(current.width, range.x + range.usedWidth - current.visualLeft);
                current.start = current.start === null ? segment.start : Math.min(current.start, segment.start);
                current.end = Math.max(current.end, segment.end);
                segments.push(segment);
                addCaretStopsForInlineObject(segment);
            }

            function finishCurrent(hardBreak) {
                const lineWasHyphenated = current.hyphenated === true;
                const line = materializeLineDraft(current, lines.length, hardBreak === true, alignment);
                line.hyphenated = lineWasHyphenated;
                lines.push(line);
                Object.keys(line.rangeShifts || {}).forEach(function (key) {
                    const shift = Number(line.rangeShifts[key] || 0) || 0;
                    if (Math.abs(shift) < 0.0001) return;
                    caretStops.forEach(function (stop) {
                        if (stop.lineId === line.id && Number(stop.rangeIndex || 0) === Number(key)) {
                            stop.rect.x += shift;
                        }
                    });
                    // R.5.16 fix — materializeLineDraft applies the alignment shift only to its
                    // THROWAWAY (sortObject-cloned) ranges, so the offset never reached the real
                    // rendered segments. `current.segments` holds this line's live segment refs (the
                    // same objects pushed to the global `segments` array), tagged with rangeIndex.
                    current.segments.forEach(function (segment) {
                        if (Number(segment.rangeIndex || 0) === Number(key)) {
                            if (segment.rect) segment.rect.x += shift;
                            if (segment.objectRect) segment.objectRect.x += shift;
                        }
                    });
                });
                y = line.rect.y + line.rect.height + normalizedOptions.lineGap;
                current = createLineDraft(lines.length,
                    resolveLineRangesForBreaker(normalizedOptions, y, line.rect.height), y);
                hyphenationState.consecutiveCount = lineWasHyphenated ? hyphenationState.consecutiveCount + 1 : 0;
                if (lineRangesAreInvalid(current.ranges, normalizedOptions.minReadableWidth)) {
                    current.invalid = true;
                }
                return line;
            }

            function canHyphenateToken(token) {
                return hyphenationOptions.enabled === true
                    && (token.type === 'longToken' || token.type === 'word')
                    && String(token.text || '').indexOf('\u2011') < 0;
            }

            function pushHyphenatedToken(token, tokenText, tokenStyle, availableWidth, tokenIndex) {
                if (!canHyphenateToken(token)) {
                    return false;
                }

                const hyphenated = hyphenateTokenToFit(token, tokenText, tokenStyle, service, availableWidth, hyphenationOptions, hyphenationState);
                if (!hyphenated) {
                    return false;
                }

                pushSegment(token, hyphenated.text, hyphenated.start, hyphenated.end, hyphenated.width, tokenStyle, true, {
                    hyphenated: true,
                    hyphenation: hyphenated.hyphenation,
                });
                tokens.splice(tokenIndex + 1, 0, {
                    ...token,
                    text: hyphenated.remainderText,
                    start: hyphenated.remainderStart,
                    type: token.type === 'longToken' ? 'longToken' : 'word',
                    hyphenationRemainder: true,
                });
                return true;
            }

            for (let tokenIndex = 0; tokenIndex < tokens.length; tokenIndex++) {
                const token = tokens[tokenIndex];
                if (token.type === 'newline') {
                    finishCurrent(true);
                    continue;
                }
                if (current.invalid) {
                    return buildLineBreakerFallback(paragraph, service, normalizedOptions, 'invalid-available-interval');
                }
                if (token.type === 'inlineObject') {
                    const objectWidth = Math.max(1, Number(token.width || token.object && token.object.width || 1) || 1);
                    const objectRange = activeRange();
                    if (activeRangeHasContent() && objectRange.usedWidth + objectWidth > objectRange.width) {
                        moveToNextRangeOrLine();
                    }
                    if (current.invalid) {
                        return buildLineBreakerFallback(paragraph, service, normalizedOptions, 'invalid-available-interval');
                    }
                    pushInlineObjectSegment(token);
                    continue;
                }
                const tokenText = token.type === 'tab' ? '    ' : token.text;
                const tokenStyle = token.style || {};
                const measurement = measureToken(token, tokenText, tokenStyle);
                const width = measurement.width;
                const isBreakSpace = token.type === 'space';
                const isNonBreakingToken = token.type === 'nbsp' || token.type === 'nbspSequence';
                let range = activeRange();
                if (isBreakSpace && current.segments.length === 0 && current.movedAcrossRange !== true) {
                    current.start = current.start === null ? token.start : current.start;
                    current.end = Math.max(current.end, token.end);
                    continue;
                }
                if (range.segments.length === 0 && width > range.width && tokenCanFitInLaterRange(width)) {
                    moveToNextRangeOrLine();
                    range = activeRange();
                }
                if (range.segments.length > 0 && range.usedWidth + width > range.width) {
                    if (pushHyphenatedToken(token, tokenText, tokenStyle, range.width - range.usedWidth, tokenIndex)) {
                        moveToNextRangeOrLine();
                        continue;
                    }
                    const movedTo = moveToNextRangeOrLine();
                    range = activeRange();
                    if (isBreakSpace && movedTo === 'line') {
                        current.start = current.start === null ? token.start : current.start;
                        current.end = Math.max(current.end, token.end);
                        continue;
                    }
                    if (isNonBreakingToken && width > range.width && movedTo === 'range' && tokenCanFitInLaterRange(width)) {
                        moveToNextRangeOrLine();
                        range = activeRange();
                    }
                }
                if (current.invalid) {
                    return buildLineBreakerFallback(paragraph, service, normalizedOptions, 'invalid-available-interval');
                }
                range = activeRange();
                if (width > range.width && (token.type === 'longToken' || token.type === 'word' || token.type === 'cjk')) {
                    if (pushHyphenatedToken(token, tokenText, tokenStyle, range.width, tokenIndex)) {
                        moveToNextRangeOrLine();
                        continue;
                    }
                    const pieces = splitTokenIntoFittingPieces(token, tokenText, tokenStyle, service, range.width);
                    for (let pieceIndex = 0; pieceIndex < pieces.length; pieceIndex++) {
                        const piece = pieces[pieceIndex];
                        range = activeRange();
                        if (range.segments.length > 0 && range.usedWidth + piece.width > range.width) {
                            moveToNextRangeOrLine();
                            range = activeRange();
                        }
                        pushSegment(token, piece.text, piece.start, piece.end, piece.width, tokenStyle, true);
                    }
                    continue;
                }
                pushSegment(token, tokenText, token.start, token.end, width, tokenStyle, false, {
                    height: measurement.height,
                });
            }
            if (current.segments.length > 0 || lines.length === 0) finishCurrent(false);

            applyJustifyMetadata(lines, alignment);
            return sortObject({
                ok: true,
                fallback: false,
                lines: lines,
                segments: segments,
                caretStops: caretStops,
                text: paragraphData.text,
                formattingStateTouched: false,
                debug: {
                    tokenCount: tokens.length,
                    cache: service.getStats(),
                    fallbackReason: '',
                },
            });
        }

        return {
            breakParagraph: breakParagraph,
            getMeasurementStats: function () { return service.getStats(); },
        };
    }

    return Object.freeze({ createLineBreaker });
}

export function mergeSoftHyphenTokens(tokens) {
    const source = Array.isArray(tokens) ? tokens : [];
    const merged = [];
    for (let index = 0; index < source.length; index += 1) {
        const token = source[index];
        if (isWordLikeToken(token)) {
            let combined = token;
            let cursor = index;
            let consumedSoftHyphen = false;
            while (String(source[cursor + 1]?.text || '') === SOFT_HYPHEN && isWordLikeToken(source[cursor + 2])) {
                const after = source[cursor + 2];
                combined = {
                    ...combined,
                    type: combined.type === 'longToken' || after.type === 'longToken' ? 'longToken' : 'word',
                    text: `${combined.text}${SOFT_HYPHEN}${after.text}`,
                    end: after.end,
                    length: Math.max(0, Number(after.end ?? combined.end ?? 0) - Number(combined.start ?? 0)),
                    breakAfter: after.breakAfter ?? combined.breakAfter,
                    unbreakable: combined.unbreakable === true || after.unbreakable === true,
                    runId: combined.runId === after.runId ? combined.runId : null,
                };
                cursor += 2;
                consumedSoftHyphen = true;
            }

            if (consumedSoftHyphen) {
                merged.push(combined);
                index = cursor;
                continue;
            }
        }

        if (String(token?.text || '') !== SOFT_HYPHEN) {
            merged.push(token);
            continue;
        }
    }

    return merged;
}

function isWordLikeToken(token) {
    return token?.type === 'word' || token?.type === 'longToken';
}
