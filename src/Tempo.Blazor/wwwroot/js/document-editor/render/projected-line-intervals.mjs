// Phase D — render/projected-line-intervals.mjs
// `createResolveProjectedWysiwygLineIntervals({getAvailableIntervals, asArray})` →
//   `resolveProjectedWysiwygLineIntervals(y, lineHeight, bodyWidth, frame,
//   allExclusions)` — resolves the available text intervals on a projected line at
//   `y` against the exclusion set. When the line had to move down past blocking
//   geometry, uses the moved intervals + moved y. Normalises intervals to
//   `{id,x,y,width,height}` (x clamped ≥0, width ≥1), sorted left→right. Falls back
//   to a single full-body-width interval when no intervals survive.

export function createResolveProjectedWysiwygLineIntervals(options) {
    const opts = options || {};
    for (const key of ['getAvailableIntervals', 'asArray']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createResolveProjectedWysiwygLineIntervals requires options.${key} (function)`);
        }
    }
    const { getAvailableIntervals, asArray } = opts;

    return function resolveProjectedWysiwygLineIntervals(y, lineHeight, bodyWidth, frame, allExclusions) {
        const result = getAvailableIntervals(y, lineHeight, frame, allExclusions, 36,
            { pageIndex: 0, region: 'Body' });
        const moved = result && (result.moved === true || result.Moved === true);
        const lineY = moved ? Number(result.movedToY ?? result.MovedToY ?? y) || y : y;
        let intervals = asArray(moved
            ? (result.movedIntervals || result.MovedIntervals || result.intervals || result.Intervals)
            : (result && (result.intervals || result.Intervals
                || result.availableIntervals || result.AvailableIntervals)));
        intervals = intervals.map(function (interval, index) {
            return {
                id: (interval && (interval.id || interval.Id)) || ('projected-interval-' + index),
                x: Math.max(0, Number((interval && (interval.x ?? interval.X)) || 0) || 0),
                y: lineY,
                width: Math.max(0, Number((interval && (interval.width ?? interval.Width)) || 0) || 0),
                height: lineHeight,
            };
        }).filter(function (interval) {
            return interval.width >= 1;
        }).sort(function (a, b) {
            return a.x - b.x;
        });
        if (!intervals.length) {
            intervals = [{ id: 'projected-interval-0', x: 0, y: lineY, width: bodyWidth, height: lineHeight }];
        }
        return { y: lineY, intervals };
    };
}
