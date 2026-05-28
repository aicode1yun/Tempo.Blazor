// Phase D — layout/text-exclusion-manager.mjs
// `createTextExclusionManager(exclusions, bodyFrame, options)` — builds an
// in-memory query helper over a set of text exclusions:
//   .bodyFrame                  — the rect this manager covers
//   .scopeKey                   — '' when no scope is set, else the scope key
//   .exclusions                 — pre-filtered source list (matches scope, allowOverlap kept)
//   .collectBlockedIntervals(y, h, minW) — `{blockedIntervals, blockingBottom}` for a line
//   .computeAt(y, h, minW)      — `{intervals, blockedIntervals, blockingBottom}` (body minus blocked)
//   .resolveLine(y, h, minW)    — `{…, movedToY, moved, movedIntervals, movedBlockedIntervals}`
//                                  for a line that gets pushed below blockingBottom when empty
//   .getAvailableIntervals(y, h, minW) — sugar returning `{intervals, blockedIntervals, …}`
//
// `options.intervalCacheStats` may be an object — counters get incremented in-place.

import { asArray, sortObject } from '../core/helpers.mjs';
import {
    rectFromGeometry,
    rectIntersectsGeometry,
    rectBottomGeometry,
} from '../objects/geometry.mjs';
import { normalizeWrapModeName, normalizeWrapSideName } from '../objects/wrap-modes.mjs';
import {
    createTextExclusionScopeDescriptor,
    textExclusionMatchesScope,
} from './text-exclusion-scope.mjs';
import { blockedIntervalsForExclusionGeometry } from './blocked-intervals.mjs';
import {
    normalizeManagerInterval,
    mergeBlockedIntervalsForLayout,
    subtractBlockedIntervalsFromBody,
} from './exclusion-intervals.mjs';

export function createTextExclusionManager(exclusions, bodyFrame, options) {
    const opts = options || {};
    const body = rectFromGeometry(
        bodyFrame || opts.bodyFrame || opts.BodyFrame
        || { x: 0, y: 0, width: 640, height: 900 });
    const intervalCacheStats = opts.intervalCacheStats || opts.IntervalCacheStats || null;
    if (intervalCacheStats) {
        intervalCacheStats.managerBuilds = Number(intervalCacheStats.managerBuilds || 0) + 1;
    }
    const scopeDescriptor = createTextExclusionScopeDescriptor(opts);
    const sourceExclusions = asArray(exclusions).filter(Boolean).filter(function (exclusion) {
        return textExclusionMatchesScope(exclusion, scopeDescriptor);
    });

    function collectBlockedIntervals(atY, lineHeight, minReadableWidth) {
        if (intervalCacheStats) {
            intervalCacheStats.lineResolveCount = Number(intervalCacheStats.lineResolveCount || 0) + 1;
        }
        const lineY = Number(atY || 0);
        const height = Math.max(1, Number(lineHeight || 1) || 1);
        const minWidth = Math.max(1,
            Number(minReadableWidth || opts.minReadableWidth || opts.MinReadableWidth || 48) || 48);
        const lineRect = { x: body.x, y: lineY, width: body.width, height };
        let blockingBottom = lineY;
        const blocked = [];

        sourceExclusions.forEach(function (exclusion) {
            if (intervalCacheStats) {
                intervalCacheStats.exclusionScanCount =
                    Number(intervalCacheStats.exclusionScanCount || 0) + 1;
            }
            if (!exclusion
                || exclusion.allowOverlap === true
                || exclusion.AllowOverlap === true) return;
            const rect = rectFromGeometry(exclusion.rect || exclusion.Rect);
            if (!rectIntersectsGeometry(lineRect, rect)) return;
            blockingBottom = Math.max(blockingBottom, rectBottomGeometry(rect));
            if (intervalCacheStats) {
                intervalCacheStats.blockedGeometryComputeCount =
                    Number(intervalCacheStats.blockedGeometryComputeCount || 0) + 1;
                if (asArray(exclusion.polygon || exclusion.Polygon).length >= 3) {
                    intervalCacheStats.polygonComputationCount =
                        Number(intervalCacheStats.polygonComputationCount || 0) + 1;
                }
            }
            blockedIntervalsForExclusionGeometry(exclusion, lineY, height, body, minWidth)
                .forEach(function (interval) {
                    blocked.push(normalizeManagerInterval(interval, lineY, height, {
                        objectId: exclusion.objectId || exclusion.ObjectId || '',
                        blockId: exclusion.blockId || exclusion.BlockId || '',
                        wrapMode: normalizeWrapModeName(
                            exclusion.wrapMode || exclusion.WrapMode),
                        wrapSide: normalizeWrapSideName(
                            exclusion.wrapSide || exclusion.WrapSide),
                    }));
                });
        });

        return sortObject({
            blockedIntervals: mergeBlockedIntervalsForLayout(blocked, body, minWidth, lineY, height),
            blockingBottom,
        });
    }

    function computeAt(atY, lineHeight, minReadableWidth) {
        const collected = collectBlockedIntervals(atY, lineHeight, minReadableWidth);
        return sortObject({
            intervals: subtractBlockedIntervalsFromBody(
                body, atY, lineHeight, collected.blockedIntervals, minReadableWidth),
            blockedIntervals: collected.blockedIntervals,
            blockingBottom: collected.blockingBottom,
        });
    }

    function resolveLine(atY, lineHeight, minReadableWidth) {
        const lineY = Number(atY || 0);
        const height = Math.max(1, Number(lineHeight || 1) || 1);
        const minWidth = Math.max(1,
            Number(minReadableWidth || opts.minReadableWidth || opts.MinReadableWidth || 48) || 48);
        const initial = computeAt(lineY, height, minWidth);
        let movedToY = lineY;
        let moved = false;
        let movedIntervals = initial.intervals;
        let movedBlockedIntervals = initial.blockedIntervals;

        if (initial.intervals.length === 0) {
            movedToY = Math.max(lineY + height, initial.blockingBottom);
            moved = movedToY > lineY;
            const movedResult = computeAt(movedToY, height, minWidth);
            movedIntervals = movedResult.intervals;
            movedBlockedIntervals = movedResult.blockedIntervals;
        }

        return sortObject({
            bodyFrame: body,
            y: lineY,
            height,
            minReadableWidth: minWidth,
            intervals: initial.intervals,
            blockedIntervals: initial.blockedIntervals,
            blockingBottom: initial.blockingBottom,
            movedToY,
            moved,
            movedIntervals,
            movedBlockedIntervals,
        });
    }

    return {
        bodyFrame: body,
        scopeKey: scopeDescriptor && scopeDescriptor.enabled ? scopeDescriptor.scopeKey : '',
        exclusions: sourceExclusions,
        collectBlockedIntervals,
        computeAt,
        resolveLine,
        getAvailableIntervals(atY, lineHeight, minReadableWidth) {
            const resolved = resolveLine(atY, lineHeight, minReadableWidth);
            return sortObject({
                intervals: resolved.movedIntervals,
                blockedIntervals: resolved.blockedIntervals,
                initialIntervals: resolved.intervals,
                movedToY: resolved.movedToY,
                moved: resolved.moved,
            });
        },
    };
}
