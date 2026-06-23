// Phase D — layout/line-breaker-fallback.mjs
// `createLineBreakerFallback({tokensForParagraph})` — factory returning
// `buildLineBreakerFallback(paragraph, service, options, reason)` which the line
// breaker invokes when normal range resolution fails (e.g. all candidate ranges
// are narrower than `minReadableWidth`). The fallback produces a single line
// rendered below the blocked region so the user still sees the paragraph.

import { sortObject } from '../core/helpers.mjs';

export function createLineBreakerFallback(options) {
    const opts = options || {};
    if (typeof opts.tokensForParagraph !== 'function') {
        throw new TypeError('createLineBreakerFallback requires options.tokensForParagraph (function)');
    }
    const { tokensForParagraph } = opts;

    function buildLineBreakerFallback(paragraph, service, opts2, reason) {
        const paragraphData = tokensForParagraph(paragraph);
        const style = paragraph && (paragraph.style || paragraph.Style) || {};
        const measurement = service.measureText(paragraphData.text || ' ', style);
        const safeWidth = Math.max(opts2.minReadableWidth, opts2.width || 0, 320);
        let blockedBottom = opts2.y;
        opts2.availableIntervals.forEach(function (interval) {
            const y = Number(interval.y || interval.Y || opts2.y || 0) || 0;
            const height = Number(interval.height || interval.Height || measurement.height || 20) || 20;
            blockedBottom = Math.max(blockedBottom, y + height);
        });
        const safeY = blockedBottom + Math.max(8, measurement.height * 0.5);
        const line = {
            id: 'fallback-line-0',
            index: 0,
            start: 0,
            end: paragraphData.text.length,
            hardBreak: false,
            rect: {
                x: opts2.x,
                y: safeY,
                width: Math.min(safeWidth, Math.max(safeWidth, measurement.width)),
                height: measurement.height,
            },
            availableIntervals: [{
                x: opts2.x, y: safeY, width: safeWidth, height: measurement.height,
                start: 0, end: paragraphData.text.length,
                collapsedOffset: paragraphData.text.length === 0 ? 0 : null,
                empty: paragraphData.text.length === 0,
            }],
            segments: [],
            justify: { enabled: false, extraSpacePerGap: 0, gapCount: 0 },
        };
        const segment = {
            id: 'fallback-segment-0',
            type: 'word',
            text: paragraphData.text,
            start: 0,
            end: paragraphData.text.length,
            rect: {
                x: opts2.x, y: safeY,
                width: Math.min(measurement.width, safeWidth),
                height: measurement.height,
            },
            splitFromLongToken: false,
        };
        line.segments.push(segment);
        return sortObject({
            ok: true,
            fallback: true,
            lines: [line],
            segments: [segment],
            caretStops: [{
                offset: 0,
                rect: { x: opts2.x, y: safeY, width: 1, height: measurement.height },
                lineId: line.id,
            }],
            text: paragraphData.text,
            formattingStateTouched: false,
            debug: {
                fallbackReason: reason || 'layout-fallback',
                tokenCount: paragraphData.tokens.length,
                cache: service.getStats(),
            },
        });
    }

    return Object.freeze({ buildLineBreakerFallback });
}
