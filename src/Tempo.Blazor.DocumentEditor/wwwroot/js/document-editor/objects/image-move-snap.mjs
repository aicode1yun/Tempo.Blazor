// Phase D — objects/image-move-snap.mjs
// `createComputeImageMoveSnap({rectFromAny, asArray})` →
//   `computeImageMoveSnap(point, context)` — snaps an image-move drag point to
//   common alignment candidates (left/right body edge, page horizontal center,
//   adjacent-object left edge, line tops) when within a 5-pixel tolerance. Returns
//   `{x, y, guides}` where `guides` lists which candidate snapped (`text-left`,
//   `text-right`, `page-center-x`, `object-left`, `line-top`).
// `context.disableSnap === true` short-circuits — returns the raw point.

export function createComputeImageMoveSnap(options) {
    const opts = options || {};
    if (typeof opts.rectFromAny !== 'function') {
        throw new TypeError(
            'createComputeImageMoveSnap requires options.rectFromAny (function)');
    }
    if (typeof opts.asArray !== 'function') {
        throw new TypeError(
            'createComputeImageMoveSnap requires options.asArray (function)');
    }
    const { rectFromAny, asArray } = opts;

    return function computeImageMoveSnap(point, context) {
        const ctx = context || {};
        const result = {
            x: Number((point && point.x) || 0) || 0,
            y: Number((point && point.y) || 0) || 0,
            guides: [],
        };
        if (ctx.disableSnap) return result;
        const body = rectFromAny(ctx.bodyRect);
        const size = rectFromAny(ctx.objectSize);
        const candidates = [
            { x: body.X, Kind: 'text-left' },
            { x: body.X + body.Width - size.Width, Kind: 'text-right' },
            { x: body.X + body.Width / 2 - size.Width / 2, Kind: 'page-center-x' },
        ];
        asArray(ctx.otherObjects).forEach(function (object) {
            const rect = rectFromAny(object.Rect);
            candidates.push({ x: rect.X - size.Width, Kind: 'object-left' });
        });
        candidates.forEach(function (candidate) {
            if (Math.abs(result.x - candidate.x) <= 5) {
                result.x = candidate.x;
                result.guides.push({ Kind: candidate.Kind, X: candidate.x });
            }
        });
        asArray(ctx.lines).forEach(function (line) {
            const rect = rectFromAny(line.Rect);
            if (Math.abs(result.y - rect.Y) <= 5) {
                result.y = rect.Y;
                result.guides.push({ Kind: 'line-top', Y: rect.Y });
            }
        });
        return result;
    };
}
