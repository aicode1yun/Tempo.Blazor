// Phase D — objects/preview-intervals-signature.mjs
// Stable string signatures for image-move preview reflow comparison. During an
// image drag the engine recomputes the text intervals each frame; comparing these
// signatures lets it skip re-rendering when the reflow is unchanged.
//
// `normalizePreviewIntervalForCompare(interval)` — `"x:width"` with both rounded to
//   2 decimals (Pascal/camel accepted), so sub-pixel jitter doesn't bust the match.
// `previewIntervalsSignature(lines)` — per line: `"y|intervals|blockedIntervals"`
//   joined with `;`. Intervals within a line are joined with `,`.

import { asArray } from '../core/helpers.mjs';

export function normalizePreviewIntervalForCompare(interval) {
    return [
        Math.round(Number((interval && (interval.x ?? interval.X)) || 0) * 100) / 100,
        Math.round(Number((interval && (interval.width ?? interval.Width)) || 0) * 100) / 100,
    ].join(':');
}

export function previewIntervalsSignature(lines) {
    return asArray(lines).map(function (line) {
        return [
            Math.round(Number((line && line.y) || 0) * 100) / 100,
            asArray(line && line.intervals).map(normalizePreviewIntervalForCompare).join(','),
            asArray(line && line.blockedIntervals).map(normalizePreviewIntervalForCompare).join(','),
        ].join('|');
    }).join(';');
}
