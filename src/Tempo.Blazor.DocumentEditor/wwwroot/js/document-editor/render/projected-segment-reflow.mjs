// Phase D — render/projected-segment-reflow.mjs
// `createReflowProjectedWysiwygSegments({asArray, asText, clone,
//   resolveProjectedWysiwygLineIntervals})` →
//   `reflowProjectedWysiwygSegments(segments, paragraphTop, lineHeight, bodyWidth,
//   frame, allExclusions)` — greedy line-breaker that flows pre-tokenized projected
//   segments through the available intervals around text exclusions. Leading
//   whitespace on a fresh line is dropped, tokens advance the cursor, and the
//   cursor jumps to the next interval (or a new line) when a token won't fit.
//   Returns `{segments, lines}` where each output segment carries its resolved rect
//   and each line records its y + a clone of the intervals it used.

export function createReflowProjectedWysiwygSegments(options) {
    const opts = options || {};
    for (const key of ['asArray', 'asText', 'clone', 'resolveProjectedWysiwygLineIntervals']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createReflowProjectedWysiwygSegments requires options.${key} (function)`);
        }
    }
    const { asArray, asText, clone, resolveProjectedWysiwygLineIntervals } = opts;

    return function reflowProjectedWysiwygSegments(
        segments, paragraphTop, lineHeight, bodyWidth, frame, allExclusions) {
        const source = asArray(segments).map(function (segment, index) {
            return Object.assign({}, segment, {
                __tmIndex: index,
                rect: Object.assign({}, (segment && segment.rect) || {}),
            });
        }).sort(function (a, b) {
            return Number(a.start ?? 0) - Number(b.start ?? 0)
                || Number(a.__tmIndex || 0) - Number(b.__tmIndex || 0);
        });
        const output = [];
        const lines = [];
        let y = paragraphTop;
        let line = resolveProjectedWysiwygLineIntervals(y, lineHeight, bodyWidth, frame, allExclusions);
        let intervalIndex = 0;
        let cursorX = line.intervals[0].x;
        let hasContentOnLine = false;

        function currentInterval() {
            return line.intervals[Math.max(0, Math.min(intervalIndex, line.intervals.length - 1))];
        }

        function startNewLine(minY) {
            y = Math.max(Number(minY || 0) || 0, Number(line.y || y) + lineHeight);
            line = resolveProjectedWysiwygLineIntervals(y, lineHeight, bodyWidth, frame, allExclusions);
            intervalIndex = 0;
            cursorX = line.intervals[0].x;
            hasContentOnLine = false;
        }

        function moveToNextIntervalOrLine(width) {
            for (let i = intervalIndex + 1; i < line.intervals.length; i++) {
                if (Number(line.intervals[i].width || 0) + 0.0001 >= width) {
                    intervalIndex = i;
                    cursorX = line.intervals[i].x;
                    hasContentOnLine = false;
                    return;
                }
            }
            startNewLine();
        }

        source.forEach(function (segment) {
            const text = asText(segment.text || '');
            const width = Math.max(1, Number((segment.rect && segment.rect.width) || 1) || 1);
            const isSpace = /^\s+$/.test(text);
            if (isSpace && !hasContentOnLine) return;
            let guard = 0;
            while (guard++ < 100) {
                const interval = currentInterval();
                const intervalRight = Number(interval.x || 0) + Number(interval.width || 0);
                if (cursorX + width <= intervalRight + 0.0001
                    || width > Number(interval.width || 0)) break;
                moveToNextIntervalOrLine(width);
                if (isSpace && !hasContentOnLine) return;
            }
            const rect = {
                x: cursorX,
                y: Number(line.y || y) || y,
                width,
                height: lineHeight,
            };
            const next = Object.assign({}, segment, { rect });
            output.push(next);
            cursorX += width;
            hasContentOnLine = hasContentOnLine || !isSpace;
            const lineId = 'projected-line-' + lines.length;
            const existing = lines.find(function (candidate) {
                return Math.abs(Number((candidate.rect && candidate.rect.y) || 0) - rect.y) < 0.5;
            });
            if (!existing) {
                lines.push({
                    id: lineId,
                    rect: { x: 0, y: rect.y, width: bodyWidth, height: lineHeight },
                    availableIntervals: line.intervals.map(clone),
                });
            }
        });

        return { segments: output, lines };
    };
}
