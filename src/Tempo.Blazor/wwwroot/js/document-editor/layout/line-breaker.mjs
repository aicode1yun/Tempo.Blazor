// Phase D — layout/line-breaker.mjs
// Factory wrapping `createLineBreaker` from the legacy IIFE. The breaker turns a
// paragraph's runs into laid-out lines, segments and caret stops. It depends on
// 13+ small helpers (tokenizer, range resolver, draft/finalize, justification),
// which are injected so this module can be unit-tested in isolation.
//
// The shape of the returned breaker matches the legacy contract:
//   { breakParagraph(paragraph, opts), getMeasurementStats() }

import { asArray, clone, sortObject } from '../core/helpers.mjs';
import { baseDirection } from './bidi.mjs';

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
            const tokens = coalesceNonBreakingTokens(paragraphData.tokens);
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

            function pushSegment(token, tokenText, start, end, width, style, splitFromLongToken) {
                const range = activeRange();
                const height = service.measureText(tokenText || ' ', style).height;
                current.lineHeight = Math.max(current.lineHeight, height);
                const segment = {
                    id: 'segment-' + nextSegmentId++,
                    type: token.type,
                    text: tokenText,
                    start: start,
                    end: end,
                    runId: token.runId || null,
                    rangeIndex: range.index,
                    rangeId: range.id,
                    rect: {
                        x: range.x + range.usedWidth,
                        y: current.y,
                        width: width,
                        height: height,
                    },
                    splitFromLongToken: splitFromLongToken === true,
                };
                current.segments.push(segment);
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
                const line = materializeLineDraft(current, lines.length, hardBreak === true, alignment);
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
                if (lineRangesAreInvalid(current.ranges, normalizedOptions.minReadableWidth)) {
                    current.invalid = true;
                }
                return line;
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
                const measurement = service.measureText(tokenText, tokenStyle);
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
                pushSegment(token, tokenText, token.start, token.end, width, tokenStyle, false);
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
