// Phase D — layout/caret-math.mjs
// Pure caret-position helpers used by the hit-testing pipeline.
//
//   - `finiteNumber(value, fallback)` — robust Number coercion that rejects NaN/±Infinity.
//   - `caretOffsetFromInterval(interval, x)` — given an x coordinate and an interval
//     (`{x, width, start, end, collapsedOffset?}`), interpolate the character offset.
//     Collapsed intervals (end ≤ start) return the explicit collapsedOffset.
//   - `nearestOffsetWithinLine(line, x)` — Phase D port of the hot-path that picks the
//     best interval in a line, then delegates to caretOffsetFromInterval.

import { asArray } from '../core/helpers.mjs';

export function finiteNumber(value, fallback) {
    const number = Number(value);
    return typeof number === 'number'
        && number === number
        && number !== Infinity
        && number !== -Infinity
        ? number
        : fallback;
}

export function caretOffsetFromInterval(interval, x) {
    const start = Math.max(0, finiteNumber(interval && interval.start, 0));
    const end = Math.max(start, finiteNumber(interval && interval.end, start));
    if (end <= start) {
        return Math.max(0, finiteNumber(interval && interval.collapsedOffset, start));
    }
    const width = Math.max(1, finiteNumber(interval && interval.width, 1));
    const ratio = Math.max(0, Math.min(1,
        (Number(x || 0) - finiteNumber(interval && interval.x, 0)) / width));
    return Math.max(start, Math.min(end, Math.round(start + ratio * (end - start))));
}

export function nearestOffsetWithinLine(line, x) {
    const start = Math.max(0, Number(line && line.start || 0) || 0);
    const end = Math.max(start, Number(line && line.end || start) || start);
    if (end <= start || line && line.empty === true) return start;
    const intervals = asArray(line && (line.availableIntervals
        || line.AvailableIntervals
        || line.ranges
        || line.Ranges)).filter(function (interval) {
            return interval && Number(interval.width ?? interval.Width ?? 0) > 0;
        });
    if (intervals.length) {
        const px = Number(x || 0) || 0;
        let best = null;
        intervals.forEach(function (interval) {
            const left = Number(interval.x ?? interval.X ?? 0) || 0;
            const width = Math.max(1, Number(interval.width ?? interval.Width ?? 1) || 1);
            const right = left + width;
            const distance = px >= left && px <= right
                ? -1
                : Math.min(Math.abs(px - left), Math.abs(px - right));
            if (!best || distance < best.distance) {
                best = {
                    distance: distance,
                    interval: {
                        x: left,
                        width: width,
                        start: interval.start ?? interval.Start ?? start,
                        end: interval.end ?? interval.End ?? end,
                        collapsedOffset: interval.collapsedOffset
                            ?? interval.CollapsedOffset ?? null,
                    },
                };
            }
        });
        if (best) return Math.max(start, Math.min(end, caretOffsetFromInterval(best.interval, px)));
    }
    const rect = line.rect || {};
    const width = Math.max(1, Number(rect.width || 0) || 1);
    const ratio = Math.max(0, Math.min(1,
        (Number(x || 0) - Number(rect.x || 0)) / width));
    return Math.max(start, Math.min(end, Math.round(start + ratio * (end - start))));
}
