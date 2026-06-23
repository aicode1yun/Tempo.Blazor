// Phase D — render/wysiwyg-render-helpers.mjs
// `normalizeScopedBlockIdSet(value)` — Set of non-empty block ids, or null when
//   the input has none (signals "full pass" to the scoped layout code).
// `createWysiwygParagraphProjectionSignature(segments, height, top)` — stable
//   checksum over a projected paragraph's text/offset/rect data (rounded to 0.1px)
//   so the renderer can skip re-projection when nothing visible changed.
// `createObjectEntryPaintLayer({drawingLayerForWrapMode})` →
//   `objectEntryPaintLayer(entry)` — the paint layer name for an object entry
//   derived from its wrap mode (default `'Inline'`).

import { asArray, asText } from '../core/helpers.mjs';
import { stableChecksum } from './render-snapshot.mjs';

export function normalizeScopedBlockIdSet(value) {
    const ids = asArray(value).map(asText).filter(Boolean);
    if (!ids.length) return null;
    const set = new Set();
    ids.forEach(function (id) { set.add(id); });
    return set;
}

export function createWysiwygParagraphProjectionSignature(segments, height, top) {
    return stableChecksum({
        height: Math.round(Number(height || 0) * 10) / 10,
        top: Math.round(Number(top || 0) * 10) / 10,
        segments: asArray(segments).map(function (segment) {
            const rect = segment.rect || {};
            return [
                segment.text || '',
                segment.start,
                segment.end,
                Math.round(Number(rect.x || 0) * 10) / 10,
                Math.round(Number(rect.y || 0) * 10) / 10,
                Math.round(Number(rect.width || 0) * 10) / 10,
                Math.round(Number(rect.height || 0) * 10) / 10,
            ];
        }),
    });
}

export function createObjectEntryPaintLayer(options) {
    const opts = options || {};
    if (typeof opts.drawingLayerForWrapMode !== 'function') {
        throw new TypeError(
            'createObjectEntryPaintLayer requires options.drawingLayerForWrapMode (function)');
    }
    const { drawingLayerForWrapMode } = opts;
    return function objectEntryPaintLayer(entry) {
        const object = (entry && entry.object) || {};
        return drawingLayerForWrapMode(object.wrapMode || object.WrapMode || 'Inline');
    };
}
