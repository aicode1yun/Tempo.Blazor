// Phase D — render/projected-segment-split.mjs
// `createSplitProjectedWysiwygSegmentsForReflow({createTextMeasurementService,
//   asArray, asText})` → `splitProjectedWysiwygSegmentsForReflow(segments,
//   fallbackStyle)` — breaks each projected segment into whitespace/word tokens,
//   measuring each token's width so the reflow pass can place them around text
//   exclusions. Each token carries its own start/end (offset within the original
//   segment) and a per-token rect width. Returns the original segments unchanged
//   when nothing tokenizes (e.g. all-empty text).

export function createSplitProjectedWysiwygSegmentsForReflow(options) {
    const opts = options || {};
    for (const key of ['createTextMeasurementService', 'asArray', 'asText']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createSplitProjectedWysiwygSegmentsForReflow requires options.${key} (function)`);
        }
    }
    const { createTextMeasurementService, asArray, asText } = opts;

    return function splitProjectedWysiwygSegmentsForReflow(segments, fallbackStyle) {
        const service = createTextMeasurementService();
        const output = [];
        asArray(segments).forEach(function (segment, segmentIndex) {
            const text = asText((segment && segment.text) || '');
            if (!text) return;
            const style = Object.assign({}, fallbackStyle || {}, (segment && segment.style) || {});
            const baseStart = Number((segment && (segment.start ?? segment.Start)) || 0) || 0;
            const tokens = text.match(/\s+|[^\s]+/g) || [text];
            let cursor = 0;
            tokens.forEach(function (token, tokenIndex) {
                if (!token) return;
                const tokenWidth = Math.max(1, service.measureText(token, style).width);
                const rect = Object.assign({}, (segment && segment.rect) || {}, { width: tokenWidth });
                output.push(Object.assign({}, segment, {
                    id: asText((segment && segment.id) || (segment && segment.Id) || 'segment')
                        + '-token-' + segmentIndex + '-' + tokenIndex,
                    text: token,
                    start: baseStart + cursor,
                    end: baseStart + cursor + token.length,
                    rect,
                    style,
                }));
                cursor += token.length;
            });
        });
        return output.length ? output : segments;
    };
}
